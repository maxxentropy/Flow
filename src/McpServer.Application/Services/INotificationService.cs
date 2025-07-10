using McpServer.Domain.Protocol.Messages;

namespace McpServer.Application.Services;

/// <summary>
/// Service for sending notifications to connected clients.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification to all connected clients.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notifies clients that resources have been updated.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyResourcesUpdatedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notifies clients that a specific resource has been updated.
    /// </summary>
    /// <param name="uri">The URI of the updated resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyResourceUpdatedAsync(string uri, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notifies clients that tools have been updated.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyToolsUpdatedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notifies clients that prompts have been updated.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyPromptsUpdatedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a progress notification.
    /// </summary>
    /// <param name="progressToken">The progress token.</param>
    /// <param name="progress">The progress percentage (0-100).</param>
    /// <param name="total">The total units of work.</param>
    /// <param name="message">Optional progress message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyProgressAsync(string progressToken, double progress, double? total = null, string? message = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notifies that an operation was cancelled.
    /// </summary>
    /// <param name="requestId">The request ID that was cancelled.</param>
    /// <param name="reason">The cancellation reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyCancelledAsync(string requestId, string? reason = null, CancellationToken cancellationToken = default);
}