namespace McpServer.Infrastructure.Tools;

/// <summary>
/// A simple echo tool for testing.
/// </summary>
public class EchoTool : ITool
{
    private readonly ILogger<EchoTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EchoTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public EchoTool(ILogger<EchoTool> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "echo";

    /// <inheritdoc/>
    public string Description => "Echoes back the provided message";

    /// <inheritdoc/>
    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["message"] = new
            {
                type = "string",
                description = "The message to echo back"
            }
        },
        Required = new List<string> { "message" }
    };

    /// <inheritdoc/>
    public Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Arguments == null || !request.Arguments.TryGetValue("message", out var messageObj))
        {
            return Task.FromResult(new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new TextContent { Text = "Error: 'message' parameter is required" }
                },
                IsError = true
            });
        }

        var message = messageObj?.ToString() ?? string.Empty;
        _logger.LogInformation("Echoing message: {Message}", message);

        return Task.FromResult(new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = $"Echo: {message}" }
            }
        });
    }
}