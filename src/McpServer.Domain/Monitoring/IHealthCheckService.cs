namespace McpServer.Domain.Monitoring;

/// <summary>
/// Service for performing health checks.
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Performs a comprehensive health check.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The health check result.</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a specific health check.
    /// </summary>
    /// <param name="checkName">The check name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The health check result.</returns>
    Task<ComponentHealthResult> CheckComponentAsync(string checkName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a health check.
    /// </summary>
    /// <param name="name">The check name.</param>
    /// <param name="check">The health check function.</param>
    void RegisterCheck(string name, Func<CancellationToken, Task<ComponentHealthResult>> check);

    /// <summary>
    /// Unregisters a health check.
    /// </summary>
    /// <param name="name">The check name.</param>
    void UnregisterCheck(string name);
}

/// <summary>
/// Overall health check result.
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Gets or sets the overall status.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the duration of the health check.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets component health results.
    /// </summary>
    public Dictionary<string, ComponentHealthResult> Components { get; set; } = new();

    /// <summary>
    /// Gets or sets system information.
    /// </summary>
    public SystemInfo System { get; set; } = new();

    /// <summary>
    /// Gets whether the service is healthy.
    /// </summary>
    public bool IsHealthy => Status == HealthStatus.Healthy;
}

/// <summary>
/// Component health check result.
/// </summary>
public class ComponentHealthResult
{
    /// <summary>
    /// Gets or sets the component name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the response time.
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets error details.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets additional data.
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Health status.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Service is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Service is degraded but operational.
    /// </summary>
    Degraded,

    /// <summary>
    /// Service is unhealthy.
    /// </summary>
    Unhealthy
}

/// <summary>
/// System information.
/// </summary>
public class SystemInfo
{
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the environment.
    /// </summary>
    public string Environment { get; set; } = "Production";

    /// <summary>
    /// Gets or sets the hostname.
    /// </summary>
    public string Hostname { get; set; } = System.Environment.MachineName;

    /// <summary>
    /// Gets or sets memory usage.
    /// </summary>
    public MemoryInfo Memory { get; set; } = new();

    /// <summary>
    /// Gets or sets CPU information.
    /// </summary>
    public CpuInfo Cpu { get; set; } = new();
}

/// <summary>
/// Memory information.
/// </summary>
public class MemoryInfo
{
    /// <summary>
    /// Gets or sets used memory in bytes.
    /// </summary>
    public long UsedBytes { get; set; }

    /// <summary>
    /// Gets or sets total memory in bytes.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets available memory in bytes.
    /// </summary>
    public long AvailableBytes { get; set; }

    /// <summary>
    /// Gets the usage percentage.
    /// </summary>
    public double UsagePercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
}

/// <summary>
/// CPU information.
/// </summary>
public class CpuInfo
{
    /// <summary>
    /// Gets or sets CPU usage percentage.
    /// </summary>
    public double UsagePercent { get; set; }

    /// <summary>
    /// Gets or sets processor count.
    /// </summary>
    public int ProcessorCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets process CPU time.
    /// </summary>
    public TimeSpan ProcessCpuTime { get; set; }
}