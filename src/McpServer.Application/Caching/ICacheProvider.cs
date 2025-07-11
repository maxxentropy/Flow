using System.Diagnostics.CodeAnalysis;

namespace McpServer.Application.Caching;

/// <summary>
/// Basic cache operations for storing and retrieving values.
/// </summary>
public interface ICacheProvider
{
    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The cached value, if found.</param>
    /// <returns>True if the value was found in cache.</returns>
    bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value);
    
    /// <summary>
    /// Gets a value from the cache asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached value and whether it was found.</returns>
    Task<(bool found, T? value)> TryGetValueAsync<T>(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets a value in the cache with an absolute expiration.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="absoluteExpiration">When the cache entry should expire.</param>
    void Set<T>(string key, T value, DateTimeOffset absoluteExpiration);
    
    /// <summary>
    /// Sets a value in the cache with a sliding expiration.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="slidingExpiration">How long after last access the entry should expire.</param>
    void Set<T>(string key, T value, TimeSpan slidingExpiration);
    
    /// <summary>
    /// Sets a value in the cache with cache options.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">Cache entry options.</param>
    void Set<T>(string key, T value, CacheEntryOptions options);
    
    /// <summary>
    /// Sets a value in the cache asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">Cache entry options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken cancellationToken = default);
}