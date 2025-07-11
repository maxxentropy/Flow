using System.Diagnostics;
using System.Linq;
using McpServer.Application.Messages;
using McpServer.Application.Services;
using McpServer.Application.Tracing;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Tools;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles tool-related requests.
/// </summary>
public class ToolsHandler : IMessageHandler
{
    private readonly ILogger<ToolsHandler> _logger;
    private readonly IToolRegistry _toolRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolsHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="toolRegistry">The tool registry.</param>
    public ToolsHandler(ILogger<ToolsHandler> logger, IToolRegistry toolRegistry)
    {
        _logger = logger;
        _toolRegistry = toolRegistry;
    }

    /// <inheritdoc/>
    public bool CanHandle(Type messageType)
    {
        return messageType == typeof(ToolsListRequest) || 
               messageType == typeof(ToolsCallRequest);
    }

    /// <inheritdoc/>
    public async Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        using var activity = TracingExtensions.StartHandlerActivity("ToolsHandler", message.GetType().Name);
        
        try
        {
            switch (message)
            {
                case JsonRpcRequest listRequest when listRequest.Method == "tools/list":
                    activity?.SetTag("tools.operation", "list");
                    return await HandleListToolsAsync(cancellationToken).ConfigureAwait(false);
                    
                case JsonRpcRequest<ToolsCallRequest> callRequest:
                    if (callRequest.Params == null)
                    {
                        throw new ProtocolException("Tool call request parameters cannot be null");
                    }
                    activity?.SetTag("tools.operation", "call");
                    activity?.SetTag("tools.name", callRequest.Params.Name);
                    return await HandleCallToolAsync(callRequest.Params, cancellationToken).ConfigureAwait(false);
                    
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

    private Task<object> HandleListToolsAsync(CancellationToken cancellationToken)
    {
        var tools = _toolRegistry.GetTools();
        var toolList = tools.Values.Select(tool => new ToolInfo
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = tool.Schema
        }).ToList();

        _logger.LogDebug("Listed {ToolCount} tools", toolList.Count);

        return Task.FromResult<object>(new ToolsListResponse { Tools = toolList });
    }

    private async Task<object> HandleCallToolAsync(ToolsCallRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calling tool: {ToolName}", request.Name);

        var tools = _toolRegistry.GetTools();
        if (!tools.TryGetValue(request.Name, out var tool))
        {
            throw new ToolExecutionException(request.Name, $"Tool '{request.Name}' not found");
        }

        try
        {
            var toolRequest = new ToolRequest
            {
                Name = request.Name,
                Arguments = request.Arguments
            };

            var result = await tool.ExecuteAsync(toolRequest, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Tool {ToolName} executed successfully", request.Name);

            return result;
        }
        catch (Exception ex) when (ex is not ToolExecutionException)
        {
            _logger.LogError(ex, "Tool {ToolName} execution failed", request.Name);
            throw new ToolExecutionException(request.Name, ex.Message, ex);
        }
    }
}