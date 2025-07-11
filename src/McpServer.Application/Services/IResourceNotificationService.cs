namespace McpServer.Application.Services;

/// <summary>
/// Resource-specific notification operations.
/// </summary>
public interface IResourceNotificationService
{
    /// <summary>
    /// Notifies that the list of resources has been updated.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyResourcesUpdatedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notifies that a specific resource has been updated.
    /// </summary>
    /// <param name="uri">The URI of the updated resource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyResourceUpdatedAsync(string uri, CancellationToken cancellationToken = default);
}