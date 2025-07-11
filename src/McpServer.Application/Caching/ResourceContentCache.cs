using McpServer.Domain.Resources;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Caching;

/// <summary>
/// Caching decorator for resource content.
/// </summary>
public class ResourceContentCache : IResourceProvider
{
    private readonly IResourceProvider _innerProvider;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ResourceContentCache> _logger;
    private readonly ResourceCacheOptions _options;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceContentCache"/> class.
    /// </summary>
    public ResourceContentCache(
        IResourceProvider innerProvider,
        ICacheService cacheService,
        ILogger<ResourceContentCache> logger,
        ResourceCacheOptions options)
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.CacheResourceList)
        {
            return await _innerProvider.ListResourcesAsync(cancellationToken);
        }
        
        var cacheKey = $"resources:list:{_innerProvider.GetType().Name}";
        
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () => await _innerProvider.ListResourcesAsync(cancellationToken),
            new CacheEntryOptions
            {
                SlidingExpiration = _options.ListCacheExpiration,
                Priority = CachePriority.Low
            },
            cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        // Check if URI should be cached
        if (!ShouldCacheUri(uri))
        {
            _logger.LogTrace("Caching disabled for resource {Uri}, reading directly", uri);
            return await _innerProvider.ReadResourceAsync(uri, cancellationToken);
        }
        
        var cacheKey = GenerateCacheKey(uri);
        
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for resource {Uri}, fetching content", uri);
                var content = await _innerProvider.ReadResourceAsync(uri, cancellationToken);
                
                // Validate content size
                if (!ShouldCacheContent(content))
                {
                    _logger.LogDebug("Resource content too large to cache: {Uri}", uri);
                    throw new InvalidOperationException("Content too large to cache");
                }
                
                return content;
            },
            new CacheEntryOptions
            {
                SlidingExpiration = GetExpirationForUri(uri),
                Priority = GetPriorityForUri(uri),
                Size = EstimateContentSize(uri),
                Callbacks = new CacheEntryCallbacks
                {
                    OnEviction = (key, value, reason) =>
                    {
                        _logger.LogDebug("Resource evicted from cache: {Key}, Reason: {Reason}", key, reason);
                    }
                }
            },
            cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task SubscribeToResourceAsync(string uri, IResourceObserver observer, CancellationToken cancellationToken = default)
    {
        // Pass through subscriptions - don't cache dynamic content
        await _innerProvider.SubscribeToResourceAsync(uri, observer, cancellationToken);
        
        // Invalidate cache when resource changes
        var wrappedObserver = new CacheInvalidatingObserver(observer, uri, _cacheService, _logger);
        await _innerProvider.SubscribeToResourceAsync(uri, wrappedObserver, cancellationToken);
    }
    
    /// <inheritdoc/>
    public async Task UnsubscribeFromResourceAsync(string uri, IResourceObserver observer, CancellationToken cancellationToken = default)
    {
        await _innerProvider.UnsubscribeFromResourceAsync(uri, observer, cancellationToken);
    }
    
    private bool ShouldCacheUri(string uri)
    {
        // Check exclude patterns
        foreach (var pattern in _options.ExcludePatterns)
        {
            if (uri.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        
        // Check include patterns
        if (_options.IncludePatterns.Any())
        {
            return _options.IncludePatterns.Any(pattern => 
                uri.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
        
        return _options.Enabled;
    }
    
    private bool ShouldCacheContent(ResourceContent content)
    {
        // Estimate size
        long size = 0;
        
        if (content.Text != null)
        {
            size += content.Text.Length * 2; // Unicode
        }
        
        if (content.Blob != null)
        {
            size += content.Blob.Length;
        }
        
        return size <= _options.MaxContentSize;
    }
    
    private string GenerateCacheKey(string uri)
    {
        // Normalize URI for consistent caching
        var normalizedUri = uri.Replace('\\', '/').ToLowerInvariant();
        return $"resource:{normalizedUri}";
    }
    
    private TimeSpan GetExpirationForUri(string uri)
    {
        // Check if URI matches any pattern with custom expiration
        foreach (var (pattern, expiration) in _options.CustomExpirations)
        {
            if (uri.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return expiration;
            }
        }
        
        return _options.DefaultExpiration;
    }
    
    private CachePriority GetPriorityForUri(string uri)
    {
        // Static resources get higher priority
        if (uri.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            uri.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
            uri.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            return CachePriority.High;
        }
        
        return CachePriority.Normal;
    }
    
    private long EstimateContentSize(string uri)
    {
        // Simple estimation based on file extension
        if (uri.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            uri.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return 10 * 1024; // 10KB average
        }
        
        return 50 * 1024; // 50KB default
    }
    
    private class CacheInvalidatingObserver : IResourceObserver
    {
        private readonly IResourceObserver _innerObserver;
        private readonly string _uri;
        private readonly ICacheService _cacheService;
        private readonly ILogger _logger;
        
        public CacheInvalidatingObserver(
            IResourceObserver innerObserver,
            string uri,
            ICacheService cacheService,
            ILogger logger)
        {
            _innerObserver = innerObserver;
            _uri = uri;
            _cacheService = cacheService;
            _logger = logger;
        }
        
        public Task OnResourceUpdatedAsync(string uri, CancellationToken cancellationToken = default)
        {
            // Invalidate cache
            var cacheKey = $"resource:{uri.Replace('\\', '/').ToLowerInvariant()}";
            _cacheService.Remove(cacheKey);
            _logger.LogDebug("Invalidated cache for updated resource: {Uri}", uri);
            
            // Forward to inner observer
            return _innerObserver.OnResourceUpdatedAsync(uri, cancellationToken);
        }
    }
}

/// <summary>
/// Configuration options for resource caching.
/// </summary>
public class ResourceCacheOptions
{
    /// <summary>
    /// Gets or sets whether caching is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to cache resource lists.
    /// </summary>
    public bool CacheResourceList { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the default cache expiration.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(15);
    
    /// <summary>
    /// Gets or sets the resource list cache expiration.
    /// </summary>
    public TimeSpan ListCacheExpiration { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Gets or sets the maximum content size to cache.
    /// </summary>
    public long MaxContentSize { get; set; } = 5 * 1024 * 1024; // 5MB
    
    /// <summary>
    /// Gets or sets URI patterns to exclude from caching.
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new()
    {
        "/temp/",
        "/tmp/",
        ".log"
    };
    
    /// <summary>
    /// Gets or sets URI patterns to include in caching (if specified, only these are cached).
    /// </summary>
    public List<string> IncludePatterns { get; set; } = new();
    
    /// <summary>
    /// Gets or sets custom expirations for specific URI patterns.
    /// </summary>
    public Dictionary<string, TimeSpan> CustomExpirations { get; set; } = new()
    {
        { ".json", TimeSpan.FromMinutes(30) },
        { ".config", TimeSpan.FromMinutes(30) },
        { "schema", TimeSpan.FromHours(1) }
    };
}