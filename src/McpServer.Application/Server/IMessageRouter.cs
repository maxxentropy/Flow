using McpServer.Application.Middleware;

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

    /// <summary>
    /// Routes a message to the appropriate handler with rate limiting context.
    /// </summary>
    /// <param name="message">The JSON-RPC message.</param>
    /// <param name="rateLimitContext">The rate limit context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response message, or null for notifications.</returns>
    Task<object?> RouteMessageAsync(string message, RateLimitContext? rateLimitContext, CancellationToken cancellationToken = default);
}