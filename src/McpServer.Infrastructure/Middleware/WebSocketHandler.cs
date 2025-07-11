using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using McpServer.Application.Server;
using McpServer.Domain.Transport;
using McpServer.Infrastructure.Transport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Infrastructure.Middleware;

/// <summary>
/// Handler for individual WebSocket connections for MCP communication.
/// </summary>
public class WebSocketHandler : IDisposable
{
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly IMessageRouter _messageRouter;
    private readonly IOptions<WebSocketTransportOptions> _options;
    private readonly WebSocket _webSocket;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly string _connectionId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="messageRouter">The message router.</param>
    /// <param name="options">The WebSocket options.</param>
    /// <param name="webSocket">The WebSocket connection.</param>
    /// <param name="connectionId">Unique identifier for this connection.</param>
    /// <param name="cancellationToken">Cancellation token for the connection lifetime.</param>
    public WebSocketHandler(
        ILogger<WebSocketHandler> logger,
        IMessageRouter messageRouter,
        IOptions<WebSocketTransportOptions> options,
        WebSocket webSocket,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        _logger = logger;
        _messageRouter = messageRouter;
        _options = options;
        _webSocket = webSocket;
        _connectionId = connectionId;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Handles the WebSocket connection lifecycle.
    /// </summary>
    public async Task HandleConnectionAsync()
    {
        _logger.LogInformation("Starting WebSocket handler for connection {ConnectionId}", _connectionId);
        
        try
        {
            var receiveTask = ReceiveLoopAsync(_cancellationTokenSource.Token);
            await receiveTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("WebSocket connection {ConnectionId} was cancelled", _connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection {ConnectionId}", _connectionId);
        }
        finally
        {
            await CloseConnectionAsync();
            _logger.LogInformation("WebSocket handler for connection {ConnectionId} completed", _connectionId);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[_options.Value.ReceiveBufferSize]);
        var messageBuilder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket close message received for connection {ConnectionId}: {Status} - {Description}",
                            _connectionId, result.CloseStatus, result.CloseStatusDescription);
                        return;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var text = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, result.Count);
                        messageBuilder.Append(text);
                    }
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = messageBuilder.ToString();
                    messageBuilder.Clear();

                    if (!string.IsNullOrEmpty(message))
                    {
                        _logger.LogDebug("Received message on connection {ConnectionId}: {Message}", _connectionId, message);
                        await ProcessMessageAsync(message, cancellationToken);
                    }
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogWarning("WebSocket connection {ConnectionId} closed prematurely", _connectionId);
                break;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving WebSocket message on connection {ConnectionId}", _connectionId);
                break;
            }
        }
    }

    private async Task ProcessMessageAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            // Route the message through the message router
            var response = await _messageRouter.RouteMessageAsync(message, cancellationToken);
            
            // Send response if we got one (notifications don't have responses)
            if (response != null)
            {
                await SendResponseAsync(response, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message on connection {ConnectionId}", _connectionId);
            
            // Try to send an error response
            try
            {
                var errorResponse = new
                {
                    jsonrpc = "2.0",
                    error = new
                    {
                        code = -32603,
                        message = "Internal error"
                    },
                    id = (object?)null
                };
                
                await SendResponseAsync(errorResponse, cancellationToken);
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx, "Failed to send error response on connection {ConnectionId}", _connectionId);
            }
        }
    }

    private async Task SendResponseAsync(object response, CancellationToken cancellationToken)
    {
        if (_webSocket.State != WebSocketState.Open)
        {
            _logger.LogWarning("Cannot send response on connection {ConnectionId}: WebSocket is not open", _connectionId);
            return;
        }

        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);

            _logger.LogDebug("Sent response on connection {ConnectionId}: {Response}", _connectionId, json);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    private async Task CloseConnectionAsync()
    {
        if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Handler disposed",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing WebSocket connection {ConnectionId}", _connectionId);
            }
        }
    }

    /// <summary>
    /// Disposes the WebSocket handler.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource.Cancel();
            _writeSemaphore.Dispose();
            _cancellationTokenSource.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}