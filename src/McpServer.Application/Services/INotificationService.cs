namespace McpServer.Application.Services;

/// <summary>
/// Service for sending notifications to connected clients, composed of segregated interfaces.
/// </summary>
public interface INotificationService : INotificationSender, IResourceNotificationService, IRegistryNotificationService, IProgressNotificationService
{
    // Composite interface - all methods are inherited from segregated interfaces
}