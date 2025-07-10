namespace McpServer.Domain.Services;

/// <summary>
/// Handles MCP protocol messages.
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Handles an incoming message and returns a response if applicable.
    /// </summary>
    /// <param name="message">The incoming message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response message, if any.</returns>
    Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether this handler can handle the specified message type.
    /// </summary>
    /// <param name="messageType">The type of message.</param>
    /// <returns>True if the handler can handle the message type; otherwise, false.</returns>
    bool CanHandle(Type messageType);
}