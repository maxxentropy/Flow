using System.ComponentModel;
using McpServer.Application.Services;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Tools;

/// <summary>
/// A tool that demonstrates MCP logging functionality.
/// </summary>
[Description("Demonstrates MCP logging at different levels")]
public class LoggingDemoTool : ITool
{
    private readonly ILogger<LoggingDemoTool> _logger;
    private readonly IServiceProvider _serviceProvider;
    private ILoggingService? _loggingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingDemoTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public LoggingDemoTool(ILogger<LoggingDemoTool> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public string Name => "logging_demo";

    /// <inheritdoc/>
    public string Description => "Demonstrates MCP logging functionality by sending log messages at different levels";

    /// <inheritdoc/>
    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["level"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "The log level to demonstrate",
                ["enum"] = new[] { "debug", "info", "notice", "warning", "error", "critical", "alert", "emergency" }
            },
            ["message"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "The message to log (optional)"
            },
            ["logger"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "The logger name to use (optional)"
            },
            ["simulate_error"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "Whether to simulate an error scenario (optional)"
            }
        },
        Required = new List<string> { "level" }
    };

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing logging demo tool");

        // Get logging service lazily
        _loggingService ??= _serviceProvider.GetService<ILoggingService>();

        if (_loggingService == null)
        {
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = "Error: Logging service is not available" }
                },
                IsError = true
            };
        }

        // Parse arguments
        if (request.Arguments == null || !request.Arguments.TryGetValue("level", out var levelObj))
        {
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = "Error: 'level' parameter is required" }
                },
                IsError = true
            };
        }

        var level = levelObj?.ToString() ?? "info";
        var message = request.Arguments.TryGetValue("message", out var msgObj) ? msgObj?.ToString() : null;
        var logger = request.Arguments.TryGetValue("logger", out var loggerObj) ? loggerObj?.ToString() : null;
        var simulateError = request.Arguments.TryGetValue("simulate_error", out var errorObj) && errorObj is bool error && error;

        // Create log data
        var logData = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow,
            ["tool"] = Name,
            ["message"] = message ?? $"This is a {level} level log message from the logging demo tool",
            ["demo"] = true
        };

        if (simulateError)
        {
            logData["error"] = new
            {
                type = "DemoException",
                message = "This is a simulated error for demonstration purposes",
                stack_trace = "at LoggingDemoTool.ExecuteAsync() line 42"
            };
        }

        try
        {
            // Parse and log at the specified level
            var logLevel = level.ToLogLevel();
            await _loggingService.LogAsync(logLevel, logData, logger ?? "demo", cancellationToken);

            var response = $"Successfully sent {level} log message";
            if (!string.IsNullOrEmpty(logger))
            {
                response += $" from logger '{logger}'";
            }
            
            response += $". Current minimum log level is {_loggingService.MinimumLogLevel.ToLogLevelString()}.";

            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = response }
                }
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid log level: {Level}", level);
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent 
                    { 
                        Text = $"Error: Invalid log level '{level}'. Valid levels are: debug, info, notice, warning, error, critical, alert, emergency"
                    }
                },
                IsError = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing logging demo");
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = $"Error executing logging demo: {ex.Message}" }
                },
                IsError = true
            };
        }
    }
}