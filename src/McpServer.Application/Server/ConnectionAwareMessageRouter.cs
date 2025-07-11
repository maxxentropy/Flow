using System.Text.Json;
using McpServer.Application.Messages;
using McpServer.Application.Middleware;
using McpServer.Domain.Connection;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.RateLimiting;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Server;

/// <summary>
/// Message router that is aware of connections and routes messages to the appropriate handlers.
/// </summary>
public class ConnectionAwareMessageRouter : IConnectionAwareMessageRouter
{
    private readonly ILogger<ConnectionAwareMessageRouter> _logger;
    private readonly IMessageRouter _innerRouter;
    private readonly IConnectionManager _connectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionAwareMessageRouter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="innerRouter">The inner message router.</param>
    /// <param name="connectionManager">The connection manager.</param>
    public ConnectionAwareMessageRouter(
        ILogger<ConnectionAwareMessageRouter> logger,
        IMessageRouter innerRouter,
        IConnectionManager connectionManager)
    {
        _logger = logger;
        _innerRouter = innerRouter;
        _connectionManager = connectionManager;
    }

    /// <inheritdoc/>
    public async Task<JsonRpcResponse?> RouteMessageAsync(
        string connectionId,
        string message,
        RateLimitContext? rateLimitContext = null,
        CancellationToken cancellationToken = default)
    {
        var connection = _connectionManager.GetConnection(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Connection {ConnectionId} not found", connectionId);
            throw new ConnectionException($"Connection {connectionId} not found");
        }

        // Update connection activity
        connection.UpdateActivity();

        // Parse message to check if it's an initialize request
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("method", out var methodElement))
            {
                var method = methodElement.GetString();
                
                // Handle initialize method specially for connection state
                if (method == "initialize")
                {
                    if (connection.IsInitialized)
                    {
                        _logger.LogWarning("Connection {ConnectionId} attempted to initialize twice", connectionId);
                        
                        // Return error response if message has an ID
                        if (root.TryGetProperty("id", out var idElement))
                        {
                            return new JsonRpcResponse
                            {
                                Jsonrpc = "2.0",
                                Id = idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : idElement.GetRawText(),
                                Error = new JsonRpcError
                                {
                                    Code = JsonRpcErrorCodes.InvalidRequest,
                                    Message = "Connection is already initialized"
                                }
                            };
                        }
                        
                        return null;
                    }
                }
                else if (!connection.IsInitialized && method != "cancel")
                {
                    _logger.LogWarning("Connection {ConnectionId} attempted to call {Method} before initialization", 
                        connectionId, method);
                    
                    // Return error response if message has an ID
                    if (root.TryGetProperty("id", out var idElement))
                    {
                        return new JsonRpcResponse
                        {
                            Jsonrpc = "2.0",
                            Id = idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : idElement.GetRawText(),
                            Error = new JsonRpcError
                            {
                                Code = JsonRpcErrorCodes.InvalidRequest,
                                Message = "Connection must be initialized before calling other methods"
                            }
                        };
                    }
                    
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse message for connection {ConnectionId}", connectionId);
        }

        // Add connection context to rate limit context if available
        if (rateLimitContext != null)
        {
            rateLimitContext = rateLimitContext with
            {
                SessionId = connectionId,
                AdditionalData = (rateLimitContext.AdditionalData ?? new Dictionary<string, object>())
                    .Concat(new[] { new KeyValuePair<string, object>("ConnectionId", connectionId) })
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        // Route through inner router
        var response = await _innerRouter.RouteMessageAsync(message, rateLimitContext, cancellationToken);

        // If it was a successful initialize, mark connection as initialized
        if (response is JsonRpcResponse jsonResponse && jsonResponse.Error == null)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("method", out var methodElement) && 
                    methodElement.GetString() == "initialize")
                {
                    connection.MarkInitialized();
                    _logger.LogInformation("Connection {ConnectionId} initialized successfully", connectionId);
                }
            }
            catch
            {
                // Ignore parsing errors here
            }
        }

        return response as JsonRpcResponse;
    }

    /// <inheritdoc/>
    public Task BroadcastNotificationAsync(
        object notification,
        string? excludeConnectionId = null,
        CancellationToken cancellationToken = default)
    {
        return _connectionManager.BroadcastAsync(notification, excludeConnectionId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendNotificationAsync(
        string connectionId,
        object notification,
        CancellationToken cancellationToken = default)
    {
        var connection = _connectionManager.GetConnection(connectionId);
        if (connection == null)
        {
            _logger.LogWarning("Cannot send notification to non-existent connection {ConnectionId}", connectionId);
            return;
        }

        await connection.SendAsync(notification, cancellationToken);
    }
}

/// <summary>
/// Message router interface that is aware of connections.
/// </summary>
public interface IConnectionAwareMessageRouter
{
    /// <summary>
    /// Routes a message from a specific connection.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="message">The message to route.</param>
    /// <param name="rateLimitContext">Optional rate limit context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response, if any.</returns>
    Task<JsonRpcResponse?> RouteMessageAsync(
        string connectionId,
        string message,
        RateLimitContext? rateLimitContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a notification to all connections.
    /// </summary>
    /// <param name="notification">The notification to broadcast.</param>
    /// <param name="excludeConnectionId">Optional connection ID to exclude.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastNotificationAsync(
        object notification,
        string? excludeConnectionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to a specific connection.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="notification">The notification to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendNotificationAsync(
        string connectionId,
        object notification,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when a connection-related error occurs.
/// </summary>
public class ConnectionException : McpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ConnectionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}