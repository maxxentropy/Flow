using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using McpServer.Application.Messages;
using McpServer.Application.Tracing;
using McpServer.Application.Services;
using McpServer.Application.Middleware;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Validation;
using McpServer.Domain.RateLimiting;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Server;

/// <summary>
/// Default implementation of the message router.
/// </summary>
public class MessageRouter : IMessageRouter
{
    private readonly ILogger<MessageRouter> _logger;
    private readonly IEnumerable<IMessageHandler> _handlers;
    private readonly IProgressTracker _progressTracker;
    private readonly IErrorResponseBuilder _errorResponseBuilder;
    private readonly ValidationMiddleware? _validationMiddleware;
    private readonly RateLimitingMiddleware? _rateLimitingMiddleware;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRouter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="handlers">The message handlers.</param>
    /// <param name="progressTracker">The progress tracker.</param>
    /// <param name="errorResponseBuilder">The error response builder.</param>
    /// <param name="validationMiddleware">The validation middleware (optional).</param>
    /// <param name="rateLimitingMiddleware">The rate limiting middleware (optional).</param>
    public MessageRouter(
        ILogger<MessageRouter> logger, 
        IEnumerable<IMessageHandler> handlers, 
        IProgressTracker progressTracker, 
        IErrorResponseBuilder errorResponseBuilder, 
        ValidationMiddleware? validationMiddleware = null,
        RateLimitingMiddleware? rateLimitingMiddleware = null)
    {
        _logger = logger;
        _handlers = handlers;
        _progressTracker = progressTracker;
        _errorResponseBuilder = errorResponseBuilder;
        _validationMiddleware = validationMiddleware;
        _rateLimitingMiddleware = rateLimitingMiddleware;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc/>
    public Task<object?> RouteMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        return RouteMessageAsync(message, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<object?> RouteMessageAsync(string message, RateLimitContext? rateLimitContext, CancellationToken cancellationToken = default)
    {
        using var activity = TracingExtensions.ActivitySource.StartActivity("MessageRouter.RouteMessage", ActivityKind.Internal);
        
        try
        {
            // Validate the message if validation middleware is available
            if (_validationMiddleware != null)
            {
                var validationResult = await _validationMiddleware.ValidateMessageAsync(message, cancellationToken);
                if (!validationResult.IsValid)
                {
                    var validationException = ValidationMiddleware.CreateValidationException(validationResult);
                    return _errorResponseBuilder.CreateErrorResponse(null, validationException);
                }
            }

            // First, try to parse as a JSON-RPC request
            var jsonDocument = JsonDocument.Parse(message);
            var root = jsonDocument.RootElement;
            
            // Check if it's a request or notification
            var hasId = root.TryGetProperty("id", out var idElement);
            var hasMethod = root.TryGetProperty("method", out var methodElement);
            
            // Check if it's a valid JSON-RPC message
            if (!root.TryGetProperty("jsonrpc", out var jsonrpcElement) || 
                jsonrpcElement.GetString() != "2.0")
            {
                var id = hasId ? ParseJsonElement(idElement) : null;
                return _errorResponseBuilder.CreateErrorResponse(id, McpErrorCodes.InvalidRequest, "Invalid JSON-RPC version");
            }
            
            if (!hasMethod)
            {
                var id = hasId ? ParseJsonElement(idElement) : null;
                return _errorResponseBuilder.CreateErrorResponse(
                    id, 
                    McpErrorCodes.InvalidRequest, 
                    "Missing method");
            }
            
            var method = methodElement.GetString();
            if (string.IsNullOrEmpty(method))
            {
                var id = hasId ? ParseJsonElement(idElement) : null;
                return _errorResponseBuilder.CreateErrorResponse(
                    id, 
                    McpErrorCodes.InvalidRequest, 
                    "Invalid method");
            }
            
            // Add tracing information
            activity?.SetTag("rpc.method", method);
            activity?.SetTag("rpc.has_id", hasId);
            if (hasId)
            {
                activity?.SetTag("rpc.id", ParseJsonElement(idElement)?.ToString());
            }

            // Check rate limit if middleware is available
            if (_rateLimitingMiddleware != null && rateLimitContext != null)
            {
                var identifier = RateLimitingMiddleware.ExtractIdentifier(rateLimitContext);
                var rateLimitResult = await _rateLimitingMiddleware.CheckRateLimitAsync(identifier, method, cancellationToken);
                
                if (!rateLimitResult.IsAllowed)
                {
                    _logger.LogWarning("Rate limit exceeded for {Identifier} calling {Method}", identifier, method);
                    activity?.SetTag("rate_limit.exceeded", true);
                    
                    if (hasId)
                    {
                        return RateLimitingMiddleware.CreateRateLimitErrorResponse(ParseJsonElement(idElement), rateLimitResult);
                    }
                    // Don't respond to rate-limited notifications
                    return null;
                }

                activity?.SetTag("rate_limit.remaining", rateLimitResult.Remaining);
            }
            
            // Route based on method
            var handler = GetHandlerForMethod(method);
            if (handler == null)
            {
                return hasId 
                    ? _errorResponseBuilder.CreateErrorResponse(ParseJsonElement(idElement), McpErrorCodes.MethodNotFound, $"Method '{method}' not found")
                    : null; // Don't respond to notifications with unknown methods
            }
            
            // Parse the full message based on method
            var request = ParseRequest(message, method);
            if (request == null)
            {
                var id = hasId ? ParseJsonElement(idElement) : null;
                return _errorResponseBuilder.CreateErrorResponse(
                    id, 
                    McpErrorCodes.InvalidParams, 
                    "Invalid parameters");
            }
            
            // Check for progress token in _meta field
            string? progressToken = null;
            if (root.TryGetProperty("_meta", out var metaElement) &&
                metaElement.TryGetProperty("progressToken", out var progressTokenElement))
            {
                progressToken = progressTokenElement.GetString();
                if (!string.IsNullOrEmpty(progressToken))
                {
                    _progressTracker.StartOperation(progressToken);
                    _logger.LogDebug("Started progress tracking for request {RequestId} with token {ProgressToken}", 
                        hasId ? ParseJsonElement(idElement)?.ToString() : "notification", progressToken);
                }
            }
            
            try
            {
                // Execute the handler
                var result = await handler.HandleMessageAsync(request, cancellationToken).ConfigureAwait(false);
                
                // Complete progress tracking if it was started
                if (!string.IsNullOrEmpty(progressToken))
                {
                    await _progressTracker.CompleteOperationAsync(progressToken, "Request completed successfully");
                }
                
                // If it's a notification (no ID), don't send a response
                if (!hasId)
                {
                    return null;
                }
                
                // Create success response
                activity?.SetSuccess("Request processed successfully");
                return new JsonRpcResponse
                {
                    Jsonrpc = "2.0",
                    Result = result,
                    Id = ParseJsonElement(idElement)
                };
            }
            catch (Exception ex)
            {
                // Mark progress as failed if tracking was started
                if (!string.IsNullOrEmpty(progressToken))
                {
                    await _progressTracker.FailOperationAsync(progressToken, ex.Message, ex);
                }
                throw;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON-RPC message");
            activity?.RecordException(ex);
            return _errorResponseBuilder.CreateErrorResponse(null, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling message");
            activity?.RecordException(ex);
            return _errorResponseBuilder.CreateErrorResponse(null, ex);
        }
    }

    private IMessageHandler? GetHandlerForMethod(string method)
    {
        // Map method names to handler types
        var handlerType = method switch
        {
            "initialize" => typeof(InitializeRequest),
            "initialized" => typeof(Messages.InitializedNotification),
            "ping" => typeof(PingRequest),
            "cancel" => typeof(CancelRequest),
            "tools/list" => typeof(ToolsListRequest),
            "tools/call" => typeof(ToolsCallRequest),
            "resources/list" => typeof(ResourcesListRequest),
            "resources/read" => typeof(ResourcesReadRequest),
            "resources/subscribe" => typeof(ResourcesSubscribeRequest),
            "resources/unsubscribe" => typeof(ResourcesUnsubscribeRequest),
            "prompts/list" => typeof(PromptsListRequest),
            "prompts/get" => typeof(PromptsGetRequest),
            "logging/setLevel" => typeof(Messages.LoggingSetLevelRequest),
            "roots/list" => typeof(RootsListRequest),
            "completion/complete" => typeof(CompletionCompleteRequest),
            _ => null
        };
        
        if (handlerType == null)
        {
            return null;
        }
        
        return _handlers.FirstOrDefault(h => h.CanHandle(handlerType));
    }

    private object? ParseRequest(string message, string method)
    {
        try
        {
            return method switch
            {
                "initialize" => JsonSerializer.Deserialize<JsonRpcRequest<InitializeRequest>>(message, _jsonOptions),
                "initialized" => JsonSerializer.Deserialize<JsonRpcNotification>(message, _jsonOptions),
                "ping" => JsonSerializer.Deserialize<PingRequest>(message, _jsonOptions),
                "cancel" => JsonSerializer.Deserialize<CancelRequest>(message, _jsonOptions),
                "tools/list" => JsonSerializer.Deserialize<JsonRpcRequest>(message, _jsonOptions),
                "tools/call" => JsonSerializer.Deserialize<JsonRpcRequest<ToolsCallRequest>>(message, _jsonOptions),
                "resources/list" => JsonSerializer.Deserialize<JsonRpcRequest>(message, _jsonOptions),
                "resources/read" => JsonSerializer.Deserialize<JsonRpcRequest<ResourcesReadRequest>>(message, _jsonOptions),
                "resources/subscribe" => JsonSerializer.Deserialize<JsonRpcRequest<ResourcesSubscribeRequest>>(message, _jsonOptions),
                "resources/unsubscribe" => JsonSerializer.Deserialize<JsonRpcRequest<ResourcesUnsubscribeRequest>>(message, _jsonOptions),
                "prompts/list" => JsonSerializer.Deserialize<JsonRpcRequest>(message, _jsonOptions),
                "prompts/get" => JsonSerializer.Deserialize<JsonRpcRequest<PromptsGetRequest>>(message, _jsonOptions),
                "logging/setLevel" => JsonSerializer.Deserialize<JsonRpcRequest<Messages.LoggingSetLevelRequest>>(message, _jsonOptions),
                "roots/list" => JsonSerializer.Deserialize<JsonRpcRequest>(message, _jsonOptions),
                "completion/complete" => JsonSerializer.Deserialize<JsonRpcRequest<CompletionCompleteRequest>>(message, _jsonOptions),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static object? ParseJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

}