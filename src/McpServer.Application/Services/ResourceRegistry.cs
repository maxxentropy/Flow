using System.Collections.Concurrent;
using McpServer.Domain.Resources;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Standalone implementation of resource registry.
/// </summary>
public class ResourceRegistry : IResourceRegistry
{
    private readonly ILogger<ResourceRegistry> _logger;
    private readonly ConcurrentBag<IResourceProvider> _resourceProviders = new();
    private readonly SemaphoreSlim _registrationLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceRegistry"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ResourceRegistry(ILogger<ResourceRegistry> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void RegisterResourceProvider(IResourceProvider provider)
    {
        _registrationLock.Wait();
        try
        {
            _resourceProviders.Add(provider);
            _logger.LogInformation("Registered resource provider: {ProviderType}", provider.GetType().Name);
            
            // Raise an event that MultiplexingMcpServer can subscribe to
            ResourceProviderRegistered?.Invoke(this, new ResourceProviderEventArgs(provider));
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IResourceProvider> GetResourceProviders() => _resourceProviders.ToArray();

    /// <summary>
    /// Event raised when a resource provider is registered.
    /// </summary>
    public event EventHandler<ResourceProviderEventArgs>? ResourceProviderRegistered;
}

/// <summary>
/// Event args for resource provider registration.
/// </summary>
public class ResourceProviderEventArgs : EventArgs
{
    /// <summary>
    /// Gets the registered provider.
    /// </summary>
    public IResourceProvider Provider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceProviderEventArgs"/> class.
    /// </summary>
    /// <param name="provider">The provider that was registered.</param>
    public ResourceProviderEventArgs(IResourceProvider provider)
    {
        Provider = provider;
    }
}