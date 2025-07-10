using McpServer.Domain.Prompts;

namespace McpServer.Application.Services;

/// <summary>
/// Service for managing prompt provider registrations.
/// </summary>
public interface IPromptRegistry
{
    /// <summary>
    /// Gets all registered prompt providers.
    /// </summary>
    /// <returns>A collection of prompt providers.</returns>
    IReadOnlyCollection<IPromptProvider> GetPromptProviders();
    
    /// <summary>
    /// Registers a prompt provider.
    /// </summary>
    /// <param name="provider">The prompt provider to register.</param>
    void RegisterPromptProvider(IPromptProvider provider);
}