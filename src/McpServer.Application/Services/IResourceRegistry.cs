using McpServer.Domain.Resources;

namespace McpServer.Application.Services;

/// <summary>
/// Service for managing resource provider registrations.
/// </summary>
public interface IResourceRegistry
{
    /// <summary>
    /// Gets all registered resource providers.
    /// </summary>
    /// <returns>A collection of resource providers.</returns>
    IReadOnlyCollection<IResourceProvider> GetResourceProviders();
    
    /// <summary>
    /// Registers a resource provider.
    /// </summary>
    /// <param name="provider">The resource provider to register.</param>
    void RegisterResourceProvider(IResourceProvider provider);
}