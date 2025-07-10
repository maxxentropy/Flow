namespace McpServer.Infrastructure.Tools;

/// <summary>
/// A simple calculator tool for basic arithmetic operations.
/// </summary>
public class CalculatorTool : ITool
{
    private readonly ILogger<CalculatorTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalculatorTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public CalculatorTool(ILogger<CalculatorTool> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "calculator";

    /// <inheritdoc/>
    public string Description => "Performs basic arithmetic operations";

    /// <inheritdoc/>
    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["operation"] = new
            {
                type = "string",
                description = "The operation to perform",
                @enum = new[] { "add", "subtract", "multiply", "divide" }
            },
            ["a"] = new
            {
                type = "number",
                description = "The first operand"
            },
            ["b"] = new
            {
                type = "number",
                description = "The second operand"
            }
        },
        Required = new List<string> { "operation", "a", "b" }
    };

    /// <inheritdoc/>
    public Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Arguments == null)
        {
            return Task.FromResult(CreateErrorResult("Missing arguments"));
        }

        if (!request.Arguments.TryGetValue("operation", out var operationObj) ||
            !request.Arguments.TryGetValue("a", out var aObj) ||
            !request.Arguments.TryGetValue("b", out var bObj))
        {
            return Task.FromResult(CreateErrorResult("Missing required parameters"));
        }

        var operation = operationObj?.ToString();
        if (!double.TryParse(aObj?.ToString(), out var a) ||
            !double.TryParse(bObj?.ToString(), out var b))
        {
            return Task.FromResult(CreateErrorResult("Invalid number format"));
        }

        _logger.LogInformation("Performing calculation: {A} {Operation} {B}", a, operation, b);

        double result;
        try
        {
            result = operation switch
            {
                "add" => a + b,
                "subtract" => a - b,
                "multiply" => a * b,
                "divide" => b != 0 ? a / b : throw new DivideByZeroException(),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
        }
        catch (DivideByZeroException)
        {
            return Task.FromResult(CreateErrorResult("Division by zero"));
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(CreateErrorResult(ex.Message));
        }

        return Task.FromResult(new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = $"Result: {result}" }
            }
        });
    }

    private static ToolResult CreateErrorResult(string message)
    {
        return new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = $"Error: {message}" }
            },
            IsError = true
        };
    }
}