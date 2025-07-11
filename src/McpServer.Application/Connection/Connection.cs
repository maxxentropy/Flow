using System.Collections.Concurrent;
using McpServer.Domain.Connection;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Connection;

/// <summary>
/// Implementation of a single connection.
/// </summary>
public class Connection : IConnection
{
    private readonly ILogger<Connection> _logger;
    private readonly ConcurrentDictionary<string, object> _metadata = new();
    private ConnectionState _state = ConnectionState.Connecting;
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="Connection"/> class.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="transport">The transport.</param>
    /// <param name="logger">The logger.</param>
    public Connection(string connectionId, ITransport transport, ILogger<Connection> logger)
    {
        ConnectionId = connectionId;
        Transport = transport;
        _logger = logger;
        ConnectedAt = DateTimeOffset.UtcNow;
        LastActivityAt = ConnectedAt;
        
        _logger.LogDebug("Connection {ConnectionId} created", connectionId);
    }

    /// <inheritdoc/>
    public string ConnectionId { get; }

    /// <inheritdoc/>
    public ITransport Transport { get; }

    /// <inheritdoc/>
    public ConnectionState State => _state;

    /// <inheritdoc/>
    public bool IsInitialized => _isInitialized;

    /// <inheritdoc/>
    public DateTimeOffset ConnectedAt { get; }

    /// <inheritdoc/>
    public DateTimeOffset LastActivityAt { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    /// <inheritdoc/>
    public void SetMetadata(string key, object value)
    {
        _metadata[key] = value;
        _logger.LogTrace("Connection {ConnectionId} metadata set: {Key} = {Value}", ConnectionId, key, value);
    }

    /// <inheritdoc/>
    public void MarkInitialized()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("Connection {ConnectionId} is already initialized", ConnectionId);
            return;
        }
        
        _isInitialized = true;
        _state = ConnectionState.Ready;
        UpdateActivity();
        
        _logger.LogInformation("Connection {ConnectionId} initialized", ConnectionId);
    }

    /// <inheritdoc/>
    public void UpdateActivity()
    {
        LastActivityAt = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc/>
    public async Task SendAsync(object message, CancellationToken cancellationToken = default)
    {
        if (_state == ConnectionState.Closing || _state == ConnectionState.Closed)
        {
            throw new InvalidOperationException($"Cannot send message on {_state} connection");
        }
        
        UpdateActivity();
        
        try
        {
            await Transport.SendMessageAsync(message, cancellationToken);
            _logger.LogTrace("Message sent on connection {ConnectionId}", ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message on connection {ConnectionId}", ConnectionId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task CloseAsync(string? reason = null, CancellationToken cancellationToken = default)
    {
        if (_state == ConnectionState.Closing || _state == ConnectionState.Closed)
        {
            _logger.LogDebug("Connection {ConnectionId} is already {State}", ConnectionId, _state);
            return;
        }
        
        _logger.LogInformation("Closing connection {ConnectionId}. Reason: {Reason}", ConnectionId, reason ?? "None");
        
        _state = ConnectionState.Closing;
        
        try
        {
            await Transport.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping transport for connection {ConnectionId}", ConnectionId);
        }
        finally
        {
            _state = ConnectionState.Closed;
            
            // Dispose transport if it's disposable
            if (Transport is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing transport for connection {ConnectionId}", ConnectionId);
                }
            }
        }
    }
    
    /// <summary>
    /// Sets the connection state.
    /// </summary>
    /// <param name="state">The new state.</param>
    internal void SetState(ConnectionState state)
    {
        var oldState = _state;
        _state = state;
        
        if (state == ConnectionState.Connected)
        {
            UpdateActivity();
        }
        
        _logger.LogDebug("Connection {ConnectionId} state changed from {OldState} to {NewState}", 
            ConnectionId, oldState, state);
    }
}