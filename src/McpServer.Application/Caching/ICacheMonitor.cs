namespace McpServer.Application.Caching;

/// <summary>
/// Cache statistics and monitoring operations.
/// </summary>
public interface ICacheMonitor
{
    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Current cache statistics.</returns>
    CacheStatistics GetStatistics();
}