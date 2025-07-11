using McpServer.Application.Middleware;
using McpServer.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Tools;

/// <summary>
/// Wrapper that adds validation to tool execution.
/// </summary>
public class ValidatedToolWrapper : ITool
{
    private readonly ITool _innerTool;
    private readonly ValidationMiddleware _validationMiddleware;
    private readonly ILogger<ValidatedToolWrapper> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatedToolWrapper"/> class.
    /// </summary>
    /// <param name="innerTool">The inner tool to wrap.</param>
    /// <param name="validationMiddleware">The validation middleware.</param>
    /// <param name="logger">The logger.</param>
    public ValidatedToolWrapper(ITool innerTool, ValidationMiddleware validationMiddleware, ILogger<ValidatedToolWrapper> logger)
    {
        _innerTool = innerTool;
        _validationMiddleware = validationMiddleware;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => _innerTool.Name;

    /// <inheritdoc/>
    public string Description => _innerTool.Description;

    /// <inheritdoc/>
    public ToolSchema Schema => _innerTool.Schema;

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate tool arguments before execution
            var validationResult = _validationMiddleware.ValidateToolArguments(request.Name, request.Arguments, Schema);
            
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Tool argument validation failed for {ToolName}: {Errors}", 
                    request.Name, string.Join("; ", validationResult.Errors.Select(e => e.Message)));
                
                // Return error result instead of throwing
                return new ToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new Domain.Tools.TextContent 
                        { 
                            Text = $"Validation failed: {string.Join("; ", validationResult.Errors.Select(e => e.Message))}" 
                        }
                    },
                    IsError = true
                };
            }

            _logger.LogDebug("Tool argument validation succeeded for {ToolName}", request.Name);
            
            // Execute the inner tool
            return await _innerTool.ExecuteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing validated tool {ToolName}", request.Name);
            
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new Domain.Tools.TextContent { Text = $"Tool execution error: {ex.Message}" }
                },
                IsError = true
            };
        }
    }
}

/// <summary>
/// Factory for creating validated tool wrappers.
/// </summary>
public class ValidatedToolFactory
{
    private readonly ValidationMiddleware _validationMiddleware;
    private readonly ILogger<ValidatedToolWrapper> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatedToolFactory"/> class.
    /// </summary>
    /// <param name="validationMiddleware">The validation middleware.</param>
    /// <param name="logger">The logger.</param>
    public ValidatedToolFactory(ValidationMiddleware validationMiddleware, ILogger<ValidatedToolWrapper> logger)
    {
        _validationMiddleware = validationMiddleware;
        _logger = logger;
    }

    /// <summary>
    /// Creates a validated tool wrapper for the given tool.
    /// </summary>
    /// <param name="tool">The tool to wrap.</param>
    /// <returns>The validated tool wrapper.</returns>
    public ITool CreateValidatedTool(ITool tool)
    {
        return new ValidatedToolWrapper(tool, _validationMiddleware, _logger);
    }

    /// <summary>
    /// Creates validated tool wrappers for a collection of tools.
    /// </summary>
    /// <param name="tools">The tools to wrap.</param>
    /// <returns>The validated tool wrappers.</returns>
    public IEnumerable<ITool> CreateValidatedTools(IEnumerable<ITool> tools)
    {
        return tools.Select(CreateValidatedTool);
    }
}