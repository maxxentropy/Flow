namespace McpServer.Application.Caching;

/// <summary>
/// Service for caching data with various expiration policies, composed of segregated interfaces.
/// </summary>
public interface ICacheService : ICacheProvider, ICacheManager, ICacheMonitor
{
    // Composite interface - all methods are inherited from segregated interfaces
}

/// <summary>
/// Options for cache entries.
/// </summary>
public class CacheEntryOptions
{
    /// <summary>
    /// Gets or sets the absolute expiration time.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }
    
    /// <summary>
    /// Gets or sets the sliding expiration time.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }
    
    /// <summary>
    /// Gets or sets the priority for cache eviction.
    /// </summary>
    public CachePriority Priority { get; set; } = CachePriority.Normal;
    
    /// <summary>
    /// Gets or sets the size of the cache entry.
    /// </summary>
    public long? Size { get; set; }
    
    /// <summary>
    /// Gets or sets callbacks for cache entry events.
    /// </summary>
    public CacheEntryCallbacks? Callbacks { get; set; }
}

/// <summary>
/// Cache entry priority levels.
/// </summary>
public enum CachePriority
{
    /// <summary>
    /// Low priority - evicted first.
    /// </summary>
    Low,
    
    /// <summary>
    /// Normal priority.
    /// </summary>
    Normal,
    
    /// <summary>
    /// High priority - evicted last.
    /// </summary>
    High,
    
    /// <summary>
    /// Never evict automatically.
    /// </summary>
    NeverRemove
}

/// <summary>
/// Callbacks for cache entry lifecycle events.
/// </summary>
public class CacheEntryCallbacks
{
    /// <summary>
    /// Called when the entry is evicted from cache.
    /// </summary>
    public Action<string, object?, EvictionReason>? OnEviction { get; set; }
}

/// <summary>
/// Reasons for cache eviction.
/// </summary>
public enum EvictionReason
{
    /// <summary>
    /// Expired due to absolute expiration.
    /// </summary>
    Expired,
    
    /// <summary>
    /// Expired due to sliding expiration.
    /// </summary>
    Unused,
    
    /// <summary>
    /// Removed explicitly.
    /// </summary>
    Removed,
    
    /// <summary>
    /// Evicted due to memory pressure.
    /// </summary>
    Capacity,
    
    /// <summary>
    /// Replaced with a new value.
    /// </summary>
    Replaced
}

/// <summary>
/// Cache statistics.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Gets the total number of cache entries.
    /// </summary>
    public long EntryCount { get; init; }
    
    /// <summary>
    /// Gets the total size of cached data in bytes.
    /// </summary>
    public long TotalSize { get; init; }
    
    /// <summary>
    /// Gets the number of cache hits.
    /// </summary>
    public long HitCount { get; init; }
    
    /// <summary>
    /// Gets the number of cache misses.
    /// </summary>
    public long MissCount { get; init; }
    
    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    public double HitRatio => HitCount + MissCount == 0 ? 0 : (double)HitCount / (HitCount + MissCount);
    
    /// <summary>
    /// Gets the number of evictions.
    /// </summary>
    public long EvictionCount { get; init; }
    
    /// <summary>
    /// Gets eviction counts by reason.
    /// </summary>
    public Dictionary<EvictionReason, long> EvictionsByReason { get; init; } = new();
}