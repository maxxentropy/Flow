using McpServer.Application.Services;
using McpServer.Domain.Tools;
using McpServer.Domain.Resources;
using McpServer.Domain.Prompts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.Application.Caching;

/// <summary>
/// Service for warming up cache with frequently accessed data.
/// </summary>
public interface ICacheWarmupService
{
    /// <summary>
    /// Warms up the cache with tools, resources, and prompts.
    /// </summary>
    Task WarmupAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Warms up tool results for common operations.
    /// </summary>
    Task WarmupToolsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Warms up resource content for static resources.
    /// </summary>
    Task WarmupResourcesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of cache warmup service.
/// </summary>
public class CacheWarmupService : ICacheWarmupService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CacheWarmupService> _logger;
    private readonly CacheWarmupOptions _options;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CacheWarmupService"/> class.
    /// </summary>
    public CacheWarmupService(
        IServiceProvider serviceProvider,
        ICacheService cacheService,
        ILogger<CacheWarmupService> logger,
        IOptions<CacheWarmupOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new CacheWarmupOptions();
    }
    
    /// <inheritdoc/>
    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Cache warmup is disabled");
            return;
        }
        
        _logger.LogInformation("Starting cache warmup");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var tasks = new List<Task>();
            
            if (_options.WarmupTools)
            {
                tasks.Add(WarmupToolsAsync(cancellationToken));
            }
            
            if (_options.WarmupResources)
            {
                tasks.Add(WarmupResourcesAsync(cancellationToken));
            }
            
            await Task.WhenAll(tasks);
            
            _logger.LogInformation("Cache warmup completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache warmup");
        }
        finally
        {
            stopwatch.Stop();
        }
    }
    
    /// <inheritdoc/>
    public async Task WarmupToolsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var toolRegistry = _serviceProvider.GetService<IToolRegistry>();
            if (toolRegistry == null)
            {
                _logger.LogWarning("Tool registry not available for cache warmup");
                return;
            }
            
            var tools = toolRegistry.GetTools();
            _logger.LogDebug("Warming up {ToolCount} tools", tools.Count);
            
            var warmupTasks = new List<Task>();
            
            foreach (var tool in tools.Values)
            {
                // Only warmup tools that have sample data
                if (_options.ToolSampleData.TryGetValue(tool.Name, out var sampleData))
                {
                    warmupTasks.Add(WarmupToolAsync(tool, sampleData, cancellationToken));
                }
            }
            
            await Task.WhenAll(warmupTasks);
            _logger.LogDebug("Tool warmup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during tool cache warmup");
        }
    }
    
    /// <inheritdoc/>
    public async Task WarmupResourcesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var resourceProviders = _serviceProvider.GetServices<IResourceProvider>();
            if (!resourceProviders.Any())
            {
                _logger.LogWarning("No resource providers available for cache warmup");
                return;
            }
            
            var warmupTasks = new List<Task>();
            
            foreach (var provider in resourceProviders)
            {
                warmupTasks.Add(WarmupResourceProviderAsync(provider, cancellationToken));
            }
            
            await Task.WhenAll(warmupTasks);
            _logger.LogDebug("Resource warmup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during resource cache warmup");
        }
    }
    
    private async Task WarmupToolAsync(ITool tool, Dictionary<string, object> sampleData, CancellationToken cancellationToken)
    {
        try
        {
            var request = new ToolRequest
            {
                Name = tool.Name,
                Arguments = sampleData.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value)
            };
            
            // Execute tool to cache the result
            await tool.ExecuteAsync(request, cancellationToken);
            _logger.LogTrace("Warmed up tool: {ToolName}", tool.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to warm up tool: {ToolName}", tool.Name);
        }
    }
    
    private async Task WarmupResourceProviderAsync(IResourceProvider provider, CancellationToken cancellationToken)
    {
        try
        {
            // Get resource list to cache it
            var resources = await provider.ListResourcesAsync(cancellationToken);
            
            var resourceList = resources.ToList();
            _logger.LogTrace("Warmed up resource list from {ProviderType}: {ResourceCount} resources", 
                provider.GetType().Name, resourceList.Count);
            
            // Warmup specific resources if configured
            var warmupTasks = new List<Task>();
            
            foreach (var resource in resourceList.Take(_options.MaxResourcesPerProvider))
            {
                // Only warmup resources that match warmup patterns
                if (ShouldWarmupResource(resource.Uri))
                {
                    warmupTasks.Add(WarmupResourceAsync(provider, resource.Uri, cancellationToken));
                }
            }
            
            await Task.WhenAll(warmupTasks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to warm up resource provider: {ProviderType}", 
                provider.GetType().Name);
        }
    }
    
    private async Task WarmupResourceAsync(IResourceProvider provider, string uri, CancellationToken cancellationToken)
    {
        try
        {
            await provider.ReadResourceAsync(uri, cancellationToken);
            _logger.LogTrace("Warmed up resource: {Uri}", uri);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to warm up resource: {Uri}", uri);
        }
    }
    
    private bool ShouldWarmupResource(string uri)
    {
        // Check if URI matches warmup patterns
        return _options.ResourceWarmupPatterns.Any(pattern => 
            uri.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Hosted service for cache warmup.
/// </summary>
public class CacheWarmupHostedService : BackgroundService
{
    private readonly ICacheWarmupService _warmupService;
    private readonly ILogger<CacheWarmupHostedService> _logger;
    private readonly CacheWarmupOptions _options;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CacheWarmupHostedService"/> class.
    /// </summary>
    public CacheWarmupHostedService(
        ICacheWarmupService warmupService,
        ILogger<CacheWarmupHostedService> logger,
        IOptions<CacheWarmupOptions> options)
    {
        _warmupService = warmupService ?? throw new ArgumentNullException(nameof(warmupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new CacheWarmupOptions();
    }
    
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }
        
        // Initial warmup delay
        await Task.Delay(_options.InitialDelay, stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _warmupService.WarmupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled cache warmup");
            }
            
            // Wait for next warmup cycle
            await Task.Delay(_options.WarmupInterval, stoppingToken);
        }
    }
}

/// <summary>
/// Configuration options for cache warmup.
/// </summary>
public class CacheWarmupOptions
{
    /// <summary>
    /// Gets or sets whether cache warmup is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to warmup tools.
    /// </summary>
    public bool WarmupTools { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to warmup resources.
    /// </summary>
    public bool WarmupResources { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the initial delay before first warmup.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Gets or sets the interval between warmup cycles.
    /// </summary>
    public TimeSpan WarmupInterval { get; set; } = TimeSpan.FromHours(1);
    
    /// <summary>
    /// Gets or sets the maximum number of resources to warmup per provider.
    /// </summary>
    public int MaxResourcesPerProvider { get; set; } = 10;
    
    /// <summary>
    /// Gets or sets sample data for warming up tools.
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> ToolSampleData { get; set; } = new()
    {
        {
            "EchoTool",
            new Dictionary<string, object> { { "message", "warmup" } }
        },
        {
            "CalculatorTool",
            new Dictionary<string, object> { { "expression", "2 + 2" } }
        },
        {
            "DateTimeTool",
            new Dictionary<string, object> { { "format", "yyyy-MM-dd" } }
        }
    };
    
    /// <summary>
    /// Gets or sets patterns for resources to warmup.
    /// </summary>
    public List<string> ResourceWarmupPatterns { get; set; } = new()
    {
        ".json",
        ".config",
        "schema",
        "readme"
    };
}