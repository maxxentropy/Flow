namespace McpServer.Application.Services;

/// <summary>
/// Progress and cancellation notification operations.
/// </summary>
public interface IProgressNotificationService
{
    /// <summary>
    /// Sends a progress notification.
    /// </summary>
    /// <param name="progressToken">The progress token.</param>
    /// <param name="progress">The current progress value.</param>
    /// <param name="total">The total progress value.</param>
    /// <param name="message">Optional progress message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyProgressAsync(string progressToken, double progress, double? total = null, string? message = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a cancellation notification.
    /// </summary>
    /// <param name="requestId">The ID of the cancelled request.</param>
    /// <param name="reason">Optional reason for cancellation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyCancelledAsync(string requestId, string? reason = null, CancellationToken cancellationToken = default);
}