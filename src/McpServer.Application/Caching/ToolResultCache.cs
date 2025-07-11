using System.Text.Json;
using McpServer.Application.Services;
using McpServer.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Caching;

/// <summary>
/// Caching decorator for tool execution results.
/// </summary>
public class ToolResultCache : ITool
{
    private readonly ITool _innerTool;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ToolResultCache> _logger;
    private readonly ToolCacheOptions _options;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolResultCache"/> class.
    /// </summary>
    public ToolResultCache(
        ITool innerTool,
        ICacheService cacheService,
        ILogger<ToolResultCache> logger,
        ToolCacheOptions options)
    {
        _innerTool = innerTool ?? throw new ArgumentNullException(nameof(innerTool));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
        // Check if caching is enabled for this tool
        if (!ShouldCache(request))
        {
            _logger.LogTrace("Caching disabled for tool {ToolName}, executing directly", Name);
            return await _innerTool.ExecuteAsync(request, cancellationToken);
        }
        
        // Generate cache key
        var cacheKey = GenerateCacheKey(request);
        
        // Try to get from cache
        if (_cacheService.TryGetValue<ToolResult>(cacheKey, out var cachedResult))
        {
            _logger.LogDebug("Cache hit for tool {ToolName} with key {CacheKey}", Name, cacheKey);
            return cachedResult;
        }
        
        // Execute tool and cache result
        var result = await _innerTool.ExecuteAsync(request, cancellationToken);
        
        // Only cache successful results
        if (result.IsSuccess && ShouldCacheResult(result))
        {
            var cacheOptions = new CacheEntryOptions
            {
                SlidingExpiration = _options.DefaultExpiration,
                Priority = _options.Priority,
                Size = EstimateResultSize(result),
                Callbacks = new CacheEntryCallbacks
                {
                    OnEviction = (key, value, reason) =>
                    {
                        _logger.LogDebug("Tool result evicted from cache: {Key}, Reason: {Reason}", key, reason);
                    }
                }
            };
            
            _cacheService.Set(cacheKey, result, cacheOptions);
            _logger.LogDebug("Cached tool result for {ToolName} with key {CacheKey}", Name, cacheKey);
        }
        
        return result;
    }
    
    private bool ShouldCache(ToolRequest request)
    {
        // Check if tool is in exclude list
        if (_options.ExcludedTools.Contains(Name, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }
        
        // Check if tool has specific cache configuration
        if (_options.ToolConfigurations.TryGetValue(Name, out var config))
        {
            return config.Enabled;
        }
        
        // Default to enabled
        return _options.Enabled;
    }
    
    private bool ShouldCacheResult(ToolResult result)
    {
        // Don't cache results with streaming content
        if (result.Content.Any(c => c.Type == "stream"))
        {
            return false;
        }
        
        // Don't cache results that are too large
        var size = EstimateResultSize(result);
        if (size > _options.MaxResultSize)
        {
            _logger.LogDebug("Tool result too large to cache: {Size} bytes", size);
            return false;
        }
        
        return true;
    }
    
    private string GenerateCacheKey(ToolRequest request)
    {
        var keyData = new
        {
            Tool = Name,
            Args = request.Arguments
        };
        
        var json = JsonSerializer.Serialize(keyData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        
        // Create a hash of the JSON for a more compact key
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
        var hash = Convert.ToBase64String(hashBytes);
        
        return $"tool:{Name}:{hash}";
    }
    
    private long EstimateResultSize(ToolResult result)
    {
        // Simple estimation based on content
        long size = 0;
        
        foreach (var content in result.Content)
        {
            size += content.Type?.Length ?? 0;
            
            size += content switch
            {
                McpServer.Domain.Tools.TextContent text => text.Text.Length * 2, // Unicode
                McpServer.Domain.Tools.ImageContent image => image.Data.Length,
                _ => 0
            };
        }
        
        return size;
    }
}

/// <summary>
/// Configuration options for tool result caching.
/// </summary>
public class ToolCacheOptions
{
    /// <summary>
    /// Gets or sets whether caching is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the default cache expiration.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Gets or sets the cache priority.
    /// </summary>
    public CachePriority Priority { get; set; } = CachePriority.Normal;
    
    /// <summary>
    /// Gets or sets the maximum size of a result to cache.
    /// </summary>
    public long MaxResultSize { get; set; } = 1024 * 1024; // 1MB
    
    /// <summary>
    /// Gets or sets tools that should not be cached.
    /// </summary>
    public HashSet<string> ExcludedTools { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "LoggingDemoTool", // Don't cache logging operations
        "AuthenticationDemoTool" // Don't cache auth operations
    };
    
    /// <summary>
    /// Gets or sets tool-specific cache configurations.
    /// </summary>
    public Dictionary<string, ToolCacheConfiguration> ToolConfigurations { get; set; } = new();
}

/// <summary>
/// Tool-specific cache configuration.
/// </summary>
public class ToolCacheConfiguration
{
    /// <summary>
    /// Gets or sets whether caching is enabled for this tool.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the cache expiration for this tool.
    /// </summary>
    public TimeSpan? Expiration { get; set; }
    
    /// <summary>
    /// Gets or sets the cache priority for this tool.
    /// </summary>
    public CachePriority? Priority { get; set; }
}