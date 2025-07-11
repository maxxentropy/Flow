using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Application.Caching;

/// <summary>
/// In-memory implementation of the cache service.
/// </summary>
public partial class MemoryCacheService : ICacheService, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly MemoryCacheOptions _options;
    private readonly ConcurrentDictionary<string, CacheEntryMetadata> _metadata = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    
    private long _hitCount;
    private long _missCount;
    private long _evictionCount;
    private readonly ConcurrentDictionary<EvictionReason, long> _evictionsByReason = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryCacheService"/> class.
    /// </summary>
    public MemoryCacheService(ILogger<MemoryCacheService> logger, IOptions<MemoryCacheOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new MemoryCacheOptions();
        
        _cache = new MemoryCache(Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions
        {
            SizeLimit = _options.SizeLimit,
            CompactionPercentage = _options.CompactionPercentage,
            ExpirationScanFrequency = _options.ExpirationScanFrequency
        }));
        
        _logger.LogInformation("Memory cache initialized with size limit: {SizeLimit}", _options.SizeLimit);
    }
    
    /// <inheritdoc/>
    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
    {
        if (_cache.TryGetValue(key, out var cachedValue))
        {
            Interlocked.Increment(ref _hitCount);
            value = (T)cachedValue!;
            _logger.LogTrace("Cache hit for key: {Key}", key);
            return true;
        }
        
        Interlocked.Increment(ref _missCount);
        value = default;
        _logger.LogTrace("Cache miss for key: {Key}", key);
        return false;
    }
    
    /// <inheritdoc/>
    public Task<(bool found, T? value)> TryGetValueAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var found = TryGetValue<T>(key, out var value);
        return Task.FromResult((found, value));
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
        
        using var entry = _cache.CreateEntry(key);
        
        entry.Value = value;
        entry.Priority = ConvertPriority(options.Priority);
        
        if (options.AbsoluteExpiration.HasValue)
        {
            entry.AbsoluteExpiration = options.AbsoluteExpiration.Value;
        }
        
        if (options.SlidingExpiration.HasValue)
        {
            entry.SlidingExpiration = options.SlidingExpiration.Value;
        }
        
        if (options.Size.HasValue)
        {
            entry.Size = options.Size.Value;
        }
        
        // Set up eviction callback
        entry.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = (k, v, r, s) => OnCacheEntryEvicted(k, v, r, s),
            State = (key, options.Callbacks)
        });
        
        // Track metadata
        _metadata[key] = new CacheEntryMetadata
        {
            Key = key,
            CreatedAt = DateTimeOffset.UtcNow,
            Size = options.Size ?? EstimateSize(value),
            Priority = options.Priority
        };
        
        _logger.LogDebug("Cached value for key: {Key} with priority: {Priority}", key, options.Priority);
    }
    
    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
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
        if (TryGetValue<T>(key, out var value))
        {
            return value;
        }
        
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (TryGetValue<T>(key, out value))
            {
                return value;
            }
            
            value = await factory();
            Set(key, value, options);
            return value;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
    
    /// <inheritdoc/>
    public bool Remove(string key)
    {
        _cache.Remove(key);
        var removed = _metadata.TryRemove(key, out _);
        
        if (removed)
        {
            _logger.LogDebug("Removed cache entry: {Key}", key);
        }
        
        return removed;
    }
    
    /// <inheritdoc/>
    public int RemoveByPattern(string pattern)
    {
        var regex = ConvertPatternToRegex(pattern);
        var keysToRemove = _metadata.Keys.Where(k => regex.IsMatch(k)).ToList();
        
        foreach (var key in keysToRemove)
        {
            Remove(key);
        }
        
        _logger.LogInformation("Removed {Count} cache entries matching pattern: {Pattern}", keysToRemove.Count, pattern);
        return keysToRemove.Count;
    }
    
    /// <inheritdoc/>
    public void Clear()
    {
        // Clear all entries from the cache
        foreach (var key in _metadata.Keys.ToList())
        {
            _cache.Remove(key);
        }
        
        _metadata.Clear();
        Interlocked.Exchange(ref _hitCount, 0);
        Interlocked.Exchange(ref _missCount, 0);
        Interlocked.Exchange(ref _evictionCount, 0);
        _evictionsByReason.Clear();
        
        _logger.LogInformation("Cache cleared");
    }
    
    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        var totalSize = _metadata.Values.Sum(m => m.Size);
        
        return new CacheStatistics
        {
            EntryCount = _metadata.Count,
            TotalSize = totalSize,
            HitCount = Interlocked.Read(ref _hitCount),
            MissCount = Interlocked.Read(ref _missCount),
            EvictionCount = Interlocked.Read(ref _evictionCount),
            EvictionsByReason = _evictionsByReason.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }
    
    /// <summary>
    /// Disposes the cache service.
    /// </summary>
    public void Dispose()
    {
        _cacheLock?.Dispose();
        if (_cache is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
    
    private void OnCacheEntryEvicted(object key, object? value, Microsoft.Extensions.Caching.Memory.EvictionReason reason, object? state)
    {
        var keyString = key.ToString()!;
        _metadata.TryRemove(keyString, out _);
        
        Interlocked.Increment(ref _evictionCount);
        _evictionsByReason.AddOrUpdate((EvictionReason)reason, 1, (_, count) => count + 1);
        
        if (state is (string k, CacheEntryCallbacks callbacks))
        {
            callbacks?.OnEviction?.Invoke(k, value, (EvictionReason)reason);
        }
        
        _logger.LogDebug("Cache entry evicted: {Key}, Reason: {Reason}", keyString, reason);
    }
    
    private static CacheItemPriority ConvertPriority(CachePriority priority)
    {
        return priority switch
        {
            CachePriority.Low => CacheItemPriority.Low,
            CachePriority.Normal => CacheItemPriority.Normal,
            CachePriority.High => CacheItemPriority.High,
            CachePriority.NeverRemove => CacheItemPriority.NeverRemove,
            _ => CacheItemPriority.Normal
        };
    }
    
    private static long EstimateSize<T>(T value)
    {
        // Simple size estimation - can be improved
        return value switch
        {
            string s => s.Length * 2, // Unicode chars
            byte[] b => b.Length,
            _ => 64 // Default object size
        };
    }
    
    private static Regex ConvertPatternToRegex(string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
    
    private class CacheEntryMetadata
    {
        public required string Key { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required long Size { get; init; }
        public required CachePriority Priority { get; init; }
    }
}

/// <summary>
/// Configuration options for memory cache.
/// </summary>
public class MemoryCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum size of the cache.
    /// </summary>
    public long? SizeLimit { get; set; } = 1024 * 1024 * 100; // 100MB default
    
    /// <summary>
    /// Gets or sets the amount to compact the cache by when the maximum size is exceeded.
    /// </summary>
    public double CompactionPercentage { get; set; } = 0.05;
    
    /// <summary>
    /// Gets or sets how often expired items are removed from the cache.
    /// </summary>
    public TimeSpan ExpirationScanFrequency { get; set; } = TimeSpan.FromMinutes(1);
}