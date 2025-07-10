using System.ComponentModel;
using System.Text.Json;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using McpServer.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Tools;

/// <summary>
/// Demo tool that showcases completion functionality.
/// </summary>
[Description("Demonstrates completion functionality for prompts and resources")]
public class CompletionDemoTool : ITool
{
    private readonly ILogger<CompletionDemoTool> _logger;
    private readonly ICompletionService _completionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompletionDemoTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="completionService">The completion service.</param>
    public CompletionDemoTool(ILogger<CompletionDemoTool> logger, ICompletionService completionService)
    {
        _logger = logger;
        _completionService = completionService;
    }

    /// <inheritdoc/>
    public string Name => "completion_demo";

    /// <inheritdoc/>
    public string Description => "Demonstrates completion functionality for prompts and resources";

    /// <inheritdoc/>
    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["reference_type"] = new
            {
                type = "string",
                description = "Type of reference to complete (ref/prompt or ref/resource)",
                @enum = new[] { "ref/prompt", "ref/resource" }
            },
            ["reference_name"] = new
            {
                type = "string",
                description = "Name of the reference to complete"
            },
            ["argument_name"] = new
            {
                type = "string",
                description = "Name of the argument to complete"
            },
            ["argument_value"] = new
            {
                type = "string",
                description = "Current value of the argument (partial input)"
            }
        },
        Required = new List<string> { "reference_type", "reference_name", "argument_name", "argument_value" }
    };

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing completion demo with arguments: {Arguments}", 
                JsonSerializer.Serialize(request.Arguments));

            if (request.Arguments == null ||
                !request.Arguments.TryGetValue("reference_type", out var refType) ||
                !request.Arguments.TryGetValue("reference_name", out var refName) ||
                !request.Arguments.TryGetValue("argument_name", out var argName) ||
                !request.Arguments.TryGetValue("argument_value", out var argValue))
            {
                return new ToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new Domain.Tools.TextContent
                        {
                            Text = "Missing required arguments: reference_type, reference_name, argument_name, argument_value"
                        }
                    },
                    IsError = true
                };
            }

            var reference = new CompletionReference
            {
                Type = refType?.ToString() ?? "",
                Name = refName?.ToString() ?? ""
            };

            var argument = new CompletionArgument
            {
                Name = argName?.ToString() ?? "",
                Value = argValue?.ToString() ?? ""
            };

            var completionResponse = await _completionService.GetCompletionAsync(reference, argument, cancellationToken);

            var resultText = $"Completion results for {reference.Type}:{reference.Name} argument '{argument.Name}' with value '{argument.Value}':\n\n";
            
            if (completionResponse.Completion.Length == 0)
            {
                resultText += "No completions found.";
            }
            else
            {
                resultText += $"Found {completionResponse.Total} completion(s):\n";
                for (int i = 0; i < completionResponse.Completion.Length; i++)
                {
                    var completion = completionResponse.Completion[i];
                    resultText += $"{i + 1}. Value: '{completion.Value}'";
                    if (!string.IsNullOrEmpty(completion.Label))
                    {
                        resultText += $", Label: '{completion.Label}'";
                    }
                    if (!string.IsNullOrEmpty(completion.Description))
                    {
                        resultText += $", Description: '{completion.Description}'";
                    }
                    resultText += "\n";
                }

                if (completionResponse.HasMore == true)
                {
                    resultText += "\n(More completions available)";
                }
            }

            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new Domain.Tools.TextContent
                    {
                        Text = resultText
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing completion demo tool");
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new Domain.Tools.TextContent
                    {
                        Text = $"Error: {ex.Message}"
                    }
                },
                IsError = true
            };
        }
    }
}