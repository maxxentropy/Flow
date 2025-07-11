namespace McpServer.Application.Caching;

/// <summary>
/// Advanced cache management operations.
/// </summary>
public interface ICacheManager
{
    /// <summary>
    /// Gets or creates a cached value.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory to create the value if not in cache.</param>
    /// <param name="options">Cache entry options.</param>
    /// <returns>The cached or newly created value.</returns>
    T GetOrCreate<T>(string key, Func<T> factory, CacheEntryOptions options);
    
    /// <summary>
    /// Gets or creates a cached value asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory to create the value if not in cache.</param>
    /// <param name="options">Cache entry options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or newly created value.</returns>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>True if the value was removed.</returns>
    bool Remove(string key);
    
    /// <summary>
    /// Removes values from the cache by pattern.
    /// </summary>
    /// <param name="pattern">The pattern to match keys (supports * and ? wildcards).</param>
    /// <returns>Number of entries removed.</returns>
    int RemoveByPattern(string pattern);
    
    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    void Clear();
}