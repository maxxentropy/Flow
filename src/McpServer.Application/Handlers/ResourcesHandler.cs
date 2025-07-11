using System.Diagnostics;
using System.Linq;
using McpServer.Application.Messages;
using McpServer.Application.Server;
using McpServer.Application.Services;
using McpServer.Application.Tracing;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Resources;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles resource-related requests.
/// </summary>
public class ResourcesHandler : IMessageHandler
{
    private readonly ILogger<ResourcesHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IMcpServer? _server;
    private IResourceRegistry? _resourceRegistry;
    private INotificationService? _notificationService;
    private readonly Dictionary<string, HashSet<IResourceObserver>> _subscriptions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourcesHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public ResourcesHandler(ILogger<ResourcesHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public bool CanHandle(Type messageType)
    {
        return messageType == typeof(ResourcesListRequest) || 
               messageType == typeof(ResourcesReadRequest) ||
               messageType == typeof(ResourcesSubscribeRequest) ||
               messageType == typeof(ResourcesUnsubscribeRequest);
    }

    /// <inheritdoc/>
    public async Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        using var activity = TracingExtensions.StartHandlerActivity("ResourcesHandler", message.GetType().Name);
        
        try
        {
            switch (message)
            {
                case JsonRpcRequest listRequest when listRequest.Method == "resources/list":
                    activity?.SetTag("resources.operation", "list");
                    return await HandleListResourcesAsync(cancellationToken).ConfigureAwait(false);
                    
                case JsonRpcRequest<ResourcesReadRequest> readRequest:
                    activity?.SetTag("resources.operation", "read");
                    if (readRequest.Params == null)
                    {
                        throw new ProtocolException("Resource read request parameters cannot be null");
                    }
                    activity?.SetTag("resources.uri", readRequest.Params.Uri);
                    return await HandleReadResourceAsync(readRequest.Params, cancellationToken).ConfigureAwait(false);
                    
                case JsonRpcRequest<ResourcesSubscribeRequest> subscribeRequest:
                    activity?.SetTag("resources.operation", "subscribe");
                    if (subscribeRequest.Params == null)
                    {
                        throw new ProtocolException("Resource subscribe request parameters cannot be null");
                    }
                    activity?.SetTag("resources.uri", subscribeRequest.Params.Uri);
                    return await HandleSubscribeResourceAsync(subscribeRequest.Params, cancellationToken).ConfigureAwait(false);
                    
                case JsonRpcRequest<ResourcesUnsubscribeRequest> unsubscribeRequest:
                    activity?.SetTag("resources.operation", "unsubscribe");
                    if (unsubscribeRequest.Params == null)
                    {
                        throw new ProtocolException("Resource unsubscribe request parameters cannot be null");
                    }
                    activity?.SetTag("resources.uri", unsubscribeRequest.Params.Uri);
                    return await HandleUnsubscribeResourceAsync(unsubscribeRequest.Params, cancellationToken).ConfigureAwait(false);
                    
                default:
                    throw new ArgumentException("Invalid message type", nameof(message));
            }
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }
    }

    private async Task<object> HandleListResourcesAsync(CancellationToken cancellationToken)
    {
        EnsureInitialized();

        var allResources = new List<Resource>();
        var providers = _resourceRegistry!.GetResourceProviders();

        foreach (var provider in providers)
        {
            try
            {
                var resources = await provider.ListResourcesAsync(cancellationToken).ConfigureAwait(false);
                allResources.AddRange(resources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list resources from provider {ProviderType}", provider.GetType().Name);
            }
        }

        _logger.LogDebug("Listed {ResourceCount} resources from {ProviderCount} providers", 
            allResources.Count, providers.Count);
        
        // Add resource count to the current activity
        Activity.Current?.SetTag("resources.count", allResources.Count);
        Activity.Current?.SetTag("resources.providers", providers.Count);

        return new { resources = allResources };
    }

    private async Task<object> HandleReadResourceAsync(ResourcesReadRequest request, CancellationToken cancellationToken)
    {
        EnsureInitialized();

        _logger.LogInformation("Reading resource: {Uri}", request.Uri);

        var providers = _resourceRegistry!.GetResourceProviders();
        
        foreach (var provider in providers)
        {
            try
            {
                var content = await provider.ReadResourceAsync(request.Uri, cancellationToken).ConfigureAwait(false);
                if (content != null)
                {
                    _logger.LogInformation("Resource {Uri} read successfully", request.Uri);
                    
                    // Add success tags to the current activity
                    Activity.Current?.SetTag("resources.found", true);
                    Activity.Current?.SetTag("resources.provider", provider.GetType().Name);
                    
                    return new { contents = new[] { content } };
                }
            }
            catch (ResourceException)
            {
                // This provider doesn't handle this resource, try next
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read resource {Uri} from provider {ProviderType}", 
                    request.Uri, provider.GetType().Name);
            }
        }

        throw new ResourceException(request.Uri, "Resource not found");
    }

