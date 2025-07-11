using System.Diagnostics;
using System.Linq;
using McpServer.Application.Messages;
using McpServer.Application.Services;
using McpServer.Application.Tracing;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Prompts;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles prompt-related requests.
/// </summary>
public class PromptsHandler : IMessageHandler
{
    private readonly ILogger<PromptsHandler> _logger;
    private readonly IPromptRegistry _promptRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptsHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="promptRegistry">The prompt registry.</param>
    public PromptsHandler(ILogger<PromptsHandler> logger, IPromptRegistry promptRegistry)
    {
        _logger = logger;
        _promptRegistry = promptRegistry;
    }

    /// <inheritdoc/>
    public bool CanHandle(Type messageType)
    {
        return messageType == typeof(PromptsListRequest) || 
               messageType == typeof(PromptsGetRequest);
    }

    /// <inheritdoc/>
    public async Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        using var activity = TracingExtensions.StartHandlerActivity("PromptsHandler", message.GetType().Name);
        
        try
        {
            switch (message)
            {
                case JsonRpcRequest listRequest when listRequest.Method == "prompts/list":
                    activity?.SetTag("prompts.operation", "list");
                    return await HandleListPromptsAsync(cancellationToken).ConfigureAwait(false);
                    
                case JsonRpcRequest<PromptsGetRequest> getRequest:
                    activity?.SetTag("prompts.operation", "get");
                    if (getRequest.Params == null)
                    {
                        throw new ProtocolException("Prompt get request parameters cannot be null");
                    }
                    activity?.SetTag("prompts.name", getRequest.Params.Name);
                    return await HandleGetPromptAsync(getRequest.Params, cancellationToken).ConfigureAwait(false);
                    
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

    private async Task<object> HandleListPromptsAsync(CancellationToken cancellationToken)
    {
        var allPrompts = new List<Prompt>();
        var providers = _promptRegistry.GetPromptProviders();

        foreach (var provider in providers)
        {
            try
            {
                var prompts = await provider.ListPromptsAsync(cancellationToken).ConfigureAwait(false);
                allPrompts.AddRange(prompts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list prompts from provider {ProviderType}", provider.GetType().Name);
            }
        }

        _logger.LogDebug("Listed {PromptCount} prompts from {ProviderCount} providers", 
            allPrompts.Count, providers.Count);
        
        // Add prompt count to the current activity
        Activity.Current?.SetTag("prompts.count", allPrompts.Count);
        Activity.Current?.SetTag("prompts.providers", providers.Count);

        return new { prompts = allPrompts };
    }

    private async Task<object> HandleGetPromptAsync(PromptsGetRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting prompt: {PromptName}", request.Name);

        var providers = _promptRegistry.GetPromptProviders();
        
        foreach (var provider in providers)
        {
            try
            {
                var prompts = await provider.ListPromptsAsync(cancellationToken).ConfigureAwait(false);
                if (prompts.Any(p => p.Name == request.Name))
                {
                    var result = await provider.GetPromptAsync(request.Name, request.Arguments, cancellationToken)
                        .ConfigureAwait(false);
                    
                    _logger.LogInformation("Prompt {PromptName} retrieved successfully", request.Name);
                    
                    // Add success tags to the current activity
                    Activity.Current?.SetTag("prompts.found", true);
                    Activity.Current?.SetTag("prompts.provider", provider.GetType().Name);
                    
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get prompt {PromptName} from provider {ProviderType}", 
                    request.Name, provider.GetType().Name);
            }
        }

        throw new McpException($"Prompt '{request.Name}' not found");
    }
}