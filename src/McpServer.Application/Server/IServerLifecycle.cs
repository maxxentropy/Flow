using McpServer.Application.Services;
using McpServer.Domain.Connection;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using McpServer.Domain.Tools;
using McpServer.Domain.Resources;
using McpServer.Domain.Prompts;

namespace McpServer.Application.Server;

/// <summary>
/// Defines server lifecycle operations.
/// </summary>
public interface IServerLifecycle
{
    /// <summary>
    /// Starts the server with the specified transport.
    /// </summary>
    /// <param name="transport">The transport to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the start operation.</returns>
    Task StartAsync(ITransport transport, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops the server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the stop operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines connection acceptance operations.
/// </summary>
public interface IConnectionAcceptor
{
    /// <summary>
    /// Gets the connection manager.
    /// </summary>
    IConnectionManager ConnectionManager { get; }
    
    /// <summary>
    /// Accepts a new connection from a transport.
    /// </summary>
    /// <param name="transport">The transport for the connection.</param>
    /// <param name="connectionId">Optional connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created connection.</returns>
    Task<IConnection> AcceptConnectionAsync(ITransport transport, string? connectionId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines registry management operations.
/// </summary>
public interface IRegistryManager : IToolRegistry, IResourceRegistry, IPromptRegistry
{
    // Combines all registry interfaces
}