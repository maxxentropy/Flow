namespace McpServer.Application.Server;

/// <summary>
/// Routes incoming messages to appropriate handlers.
/// </summary>
public interface IMessageRouter
{
    /// <summary>
    /// Routes an incoming message to the appropriate handler.
    /// </summary>
    /// <param name="message">The incoming message as a JSON string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response message, if any.</returns>
    Task<object?> RouteMessageAsync(string message, CancellationToken cancellationToken = default);
}