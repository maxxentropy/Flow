using McpServer.Domain.Connection;
using McpServer.Domain.Transport;

namespace McpServer.Application.Server;

/// <summary>
/// Connection management operations for the server.
/// </summary>
public interface IServerConnectionManager
{
    /// <summary>
    /// Gets the connection manager.
    /// </summary>
    IConnectionManager ConnectionManager { get; }
    
    /// <summary>
    /// Accepts a new connection.
    /// </summary>
    /// <param name="transport">The transport for the connection.</param>
    /// <param name="connectionId">Optional connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The established connection.</returns>
    Task<IConnection> AcceptConnectionAsync(ITransport transport, string? connectionId = null, CancellationToken cancellationToken = default);
}