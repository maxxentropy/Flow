using McpServer.Domain.Protocol.Messages;

namespace McpServer.Application.Services;

/// <summary>
/// Generic notification sending operations.
/// </summary>
public interface INotificationSender
{
    /// <summary>
    /// Sends a notification.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
}