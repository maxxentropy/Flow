using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Default implementation of the notification service.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private ITransport? _transport;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class with a transport.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="transport">The transport to use for sending notifications.</param>
    public NotificationService(ILogger<NotificationService> logger, ITransport transport)
    {
        _logger = logger;
        _transport = transport;
    }
    
    /// <summary>
    /// Sets the transport for sending notifications.
    /// </summary>
    /// <param name="transport">The transport.</param>
    public void SetTransport(ITransport transport)
    {
        _transport = transport;
    }
    
    /// <inheritdoc/>
    public async Task SendNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        if (_transport == null)
        {
            _logger.LogWarning("Cannot send notification {Method}: No transport available", notification.Method);
            return;
        }
        
        _logger.LogDebug("Sending notification: {Method}", notification.Method);
        
        try
        {
            await _transport.SendMessageAsync(notification, cancellationToken);
            _logger.LogDebug("Notification sent successfully: {Method}", notification.Method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification {Method}", notification.Method);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task NotifyResourcesUpdatedAsync(CancellationToken cancellationToken = default)
    {
        var notification = new ResourcesUpdatedNotification();
        await SendNotificationAsync(notification, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task NotifyResourceUpdatedAsync(string uri, CancellationToken cancellationToken = default)
    {
        var notification = new ResourceUpdatedNotification
        {
            ResourceParams = new ResourceUpdatedParams { Uri = uri }
        };
        await SendNotificationAsync(notification, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task NotifyToolsUpdatedAsync(CancellationToken cancellationToken = default)
    {
        var notification = new ToolsUpdatedNotification();
        await SendNotificationAsync(notification, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task NotifyPromptsUpdatedAsync(CancellationToken cancellationToken = default)
    {
        var notification = new PromptsUpdatedNotification();
        await SendNotificationAsync(notification, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task NotifyProgressAsync(string progressToken, double progress, double? total = null, string? message = null, CancellationToken cancellationToken = default)
    {
        var notification = new ProgressNotification
        {
            ProgressParams = new ProgressNotificationParams
            {
                ProgressToken = progressToken,
                Progress = progress,
                Total = total,
                Message = message
            }
        };
        await SendNotificationAsync(notification, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task NotifyCancelledAsync(string requestId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var notification = new CancelledNotification
        {
            CancelledParams = new CancelledNotificationParams
            {
                RequestId = requestId,
                Reason = reason
            }
        };
        await SendNotificationAsync(notification, cancellationToken);
    }
}