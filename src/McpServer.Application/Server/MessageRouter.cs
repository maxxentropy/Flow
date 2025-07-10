using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using McpServer.Application.Messages;
using McpServer.Application.Tracing;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Server;

/// <summary>
/// Default implementation of the message router.
/// </summary>
public class MessageRouter : IMessageRouter
{
    private readonly ILogger<MessageRouter> _logger;
    private readonly IEnumerable<IMessageHandler> _handlers;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRouter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="handlers">The message handlers.</param>
    public MessageRouter(ILogger<MessageRouter> logger, IEnumerable<IMessageHandler> handlers)
    {
        _logger = logger;
        _handlers = handlers;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc/>
    public async Task<object?> RouteMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        using var activity = TracingExtensions.ActivitySource.StartActivity("MessageRouter.RouteMessage", ActivityKind.Internal);
        
        try
        {
            // First, try to parse as a JSON-RPC request
            var jsonDocument = JsonDocument.Parse(message);
            var root = jsonDocument.RootElement;
            
            // Check if it's a valid JSON-RPC message
            if (!root.TryGetProperty("jsonrpc", out var jsonrpcElement) || 
                jsonrpcElement.GetString() != "2.0")
            {
                return CreateErrorResponse(null, JsonRpcErrorCodes.InvalidRequest, "Invalid JSON-RPC version");
            }
            
            // Check if it's a request or notification
            var hasId = root.TryGetProperty("id", out var idElement);
            var hasMethod = root.TryGetProperty("method", out var methodElement);
            
            if (!hasMethod)
            {
                return CreateErrorResponse(
                    hasId ? idElement : null, 
                    JsonRpcErrorCodes.InvalidRequest, 
                    "Missing method");
            }
            
            var method = methodElement.GetString();
            if (string.IsNullOrEmpty(method))
            {
                return CreateErrorResponse(
                    hasId ? idElement : null, 
                    JsonRpcErrorCodes.InvalidRequest, 
                    "Invalid method");
            }
            
            // Add tracing information
            activity?.SetTag("rpc.method", method);
            activity?.SetTag("rpc.has_id", hasId);
            if (hasId)
            {
                activity?.SetTag("rpc.id", idElement.GetRawText());
            }
            
            // Route based on method
            var handler = GetHandlerForMethod(method);
            if (handler == null)
            {
                return hasId 
                    ? CreateErrorResponse(idElement, JsonRpcErrorCodes.MethodNotFound, $"Method '{method}' not found")
                    : null; // Don't respond to notifications with unknown methods
            }
            
            // Parse the full message based on method
            var request = ParseRequest(message, method);
            if (request == null)
            {
                return CreateErrorResponse(
                    hasId ? idElement : null, 
                    JsonRpcErrorCodes.InvalidParams, 
                    "Invalid parameters");
            }
            
            // Execute the handler
            var result = await handler.HandleMessageAsync(request, cancellationToken).ConfigureAwait(false);
            
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
                Id = idElement
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON-RPC message");
            activity?.RecordException(ex);
            return CreateErrorResponse(null, JsonRpcErrorCodes.ParseError, "Parse error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling message");
            activity?.RecordException(ex);
            return CreateErrorResponse(null, JsonRpcErrorCodes.InternalError, "Internal error");
        }
    }

    private IMessageHandler? GetHandlerForMethod(string method)
    {
        // Map method names to handler types
        var handlerType = method switch
        {
            "initialize" => typeof(InitializeRequest),
            "initialized" => typeof(InitializedNotification),
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

    private static JsonRpcResponse CreateErrorResponse(JsonElement? id, int code, string message)
    {
        return new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            },
            Id = id?.ValueKind switch
            {
                JsonValueKind.String => id.Value.GetString(),
                JsonValueKind.Number => id.Value.GetInt64(),
                _ => null
            }
        };
    }
}