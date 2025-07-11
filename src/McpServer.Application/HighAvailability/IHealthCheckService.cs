using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace McpServer.Application.HighAvailability;

/// <summary>
/// Service for comprehensive health monitoring of MCP server components.
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Performs a comprehensive health check of all server components.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Overall health status and detailed component results.</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks the health of a specific component.
    /// </summary>
    /// <param name="componentName">Name of the component to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health status of the specific component.</returns>
    Task<HealthCheckResult> CheckComponentHealthAsync(string componentName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current health status of all components.
    /// </summary>
    /// <returns>Dictionary of component names and their health status.</returns>
    Task<Dictionary<string, HealthCheckResult>> GetAllComponentHealthAsync();
    
    /// <summary>
    /// Registers a custom health check.
    /// </summary>
    /// <param name="name">Name of the health check.</param>
    /// <param name="healthCheck">Health check implementation.</param>
    /// <param name="tags">Optional tags for categorization.</param>
    void RegisterHealthCheck(string name, IHealthCheck healthCheck, params string[] tags);
}

/// <summary>
/// Detailed health check result with component information.
/// </summary>
public class ComponentHealthCheckResult
{
    /// <summary>
    /// Gets the component name.
    /// </summary>
    public string ComponentName { get; init; } = string.Empty;
    
    /// <summary>
    /// Gets the health status.
    /// </summary>
    public HealthStatus Status { get; init; } = HealthStatus.Healthy;
    
    /// <summary>
    /// Gets the description.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Gets the response time.
    /// </summary>
    public TimeSpan ResponseTime { get; init; }
    
    /// <summary>
    /// Gets the error message if any.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Gets additional data.
    /// </summary>
    public Dictionary<string, object>? Data { get; init; }
    
    /// <summary>
    /// Gets the component type.
    /// </summary>
    public string ComponentType { get; init; } = string.Empty;
    
    /// <summary>
    /// Gets the last check timestamp.
    /// </summary>
    public DateTimeOffset LastChecked { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Gets any error details.
    /// </summary>
    public string? ErrorDetails { get; init; }
    
    /// <summary>
    /// Gets additional metrics.
    /// </summary>
    public Dictionary<string, object> Metrics { get; init; } = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentHealthCheckResult"/> class.
    /// </summary>
    public ComponentHealthCheckResult()
    {
    }
}

/// <summary>
/// Health check for transport connectivity.
/// </summary>
public interface ITransportHealthCheck : IHealthCheck
{
    /// <summary>
    /// Gets the transport type being monitored.
    /// </summary>
    string TransportType { get; }
    
    /// <summary>
    /// Checks if the transport can accept new connections.
    /// </summary>
    Task<bool> CanAcceptConnectionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health check for database connectivity.
/// </summary>
public interface IDatabaseHealthCheck : IHealthCheck
{
    /// <summary>
    /// Gets the database connection string (masked).
    /// </summary>
    string ConnectionInfo { get; }
    
    /// <summary>
    /// Checks database connectivity with a simple query.
    /// </summary>
    Task<TimeSpan> CheckConnectionLatencyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health check for cache systems.
/// </summary>
public interface ICacheHealthCheck : IHealthCheck
{
    /// <summary>
    /// Gets the cache type being monitored.
    /// </summary>
    string CacheType { get; }
    
    /// <summary>
    /// Gets current cache statistics.
    /// </summary>
    Task<Dictionary<string, object>> GetCacheStatsAsync(CancellationToken cancellationToken = default);
}