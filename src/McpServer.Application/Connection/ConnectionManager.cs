using System.Collections.Concurrent;
using McpServer.Domain.Connection;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace McpServer.Application.Connection;

/// <summary>
/// Implementation of connection manager that supports multiple concurrent connections.
/// </summary>
public class ConnectionManager : IConnectionManager, IHostedService, IDisposable
{
    private readonly ILogger<ConnectionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConnectionManagerOptions _options;
    private readonly ConcurrentDictionary<string, IConnection> _connections = new();
    private Timer? _cleanupTimer;
    private readonly SemaphoreSlim _connectionSemaphore;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="options">The configuration options.</param>
    public ConnectionManager(
        ILogger<ConnectionManager> logger,
        ILoggerFactory loggerFactory,
        IOptions<ConnectionManagerOptions> options)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _options = options.Value;
        _connectionSemaphore = new SemaphoreSlim(_options.MaxConnections, _options.MaxConnections);
        
        _logger.LogInformation("Connection manager initialized. Max connections: {MaxConnections}, Multiplexing: {Multiplexing}",
            _options.MaxConnections, _options.EnableMultiplexing ? "Enabled" : "Disabled");
    }

    /// <inheritdoc/>
    public int ActiveConnectionCount => _connections.Count;

    /// <inheritdoc/>
    public IEnumerable<IConnection> ActiveConnections => _connections.Values;

    /// <inheritdoc/>
    public event EventHandler<ConnectionEventArgs>? ConnectionEstablished;

    /// <inheritdoc/>
    public event EventHandler<ConnectionEventArgs>? ConnectionClosed;

    /// <inheritdoc/>
    public async Task<IConnection> AcceptConnectionAsync(ITransport transport, string? connectionId = null, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableMultiplexing && !_connections.IsEmpty)
        {
            throw new InvalidOperationException("Connection multiplexing is disabled and a connection already exists");
        }
        
        // Wait for available connection slot
        if (!await _connectionSemaphore.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException($"Maximum number of connections ({_options.MaxConnections}) reached");
        }
        
        try
        {
            connectionId ??= GenerateConnectionId();
            
            var connectionLogger = _loggerFactory.CreateLogger<Connection>();
            var connection = new Connection(connectionId, transport, connectionLogger);
            
            if (!_connections.TryAdd(connectionId, connection))
            {
                throw new InvalidOperationException($"Connection with ID {connectionId} already exists");
            }
            
            // Set up transport event handlers
            transport.MessageReceived += (sender, args) => OnTransportMessageReceived(connectionId, args);
            transport.Disconnected += (sender, args) => OnTransportDisconnected(connectionId, args);
            
            connection.SetState(ConnectionState.Connected);
            
            _logger.LogInformation("Connection {ConnectionId} accepted. Total connections: {ConnectionCount}",
                connectionId, _connections.Count);
            
            // Raise connection established event
            ConnectionEstablished?.Invoke(this, new ConnectionEventArgs(connection));
            
            return connection;
        }
        catch
        {
            _connectionSemaphore.Release();
            throw;
        }
    }

    /// <inheritdoc/>
    public IConnection? GetConnection(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var connection) ? connection : null;
    }

    /// <inheritdoc/>
    public async Task CloseConnectionAsync(string connectionId, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (!_connections.TryRemove(connectionId, out var connection))
        {
            _logger.LogWarning("Connection {ConnectionId} not found", connectionId);
            return;
        }
        
        try
        {
            await connection.CloseAsync(reason, cancellationToken);
            
            _logger.LogInformation("Connection {ConnectionId} closed. Remaining connections: {ConnectionCount}",
                connectionId, _connections.Count);
            
            // Raise connection closed event
            ConnectionClosed?.Invoke(this, new ConnectionEventArgs(connection, reason));
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CloseAllConnectionsAsync(string? reason = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Closing all {ConnectionCount} connections. Reason: {Reason}",
            _connections.Count, reason ?? "None");
        
        var tasks = _connections.Keys.Select(id => CloseConnectionAsync(id, reason, cancellationToken));
        await Task.WhenAll(tasks);
        
        _connections.Clear();
    }

    /// <inheritdoc/>
    public async Task BroadcastAsync(object message, string? excludeConnectionId = null, CancellationToken cancellationToken = default)
    {
        var connections = _connections.Values
            .Where(c => c.ConnectionId != excludeConnectionId && c.State == ConnectionState.Ready)
            .ToList();
        
        if (connections.Count == 0)
        {
            _logger.LogDebug("No connections available for broadcast");
            return;
        }
        
        _logger.LogDebug("Broadcasting message to {ConnectionCount} connections", connections.Count);
        
        var tasks = connections.Select(c => SendSafeAsync(c, message, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.AutoCleanupIdleConnections)
        {
            _cleanupTimer = new Timer(
                CleanupIdleConnections,
                null,
                _options.CleanupInterval,
                _options.CleanupInterval);
            
            _logger.LogInformation("Idle connection cleanup enabled. Interval: {CleanupInterval}",
                _options.CleanupInterval);
        }
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping connection manager");
        
        _cleanupTimer?.Change(Timeout.Infinite, 0);
        
        await CloseAllConnectionsAsync("Server shutdown", cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        GC.SuppressFinalize(this);
        
        _cleanupTimer?.Dispose();
        _connectionSemaphore.Dispose();
        
        // Close all connections synchronously
        var task = CloseAllConnectionsAsync("Disposing");
        task.Wait(TimeSpan.FromSeconds(30));
    }

    private static string GenerateConnectionId()
    {
        return $"conn_{Guid.NewGuid():N}";
    }

    private void OnTransportMessageReceived(string connectionId, MessageReceivedEventArgs args)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.UpdateActivity();
        }
    }

    private async void OnTransportDisconnected(string connectionId, DisconnectedEventArgs args)
    {
        _logger.LogInformation("Transport disconnected for connection {ConnectionId}. Reason: {Reason}",
            connectionId, args.Reason ?? "Unknown");
        
        try
        {
            await CloseConnectionAsync(connectionId, args.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling transport disconnection for connection {ConnectionId}", connectionId);
        }
    }

    private async Task SendSafeAsync(IConnection connection, object message, CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to connection {ConnectionId}", connection.ConnectionId);
        }
    }

    private async void CleanupIdleConnections(object? state)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var idleConnections = _connections.Values
                .Where(c => now - c.LastActivityAt > _options.IdleTimeout)
                .Select(c => c.ConnectionId)
                .ToList();
            
            if (idleConnections.Count > 0)
            {
                _logger.LogInformation("Cleaning up {Count} idle connections", idleConnections.Count);
                
                foreach (var connectionId in idleConnections)
                {
                    await CloseConnectionAsync(connectionId, "Idle timeout");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during idle connection cleanup");
        }
    }
}