    private async Task<object> HandleSubscribeResourceAsync(ResourcesSubscribeRequest request, CancellationToken cancellationToken)
    {
        EnsureInitialized();

        _logger.LogInformation("Subscribing to resource: {Uri}", request.Uri);

        // Get notification service if not already available
        _notificationService ??= _serviceProvider.GetRequiredService<INotificationService>();

        // Create observer for this subscription
        var observer = new ResourceObserver(request.Uri, _logger, _notificationService);
        
        // Add to subscriptions
        lock (_subscriptions)
        {
            if (!_subscriptions.TryGetValue(request.Uri, out var observers))
            {
                observers = new HashSet<IResourceObserver>();
                _subscriptions[request.Uri] = observers;
            }
            observers.Add(observer);
        }

        // Subscribe with providers
        var providers = _resourceRegistry!.GetResourceProviders();
        var subscribed = false;

        foreach (var provider in providers)
        {
            try
            {
                await provider.SubscribeToResourceAsync(request.Uri, observer, cancellationToken).ConfigureAwait(false);
                subscribed = true;
                break;
            }
            catch (ResourceException)
            {
                // This provider doesn't handle this resource, try next
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to resource {Uri} with provider {ProviderType}", 
                    request.Uri, provider.GetType().Name);
            }
        }

        if (!subscribed)
        {
            // Remove from subscriptions if no provider could handle it
            lock (_subscriptions)
            {
                if (_subscriptions.TryGetValue(request.Uri, out var observers))
                {
                    observers.Remove(observer);
                    if (observers.Count == 0)
                    {
                        _subscriptions.Remove(request.Uri);
                    }
                }
            }
            
            throw new ResourceException(request.Uri, "Resource not found or subscription not supported");
        }

        _logger.LogInformation("Subscribed to resource {Uri} successfully", request.Uri);
        
        // Add subscription success tag to the current activity
        Activity.Current?.SetTag("resources.subscribed", true);
        
        return new { success = true };
    }

    private async Task<object> HandleUnsubscribeResourceAsync(ResourcesUnsubscribeRequest request, CancellationToken cancellationToken)
    {
        EnsureInitialized();

        _logger.LogInformation("Unsubscribing from resource: {Uri}", request.Uri);

        // Get observers for this URI
        IResourceObserver[] observersToRemove;
        lock (_subscriptions)
        {
            if (!_subscriptions.TryGetValue(request.Uri, out var observers) || observers.Count == 0)
            {
                _logger.LogWarning("No subscriptions found for resource {Uri}", request.Uri);
                return new { success = true };
            }
            
            observersToRemove = observers.ToArray();
            _subscriptions.Remove(request.Uri);
        }

        // Unsubscribe with providers
        var providers = _resourceRegistry!.GetResourceProviders();
        
        foreach (var observer in observersToRemove)
        {
            foreach (var provider in providers)
            {
                try
                {
                    await provider.UnsubscribeFromResourceAsync(request.Uri, observer, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unsubscribe from resource {Uri} with provider {ProviderType}", 
                        request.Uri, provider.GetType().Name);
                }
            }
        }

        _logger.LogInformation("Unsubscribed from resource {Uri} successfully", request.Uri);
        
        // Add unsubscription success tag to the current activity
        Activity.Current?.SetTag("resources.unsubscribed", true);
        Activity.Current?.SetTag("resources.observers_removed", observersToRemove.Length);
        
        return new { success = true };
    }

    private void EnsureInitialized()
    {
        // Lazily get the server instance
        _server ??= _serviceProvider.GetRequiredService<IMcpServer>();
        _resourceRegistry ??= _serviceProvider.GetRequiredService<IResourceRegistry>();
        
        // Note: Connection-level initialization is handled by ConnectionAwareMessageRouter
        // No need to check server-level initialization as it's connection-specific
    }

    private class ResourceObserver : IResourceObserver
    {
        private readonly string _uri;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;
        private readonly string? _connectionId;

        public ResourceObserver(string uri, ILogger logger, INotificationService notificationService, string? connectionId = null)
        {
            _uri = uri;
            _logger = logger;
            _notificationService = notificationService;
            _connectionId = connectionId;
        }

        public async Task OnResourceUpdatedAsync(string uri, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Resource updated: {Uri}", uri);
            
            try
            {
                // Send resource update notification
                await _notificationService.NotifyResourceUpdatedAsync(uri, cancellationToken);
                
                _logger.LogDebug("Sent resource update notification for {Uri}", uri);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send resource update notification for {Uri}", uri);
            }
        }
    }
}