using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Implementation of the completion service.
/// </summary>
public class CompletionService : ICompletionService
{
    private readonly ILogger<CompletionService> _logger;
    private readonly IResourceRegistry _resourceRegistry;
    private readonly IPromptRegistry _promptRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompletionService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="resourceRegistry">The resource registry.</param>
    /// <param name="promptRegistry">The prompt registry.</param>
    public CompletionService(
        ILogger<CompletionService> logger,
        IResourceRegistry resourceRegistry,
        IPromptRegistry promptRegistry)
    {
        _logger = logger;
        _resourceRegistry = resourceRegistry;
        _promptRegistry = promptRegistry;
    }

    /// <inheritdoc/>
    public async Task<CompletionCompleteResponse> GetCompletionAsync(
        CompletionReference reference,
        CompletionArgument argument,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting completion for reference type {Type}, name {Name}, argument {ArgumentName}",
            reference.Type, reference.Name, argument.Name);

        var completions = reference.Type switch
        {
            "ref/prompt" => await GetPromptCompletionAsync(reference, argument, cancellationToken),
            "ref/resource" => await GetResourceCompletionAsync(reference, argument, cancellationToken),
            _ => Array.Empty<CompletionItem>()
        };

        return new CompletionCompleteResponse
        {
            Completion = completions,
            HasMore = false,
            Total = completions.Length
        };
    }

    private async Task<CompletionItem[]> GetPromptCompletionAsync(
        CompletionReference reference,
        CompletionArgument argument,
        CancellationToken cancellationToken)
    {
        try
        {
            var promptProviders = _promptRegistry.GetPromptProviders();
            var allCompletions = new List<CompletionItem>();

            foreach (var provider in promptProviders)
            {
                try
                {
                    var prompts = await provider.ListPromptsAsync(cancellationToken);
                    var matchingPrompt = prompts.FirstOrDefault(p => p.Name == reference.Name);
                    
                    if (matchingPrompt?.Arguments != null)
                    {
                        foreach (var promptArg in matchingPrompt.Arguments)
                        {
                            var suggestions = GetArgumentCompletions(promptArg, argument.Value);
                            allCompletions.AddRange(suggestions);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get completions from prompt provider {ProviderType}",
                        provider.GetType().Name);
                }
            }

            return allCompletions.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prompt completions");
            return Array.Empty<CompletionItem>();
        }
    }

    private async Task<CompletionItem[]> GetResourceCompletionAsync(
        CompletionReference reference,
        CompletionArgument argument,
        CancellationToken cancellationToken)
    {
        try
        {
            var resourceProviders = _resourceRegistry.GetResourceProviders();
            var allCompletions = new List<CompletionItem>();

            foreach (var provider in resourceProviders)
            {
                try
                {
                    var resources = await provider.ListResourcesAsync(cancellationToken);
                    var resourceUris = resources
                        .Where(r => r.Uri.Contains(argument.Value, StringComparison.OrdinalIgnoreCase))
                        .Select(r => new CompletionItem
                        {
                            Value = r.Uri,
                            Label = r.Name ?? Path.GetFileName(r.Uri),
                            Description = r.Description
                        })
                        .ToArray();

                    allCompletions.AddRange(resourceUris);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get completions from resource provider {ProviderType}",
                        provider.GetType().Name);
                }
            }

            return allCompletions.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource completions");
            return Array.Empty<CompletionItem>();
        }
    }

    private static CompletionItem[] GetArgumentCompletions(PromptArgument arg, string currentValue)
    {
        var completions = new List<CompletionItem>();

        if (string.IsNullOrEmpty(currentValue))
        {
            completions.Add(new CompletionItem
            {
                Value = arg.Name,
                Label = arg.Name,
                Description = arg.Description
            });
        }
        else if (arg.Name.StartsWith(currentValue, StringComparison.OrdinalIgnoreCase))
        {
            completions.Add(new CompletionItem
            {
                Value = arg.Name,
                Label = arg.Name,
                Description = arg.Description
            });
        }

        return completions.ToArray();
    }
}