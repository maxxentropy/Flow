using McpServer.Application.Tools;
using McpServer.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Tools;

/// <summary>
/// Example tool that demonstrates progress reporting for long-running operations.
/// </summary>
public class DataProcessingTool : ProgressAwareTool
{
    private readonly ILogger<DataProcessingTool> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DataProcessingTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DataProcessingTool(ILogger<DataProcessingTool> logger) : base(logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public override string Name => "data_processor";
    
    /// <inheritdoc/>
    public override string Description => "Processes large datasets with progress reporting";
    
    /// <inheritdoc/>
    public override ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["itemCount"] = new
            {
                type = "integer",
                description = "Number of items to process",
                minimum = 1,
                maximum = 10000
            },
            ["processingTimeMs"] = new
            {
                type = "integer",
                description = "Time to process each item in milliseconds",
                minimum = 1,
                maximum = 1000,
                @default = 100
            }
        },
        Required = new List<string> { "itemCount" }
    };
    
    /// <inheritdoc/>
    protected override async Task<ToolResult> ExecuteWithProgressAsync(
        ToolRequest request,
        ProgressContext? progressContext,
        CancellationToken cancellationToken)
    {
        // Extract parameters
        var itemCount = Convert.ToInt32(request.Arguments!["itemCount"]);
        var processingTimeMs = request.Arguments.ContainsKey("processingTimeMs") 
            ? Convert.ToInt32(request.Arguments["processingTimeMs"]) 
            : 100;
        
        _logger.LogInformation("Starting data processing for {ItemCount} items", itemCount);
        
        var results = new List<object>();
        var errors = new List<string>();
        
        for (int i = 0; i < itemCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Report progress if context is available
            if (progressContext != null)
            {
                await progressContext.ReportProgressAsync(
                    i, 
                    itemCount, 
                    $"Processing item {i + 1} of {itemCount}",
                    cancellationToken);
            }
            
            try
            {
                // Simulate processing
                await Task.Delay(processingTimeMs, cancellationToken);
                
                // Generate some result
                results.Add(new
                {
                    itemId = i + 1,
                    processedAt = DateTime.UtcNow,
                    result = $"Processed item {i + 1}",
                    hash = Guid.NewGuid().ToString("N").Substring(0, 8)
                });
                
                // Log progress every 10 items
                if ((i + 1) % 10 == 0)
                {
                    _logger.LogDebug("Processed {Count}/{Total} items", i + 1, itemCount);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to process item {i + 1}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to process item {ItemNumber}", i + 1);
            }
        }
        
        // Final progress report
        if (progressContext != null)
        {
            await progressContext.ReportProgressAsync(
                itemCount,
                itemCount,
                "Processing complete",
                cancellationToken);
        }
        
        _logger.LogInformation("Data processing completed. Processed: {ProcessedCount}, Errors: {ErrorCount}", 
            results.Count, errors.Count);
        
        return new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent
                {
                    Text = $"Processing completed successfully!\n" +
                           $"- Items processed: {results.Count}\n" +
                           $"- Errors: {errors.Count}\n" +
                           $"- Total time: ~{itemCount * processingTimeMs / 1000.0:F1} seconds"
                }
            }
        };
    }
}