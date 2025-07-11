namespace McpServer.Application.Services;

/// <summary>
/// Tool and prompt registry notification operations.
/// </summary>
public interface IRegistryNotificationService
{
    /// <summary>
    /// Notifies that the list of tools has been updated.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyToolsUpdatedAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notifies that the list of prompts has been updated.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyPromptsUpdatedAsync(CancellationToken cancellationToken = default);
}