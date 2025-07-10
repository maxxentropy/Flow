using System.Diagnostics;
using McpServer.Application.Messages;
using McpServer.Application.Tracing;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles completion/complete requests.
/// </summary>
public class CompletionHandler : IMessageHandler
{
    private readonly ILogger<CompletionHandler> _logger;
    private readonly ICompletionService _completionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompletionHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="completionService">The completion service.</param>
    public CompletionHandler(ILogger<CompletionHandler> logger, ICompletionService completionService)
    {
        _logger = logger;
        _completionService = completionService;
    }

    /// <inheritdoc/>
    public bool CanHandle(Type messageType)
    {
        return messageType == typeof(CompletionCompleteRequest);
    }

    /// <inheritdoc/>
    public async Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        using var activity = TracingExtensions.StartHandlerActivity("CompletionHandler", message.GetType().Name);
        
        try
        {
            if (message is not JsonRpcRequest<CompletionCompleteRequest> request)
            {
                throw new ArgumentException("Invalid message type", nameof(message));
            }

            activity?.SetTag("completion.operation", "complete");

            if (request.Params == null)
            {
                _logger.LogWarning("Completion request has null parameters");
                return new CompletionCompleteResponse
                {
                    Completion = Array.Empty<CompletionItem>(),
                    HasMore = false,
                    Total = 0
                };
            }

            activity?.SetTag("completion.ref.type", request.Params.Ref.Type);
            activity?.SetTag("completion.ref.name", request.Params.Ref.Name);

            _logger.LogDebug("Handling completion request for {Type}:{Name}",
                request.Params.Ref.Type, request.Params.Ref.Name);

            try
            {
                var response = await _completionService.GetCompletionAsync(
                    request.Params.Ref,
                    request.Params.Argument,
                    cancellationToken);

                activity?.SetTag("completion.count", response.Completion.Length);
                activity?.SetTag("completion.has_more", response.HasMore);

                _logger.LogDebug("Returning {Count} completion suggestions",
                    response.Completion.Length);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get completion suggestions");
                return new CompletionCompleteResponse
                {
                    Completion = Array.Empty<CompletionItem>(),
                    HasMore = false,
                    Total = 0
                };
            }
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }
    }
}