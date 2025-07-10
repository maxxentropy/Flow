using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using McpServer.Application.Services;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SamplingMessages = McpServer.Domain.Protocol.Messages;

namespace McpServer.Infrastructure.Tools;

/// <summary>
/// A tool that uses sampling to get AI assistance.
/// </summary>
[Description("Get AI assistance for various tasks using the connected LLM")]
public class AiAssistantTool : ITool
{
    private readonly ILogger<AiAssistantTool> _logger;
    private readonly IServiceProvider _serviceProvider;
    private ISamplingService? _samplingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiAssistantTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public AiAssistantTool(ILogger<AiAssistantTool> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public string Name => "ai_assistant";

    /// <inheritdoc/>
    public string Description => "Get AI assistance for various tasks";

    /// <inheritdoc/>
    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["task"] = new Dictionary<string, object>
            { 
                ["type"] = "string", 
                ["description"] = "The task you need help with" 
            },
            ["context"] = new Dictionary<string, object>
            { 
                ["type"] = "string", 
                ["description"] = "Additional context for the task (optional)" 
            },
            ["temperature"] = new Dictionary<string, object>
            {
                ["type"] = "number",
                ["description"] = "Temperature for sampling (0.0-1.0, optional)",
                ["minimum"] = 0.0,
                ["maximum"] = 1.0
            },
            ["max_tokens"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "Maximum tokens to generate (optional)",
                ["minimum"] = 1
            }
        },
        Required = new List<string> { "task" }
    };

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing AI assistant tool");

        // Get sampling service lazily
        _samplingService ??= _serviceProvider.GetService<ISamplingService>();
        
        if (_samplingService == null)
        {
            throw new ToolExecutionException(Name, "Sampling service is not available");
        }

        if (!_samplingService.IsSamplingSupported)
        {
            throw new ToolExecutionException(Name, "The connected client does not support sampling");
        }

        // Parse arguments
        if (request.Arguments == null || !request.Arguments.TryGetValue("task", out var taskObj))
        {
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = "Error: 'task' parameter is required" }
                },
                IsError = true
            };
        }
        
        var task = taskObj?.ToString() ?? throw new ToolExecutionException(Name, "Task is required");
        var context = request.Arguments.TryGetValue("context", out var contextObj) ? contextObj?.ToString() : null;
        var temperature = request.Arguments.TryGetValue("temperature", out var tempObj) && tempObj is double temp ? temp : (double?)null;
        var maxTokens = request.Arguments.TryGetValue("max_tokens", out var tokensObj) && tokensObj is int tokens ? tokens : (int?)null;

        // Build the prompt
        var prompt = $"Please help with the following task:\n\n{task}";
        if (!string.IsNullOrEmpty(context))
        {
            prompt += $"\n\nAdditional context:\n{context}";
        }

        try
        {
            // Create the sampling request
            var samplingRequest = new CreateMessageRequest
            {
                Messages = new List<SamplingMessage>
                {
                    new()
                    {
                        Role = "user",
                        Content = new SamplingMessages.TextContent { Text = prompt }
                    }
                },
                Temperature = temperature,
                MaxTokens = maxTokens,
                SystemPrompt = "You are a helpful AI assistant integrated into an MCP server. Provide clear, concise, and accurate responses."
            };

            // Send the request and wait for response
            var response = await _samplingService.CreateMessageAsync(samplingRequest, cancellationToken);

            // Extract the response text
            var responseText = response.Content switch
            {
                SamplingMessages.TextContent textContent => textContent.Text,
                _ => "Received non-text response"
            };

            _logger.LogInformation("AI assistant completed successfully");

            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent 
                    { 
                        Text = $"{responseText}\n\n---\nModel: {response.Model}\nStop Reason: {response.StopReason ?? "unknown"}"
                    }
                }
            };
        }
        catch (Exception ex) when (ex is not ToolExecutionException)
        {
            _logger.LogError(ex, "Error executing AI assistant");
            throw new ToolExecutionException(Name, $"Failed to get AI assistance: {ex.Message}", ex);
        }
    }
}