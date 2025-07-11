using McpServer.Application.Middleware;
using McpServer.Application.Services;
using McpServer.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Tools;

/// <summary>
/// Base class for tools that support progress reporting.
/// </summary>
public abstract class ProgressAwareTool : ITool
{
    private readonly ILogger _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressAwareTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    protected ProgressAwareTool(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc/>
    public abstract string Name { get; }
    
    /// <inheritdoc/>
    public abstract string Description { get; }
    
    /// <inheritdoc/>
    public abstract ToolSchema Schema { get; }
    
    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        // Extract progress token from _meta if available
        string? progressToken = null;
        if (request.Arguments?.TryGetValue("_meta", out var metaValue) == true &&
            metaValue is Dictionary<string, object> meta &&
            meta.TryGetValue("progressToken", out var tokenValue))
        {
            progressToken = tokenValue?.ToString();
        }
        
        // Create progress context
        var progressContext = progressToken != null 
            ? new ProgressContext(progressToken) 
            : null;
        
        try
        {
            return await ExecuteWithProgressAsync(request, progressContext, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tool {ToolName} execution cancelled", Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", Name);
            throw;
        }
    }
    
    /// <summary>
    /// Executes the tool with progress reporting support.
    /// </summary>
    /// <param name="request">The tool request.</param>
    /// <param name="progressContext">The progress context, if available.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool result.</returns>
    protected abstract Task<ToolResult> ExecuteWithProgressAsync(
        ToolRequest request, 
        ProgressContext? progressContext, 
        CancellationToken cancellationToken);
}

/// <summary>
/// Context for reporting progress.
/// </summary>
public class ProgressContext
{
    private readonly string _progressToken;
    private readonly IProgressTracker? _progressTracker;
    private readonly INotificationService? _notificationService;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressContext"/> class.
    /// </summary>
    /// <param name="progressToken">The progress token.</param>
    public ProgressContext(string progressToken)
    {
        _progressToken = progressToken ?? throw new ArgumentNullException(nameof(progressToken));
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressContext"/> class with services.
    /// </summary>
    /// <param name="progressToken">The progress token.</param>
    /// <param name="progressTracker">The progress tracker service.</param>
    /// <param name="notificationService">The notification service.</param>
    public ProgressContext(string progressToken, IProgressTracker progressTracker, INotificationService notificationService)
    {
        _progressToken = progressToken ?? throw new ArgumentNullException(nameof(progressToken));
        _progressTracker = progressTracker;
        _notificationService = notificationService;
    }
    
    /// <summary>
    /// Gets the progress token.
    /// </summary>
    public string ProgressToken => _progressToken;
    
    /// <summary>
    /// Reports progress.
    /// </summary>
    /// <param name="progress">The current progress value.</param>
    /// <param name="total">The total value, if known.</param>
    /// <param name="message">Optional progress message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReportProgressAsync(double progress, double? total = null, string? message = null, CancellationToken cancellationToken = default)
    {
        // Update progress tracker if available
        if (_progressTracker != null)
        {
            await _progressTracker.UpdateProgressAsync(_progressToken, progress, message, total);
        }
        
        // Send notification if service is available
        if (_notificationService != null)
        {
            await _notificationService.NotifyProgressAsync(_progressToken, progress, total, message, cancellationToken);
        }
    }
}