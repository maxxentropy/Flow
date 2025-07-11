using System.Collections.Concurrent;
using McpServer.Domain.Prompts;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Standalone implementation of prompt registry.
/// </summary>
public class PromptRegistry : IPromptRegistry
{
    private readonly ILogger<PromptRegistry> _logger;
    private readonly ConcurrentBag<IPromptProvider> _promptProviders = new();
    private readonly SemaphoreSlim _registrationLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptRegistry"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PromptRegistry(ILogger<PromptRegistry> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void RegisterPromptProvider(IPromptProvider provider)
    {
        _registrationLock.Wait();
        try
        {
            _promptProviders.Add(provider);
            _logger.LogInformation("Registered prompt provider: {ProviderType}", provider.GetType().Name);
            
            // Raise an event that MultiplexingMcpServer can subscribe to
            PromptProviderRegistered?.Invoke(this, new PromptProviderEventArgs(provider));
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IPromptProvider> GetPromptProviders() => _promptProviders.ToArray();

    /// <summary>
    /// Event raised when a prompt provider is registered.
    /// </summary>
    public event EventHandler<PromptProviderEventArgs>? PromptProviderRegistered;
}

/// <summary>
/// Event args for prompt provider registration.
/// </summary>
public class PromptProviderEventArgs : EventArgs
{
    /// <summary>
    /// Gets the registered provider.
    /// </summary>
    public IPromptProvider Provider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptProviderEventArgs"/> class.
    /// </summary>
    /// <param name="provider">The provider that was registered.</param>
    public PromptProviderEventArgs(IPromptProvider provider)
    {
        Provider = provider;
    }
}