using System.Collections.Concurrent;
using McpServer.Domain.Transport;

namespace McpServer.Domain.Connection;

/// <summary>
/// Manages multiple concurrent connections to the MCP server.
/// </summary>
public interface IConnectionManager
{
    /// <summary>
    /// Gets the total number of active connections.
    /// </summary>
    int ActiveConnectionCount { get; }
    
    /// <summary>
    /// Gets all active connections.
    /// </summary>
    IEnumerable<IConnection> ActiveConnections { get; }
    
    /// <summary>
    /// Accepts a new connection from a transport.
    /// </summary>
    /// <param name="transport">The transport for the connection.</param>
    /// <param name="connectionId">Optional connection ID. If not provided, one will be generated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created connection.</returns>
    Task<IConnection> AcceptConnectionAsync(ITransport transport, string? connectionId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a connection by its ID.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <returns>The connection if found; otherwise, null.</returns>
    IConnection? GetConnection(string connectionId);
    
    /// <summary>
    /// Closes a specific connection.
    /// </summary>
    /// <param name="connectionId">The connection ID to close.</param>
    /// <param name="reason">The reason for closing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CloseConnectionAsync(string connectionId, string? reason = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Closes all active connections.
    /// </summary>
    /// <param name="reason">The reason for closing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CloseAllConnectionsAsync(string? reason = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Broadcasts a message to all active connections.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="excludeConnectionId">Optional connection ID to exclude from broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastAsync(object message, string? excludeConnectionId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Event raised when a new connection is established.
    /// </summary>
    event EventHandler<ConnectionEventArgs>? ConnectionEstablished;
    
    /// <summary>
    /// Event raised when a connection is closed.
    /// </summary>
    event EventHandler<ConnectionEventArgs>? ConnectionClosed;
}

/// <summary>
/// Represents a single connection to the MCP server.
/// </summary>
public interface IConnection
{
    /// <summary>
    /// Gets the unique connection ID.
    /// </summary>
    string ConnectionId { get; }
    
    /// <summary>
    /// Gets the transport for this connection.
    /// </summary>
    ITransport Transport { get; }
    
    /// <summary>
    /// Gets the connection state.
    /// </summary>
    ConnectionState State { get; }
    
    /// <summary>
    /// Gets whether the connection is initialized (handshake completed).
    /// </summary>
    bool IsInitialized { get; }
    
    /// <summary>
    /// Gets the time when the connection was established.
    /// </summary>
    DateTimeOffset ConnectedAt { get; }
    
    /// <summary>
    /// Gets the last activity time on this connection.
    /// </summary>
    DateTimeOffset LastActivityAt { get; }
    
    /// <summary>
    /// Gets connection metadata.
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
    
    /// <summary>
    /// Sets a metadata value for this connection.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    void SetMetadata(string key, object value);
    
    /// <summary>
    /// Marks the connection as initialized.
    /// </summary>
    void MarkInitialized();
    
    /// <summary>
    /// Updates the last activity time.
    /// </summary>
    void UpdateActivity();
    
    /// <summary>
    /// Sends a message through this connection.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(object message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Closes this connection.
    /// </summary>
    /// <param name="reason">The reason for closing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CloseAsync(string? reason = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Connection state enumeration.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Connection is being established.
    /// </summary>
    Connecting,
    
    /// <summary>
    /// Connection is established but not initialized.
    /// </summary>
    Connected,
    
    /// <summary>
    /// Connection is initialized and ready.
    /// </summary>
    Ready,
    
    /// <summary>
    /// Connection is closing.
    /// </summary>
    Closing,
    
    /// <summary>
    /// Connection is closed.
    /// </summary>
    Closed
}

/// <summary>
/// Event arguments for connection events.
/// </summary>
public class ConnectionEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionEventArgs"/> class.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="reason">Optional reason for the event.</param>
    public ConnectionEventArgs(IConnection connection, string? reason = null)
    {
        Connection = connection;
        Reason = reason;
    }
    
    /// <summary>
    /// Gets the connection.
    /// </summary>
    public IConnection Connection { get; }
    
    /// <summary>
    /// Gets the optional reason.
    /// </summary>
    public string? Reason { get; }
}

/// <summary>
/// Configuration for connection management.
/// </summary>
public class ConnectionManagerOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent connections.
    /// </summary>
    public int MaxConnections { get; set; } = 100;
    
    /// <summary>
    /// Gets or sets the connection idle timeout.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Gets or sets whether to enable connection multiplexing.
    /// </summary>
    public bool EnableMultiplexing { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to automatically clean up idle connections.
    /// </summary>
    public bool AutoCleanupIdleConnections { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the cleanup interval for idle connections.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
}