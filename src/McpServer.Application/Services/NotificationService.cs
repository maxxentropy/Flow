using System.Collections.Concurrent;
using McpServer.Application.Server;
using McpServer.Domain.Connection;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Default implementation of the notification service.
/// </summary>
public class NotificationService : INotificationService, IConnectionAwareNotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentDictionary<string, IConnection> _connections = new();
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
        await SendNotificationInternalAsync(notification, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task NotifyResourceUpdatedAsync(string uri, CancellationToken cancellationToken = default)
    {
        var notification = new ResourceUpdatedNotification
        {
            ResourceParams = new ResourceUpdatedParams { Uri = uri }
        };
        await SendNotificationInternalAsync(notification, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task NotifyToolsUpdatedAsync(CancellationToken cancellationToken = default)
    {
        var notification = new ToolsUpdatedNotification();
        await SendNotificationInternalAsync(notification, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task NotifyPromptsUpdatedAsync(CancellationToken cancellationToken = default)
    {
        var notification = new PromptsUpdatedNotification();
        await SendNotificationInternalAsync(notification, cancellationToken);
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
        await SendNotificationInternalAsync(notification, cancellationToken);
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
        await SendNotificationInternalAsync(notification, cancellationToken);
    }
    
    /// <inheritdoc/>
    public void AddConnection(IConnection connection)
    {
        _connections.TryAdd(connection.ConnectionId, connection);
        _logger.LogDebug("Added connection {ConnectionId} to notification service", connection.ConnectionId);
    }
    
    /// <inheritdoc/>
    public void RemoveConnection(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out _))
        {
            _logger.LogDebug("Removed connection {ConnectionId} from notification service", connectionId);
        }
    }
    
    /// <summary>
    /// Sends a notification to all connections or fallback to single transport.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task SendNotificationInternalAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        // If we have connections, send to all ready connections
        if (!_connections.IsEmpty)
        {
            var tasks = new List<Task>();
            
            foreach (var connection in _connections.Values)
            {
                if (connection.State == ConnectionState.Ready)
                {
                    tasks.Add(SendToConnectionAsync(connection, notification, cancellationToken));
                }
            }
            
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
                return;
            }
        }
        
        // Fallback to single transport
        if (_transport != null && _transport.IsConnected)
        {
            _logger.LogDebug("Sending notification via transport: {Method}", notification.Method);
            
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
        else
        {
            _logger.LogWarning("Cannot send notification {Method}: No transport or connections available", notification.Method);
        }
    }
    
    /// <summary>
    /// Sends a notification to a specific connection.
    /// </summary>
    private async Task SendToConnectionAsync(IConnection connection, object notification, CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(notification, cancellationToken);
            _logger.LogDebug("Sent notification to connection {ConnectionId}", connection.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to connection {ConnectionId}", connection.ConnectionId);
        }
    }
}