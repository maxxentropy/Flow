namespace McpServer.Domain.Transport;

/// <summary>
/// Represents a transport mechanism for MCP communication.
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Event raised when a message is received.
    /// </summary>
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Event raised when the transport is disconnected.
    /// </summary>
    event EventHandler<DisconnectedEventArgs>? Disconnected;

    /// <summary>
    /// Gets whether the transport is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Starts the transport.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the start operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the transport.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the stop operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message through the transport.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the send operation.</returns>
    Task SendMessageAsync(object message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event arguments for message received events.
/// </summary>
public class MessageReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageReceivedEventArgs"/> class.
    /// </summary>
    /// <param name="message">The received message.</param>
    public MessageReceivedEventArgs(string message)
    {
        Message = message;
    }

    /// <summary>
    /// Gets the received message.
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// Event arguments for disconnected events.
/// </summary>
public class DisconnectedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisconnectedEventArgs"/> class.
    /// </summary>
    /// <param name="reason">The reason for disconnection.</param>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    public DisconnectedEventArgs(string? reason = null, Exception? exception = null)
    {
        Reason = reason;
        Exception = exception;
    }

    /// <summary>
    /// Gets the reason for disconnection.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Gets the exception that caused the disconnection, if any.
    /// </summary>
    public Exception? Exception { get; }
}