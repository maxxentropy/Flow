using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using McpServer.Domain.Transport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Infrastructure.Transport;

/// <summary>
/// WebSocket transport implementation for MCP communication.
/// </summary>
public class WebSocketTransport : ITransport, IDisposable
{
    private readonly ILogger<WebSocketTransport> _logger;
    private readonly IOptions<WebSocketTransportOptions> _options;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private WebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketTransport"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The transport options.</param>
    public WebSocketTransport(ILogger<WebSocketTransport> logger, IOptions<WebSocketTransportOptions> options)
    {
        _logger = logger;
        _options = options;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc/>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <inheritdoc/>
    public event EventHandler<DisconnectedEventArgs>? Disconnected;

    /// <inheritdoc/>
    public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;

    /// <summary>
    /// Accepts a WebSocket connection from an HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context containing the WebSocket upgrade request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task AcceptWebSocketAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            throw new InvalidOperationException("Not a WebSocket request");
        }

        // Validate origin if configured
        if (_options.Value.AllowedOrigins?.Count > 0)
        {
            var origin = context.Request.Headers["Origin"].ToString();
            if (!string.IsNullOrEmpty(origin) && !_options.Value.AllowedOrigins.Contains(origin))
            {
                _logger.LogWarning("Rejected WebSocket connection from unauthorized origin: {Origin}", origin);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Forbidden: Origin not allowed", cancellationToken);
                return;
            }
        }

        _logger.LogInformation("Accepting WebSocket connection from {RemoteIpAddress}", 
            context.Connection.RemoteIpAddress);

        _webSocket = await context.WebSockets.AcceptWebSocketAsync(_options.Value.SubProtocol);
        await StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
        {
            throw new InvalidOperationException("Transport is already started");
        }

        if (_webSocket == null)
        {
            throw new InvalidOperationException("WebSocket is not initialized. Call AcceptWebSocketAsync first.");
        }

        _logger.LogInformation("Starting WebSocket transport");

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isConnected = true;

        // Start receiving messages
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

        _logger.LogInformation("WebSocket transport started");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            return;
        }

        _logger.LogInformation("Stopping WebSocket transport");

        _isConnected = false;
        _cancellationTokenSource?.Cancel();

        // Close the WebSocket connection gracefully
        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Transport stopped",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing WebSocket connection");
            }
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _logger.LogInformation("WebSocket transport stopped");
    }

    /// <inheritdoc/>
    public async Task SendMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Transport is not connected");
        }

        await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _webSocket!.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Sent message: {Message}", json);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                StopAsync().GetAwaiter().GetResult();
                _writeSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();
                _webSocket?.Dispose();
            }

            _disposed = true;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[_options.Value.ReceiveBufferSize]);
        var messageBuilder = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _logger.LogInformation("WebSocket close message received: {Status} - {Description}",
                                result.CloseStatus, result.CloseStatusDescription);
                            
                            await HandleCloseAsync(result, cancellationToken);
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
                            _logger.LogDebug("Received message: {Message}", message);
                            OnMessageReceived(message);
                        }
                    }
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    _logger.LogWarning("WebSocket connection closed prematurely");
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving WebSocket message");
                    OnDisconnected("Receive error", ex);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in receive loop");
            OnDisconnected("Fatal error", ex);
        }
        finally
        {
            _isConnected = false;
            OnDisconnected("Transport closed", null);
        }
    }

    private async Task HandleCloseAsync(WebSocketReceiveResult result, CancellationToken cancellationToken)
    {
        try
        {
            if (_webSocket?.State == WebSocketState.CloseReceived)
            {
                await _webSocket.CloseOutputAsync(
                    result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                    result.CloseStatusDescription ?? "Closing",
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket close");
        }
    }

    private void OnMessageReceived(string message)
    {
        try
        {
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message received handler");
        }
    }

    private void OnDisconnected(string? reason, Exception? exception)
    {
        try
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(reason, exception));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in disconnected handler");
        }
    }
}

/// <summary>
/// Configuration options for the WebSocket transport.
/// </summary>
public class WebSocketTransportOptions
{
    /// <summary>
    /// Gets or sets the buffer size for receiving messages.
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the keep-alive interval for the WebSocket connection.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the WebSocket subprotocol to use.
    /// </summary>
    public string? SubProtocol { get; set; }

    /// <summary>
    /// Gets or sets the allowed origins for WebSocket connections.
    /// Empty list allows all origins.
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to validate the origin header.
    /// </summary>
    public bool ValidateOrigin { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum message size in bytes.
    /// </summary>
    public int MaxMessageSize { get; set; } = 65536; // 64KB

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(30);
}