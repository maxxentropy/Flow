using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Application.Caching;

/// <summary>
/// Distributed cache implementation of the cache service.
/// </summary>
public class DistributedCacheService : ICacheService, IDisposable
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly DistributedCacheOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private long _hitCount;
    private long _missCount;
    private long _evictionCount;
    private readonly object _statsLock = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheService"/> class.
    /// </summary>
    public DistributedCacheService(
        IDistributedCache distributedCache,
        ILogger<DistributedCacheService> logger,
        IOptions<DistributedCacheOptions> options)
    {
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new DistributedCacheOptions();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        _logger.LogInformation("Distributed cache initialized with key prefix: {KeyPrefix}", _options.KeyPrefix);
    }
    
    /// <inheritdoc/>
    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
    {
        var fullKey = GetFullKey(key);
        
        try
        {
            var cachedData = _distributedCache.GetString(fullKey);
            
            if (cachedData != null)
            {
                value = JsonSerializer.Deserialize<T>(cachedData, _jsonOptions)!;
                Interlocked.Increment(ref _hitCount);
                _logger.LogTrace("Distributed cache hit for key: {Key}", key);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving value from distributed cache for key: {Key}", key);
        }
        
        value = default;
        Interlocked.Increment(ref _missCount);
        _logger.LogTrace("Distributed cache miss for key: {Key}", key);
        return false;
    }
    
    /// <inheritdoc/>
    public async Task<(bool found, T? value)> TryGetValueAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = GetFullKey(key);
        
        try
        {
            var cachedData = await _distributedCache.GetStringAsync(fullKey, cancellationToken);
            
            if (cachedData != null)
            {
                var value = JsonSerializer.Deserialize<T>(cachedData, _jsonOptions)!;
                Interlocked.Increment(ref _hitCount);
                _logger.LogTrace("Distributed cache hit for key: {Key}", key);
                return (true, value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving value from distributed cache for key: {Key}", key);
        }
        
        Interlocked.Increment(ref _missCount);
        _logger.LogTrace("Distributed cache miss for key: {Key}", key);
        return (false, default);
    }
    
    /// <inheritdoc/>
    public void Set<T>(string key, T value, DateTimeOffset absoluteExpiration)
    {
        var options = new CacheEntryOptions
        {
            AbsoluteExpiration = absoluteExpiration
        };
        Set(key, value, options);
    }
    
    /// <inheritdoc/>
    public void Set<T>(string key, T value, TimeSpan slidingExpiration)
    {
        var options = new CacheEntryOptions
        {
            SlidingExpiration = slidingExpiration
        };
        Set(key, value, options);
    }
    
    /// <inheritdoc/>
    public void Set<T>(string key, T value, CacheEntryOptions options)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));
        
        var fullKey = GetFullKey(key);
        
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var distributedOptions = new DistributedCacheEntryOptions();
            
            if (options.AbsoluteExpiration.HasValue)
            {
                distributedOptions.SetAbsoluteExpiration(options.AbsoluteExpiration.Value);
            }
            
            if (options.SlidingExpiration.HasValue)
            {
                distributedOptions.SetSlidingExpiration(options.SlidingExpiration.Value);
            }
            
            // Set default expiration if none provided
            if (!options.AbsoluteExpiration.HasValue && !options.SlidingExpiration.HasValue)
            {
                distributedOptions.SetSlidingExpiration(_options.DefaultExpiration);
            }
            
            _distributedCache.SetString(fullKey, json, distributedOptions);
            _logger.LogDebug("Set value in distributed cache for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in distributed cache for key: {Key}", key);
        }
    }
    
    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));
        
        var fullKey = GetFullKey(key);
        
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var distributedOptions = new DistributedCacheEntryOptions();
            
            if (options.AbsoluteExpiration.HasValue)
            {
                distributedOptions.SetAbsoluteExpiration(options.AbsoluteExpiration.Value);
            }
            
            if (options.SlidingExpiration.HasValue)
            {
                distributedOptions.SetSlidingExpiration(options.SlidingExpiration.Value);
            }
            
            // Set default expiration if none provided
            if (!options.AbsoluteExpiration.HasValue && !options.SlidingExpiration.HasValue)
            {
                distributedOptions.SetSlidingExpiration(_options.DefaultExpiration);
            }
            
            await _distributedCache.SetStringAsync(fullKey, json, distributedOptions, cancellationToken);
            _logger.LogDebug("Set value in distributed cache for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in distributed cache for key: {Key}", key);
        }
    }
    
    /// <inheritdoc/>
    public T GetOrCreate<T>(string key, Func<T> factory, CacheEntryOptions options)
    {
        if (TryGetValue<T>(key, out var value))
        {
            return value;
        }
        
        value = factory();
        Set(key, value, options);
        return value;
    }
    
    /// <inheritdoc/>
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        var (found, value) = await TryGetValueAsync<T>(key, cancellationToken);
        if (found)
        {
            return value!;
        }
        
        value = await factory();
        await SetAsync(key, value, options, cancellationToken);
        return value;
    }
    
    /// <inheritdoc/>
    public bool Remove(string key)
    {
        var fullKey = GetFullKey(key);
        
        try
        {
            _distributedCache.Remove(fullKey);
            _logger.LogDebug("Removed value from distributed cache for key: {Key}", key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from distributed cache for key: {Key}", key);
            return false;
        }
    }
    
    /// <inheritdoc/>
    public int RemoveByPattern(string pattern)
    {
        // Note: Pattern removal is not natively supported by IDistributedCache
        // This would require Redis-specific implementation or key tracking
        _logger.LogWarning("RemoveByPattern is not fully supported by distributed cache. Pattern: {Pattern}", pattern);
        return 0;
    }
    
    /// <inheritdoc/>
    public void Clear()
    {
        // Note: Clear is not natively supported by IDistributedCache
        // This would require Redis-specific implementation or key tracking
        _logger.LogWarning("Clear is not fully supported by distributed cache");
        
        // Reset local statistics
        lock (_statsLock)
        {
            _hitCount = 0;
            _missCount = 0;
            _evictionCount = 0;
        }
    }
    
    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new CacheStatistics
            {
                EntryCount = -1, // Not available in distributed cache
                TotalSize = -1, // Not available in distributed cache
                HitCount = _hitCount,
                MissCount = _missCount,
                EvictionCount = _evictionCount,
                EvictionsByReason = new Dictionary<EvictionReason, long>()
            };
        }
    }
    
    /// <summary>
    /// Disposes the cache service.
    /// </summary>
    public void Dispose()
    {
        // IDistributedCache implementations handle their own disposal
        GC.SuppressFinalize(this);
    }
    
    private string GetFullKey(string key)
    {
        return string.IsNullOrEmpty(_options.KeyPrefix) ? key : $"{_options.KeyPrefix}:{key}";
    }
}

/// <summary>
/// Configuration options for distributed cache.
/// </summary>
public class DistributedCacheOptions
{
    /// <summary>
    /// Gets or sets the key prefix for all cache keys.
    /// </summary>
    public string KeyPrefix { get; set; } = "mcp";
    
    /// <summary>
    /// Gets or sets the default cache expiration.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(15);
}