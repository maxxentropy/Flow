namespace McpServer.Domain.Monitoring;

/// <summary>
/// Service for collecting and reporting performance metrics.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Records a request metric.
    /// </summary>
    /// <param name="method">The request method.</param>
    /// <param name="duration">The request duration.</param>
    /// <param name="success">Whether the request was successful.</param>
    /// <param name="metadata">Additional metadata.</param>
    void RecordRequest(string method, TimeSpan duration, bool success, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Records a tool execution metric.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="success">Whether the execution was successful.</param>
    void RecordToolExecution(string toolName, TimeSpan duration, bool success);

    /// <summary>
    /// Records a resource access metric.
    /// </summary>
    /// <param name="resourceUri">The resource URI.</param>
    /// <param name="operation">The operation performed.</param>
    /// <param name="duration">The operation duration.</param>
    /// <param name="success">Whether the operation was successful.</param>
    void RecordResourceAccess(string resourceUri, string operation, TimeSpan duration, bool success);

    /// <summary>
    /// Records an authentication metric.
    /// </summary>
    /// <param name="provider">The authentication provider.</param>
    /// <param name="success">Whether authentication was successful.</param>
    /// <param name="duration">The authentication duration.</param>
    void RecordAuthentication(string provider, bool success, TimeSpan duration);

    /// <summary>
    /// Records a custom metric.
    /// </summary>
    /// <param name="name">The metric name.</param>
    /// <param name="value">The metric value.</param>
    /// <param name="tags">Optional tags.</param>
    void RecordMetric(string name, double value, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Increments a counter.
    /// </summary>
    /// <param name="name">The counter name.</param>
    /// <param name="tags">Optional tags.</param>
    /// <param name="increment">The increment value.</param>
    void IncrementCounter(string name, Dictionary<string, string>? tags = null, long increment = 1);

    /// <summary>
    /// Records a gauge value.
    /// </summary>
    /// <param name="name">The gauge name.</param>
    /// <param name="value">The gauge value.</param>
    /// <param name="tags">Optional tags.</param>
    void RecordGauge(string name, double value, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Gets current metrics snapshot.
    /// </summary>
    /// <returns>The metrics snapshot.</returns>
    MetricsSnapshot GetSnapshot();

    /// <summary>
    /// Gets metrics for a specific time range.
    /// </summary>
    /// <param name="startTime">The start time.</param>
    /// <param name="endTime">The end time.</param>
    /// <returns>The metrics for the time range.</returns>
    MetricsSnapshot GetSnapshot(DateTime startTime, DateTime endTime);

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Represents a snapshot of metrics.
/// </summary>
public class MetricsSnapshot
{
    /// <summary>
    /// Gets or sets the snapshot timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the duration this snapshot covers.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets request metrics.
    /// </summary>
    public RequestMetrics Requests { get; set; } = new();

    /// <summary>
    /// Gets or sets tool metrics.
    /// </summary>
    public ToolMetrics Tools { get; set; } = new();

    /// <summary>
    /// Gets or sets resource metrics.
    /// </summary>
    public ResourceMetrics Resources { get; set; } = new();

    /// <summary>
    /// Gets or sets authentication metrics.
    /// </summary>
    public AuthenticationMetrics Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets system metrics.
    /// </summary>
    public SystemMetrics System { get; set; } = new();

    /// <summary>
    /// Gets or sets custom metrics.
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Request metrics.
/// </summary>
public class RequestMetrics
{
    /// <summary>
    /// Gets or sets total request count.
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Gets or sets successful request count.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets failed request count.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Gets or sets average response time.
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets minimum response time.
    /// </summary>
    public TimeSpan MinResponseTime { get; set; }

    /// <summary>
    /// Gets or sets maximum response time.
    /// </summary>
    public TimeSpan MaxResponseTime { get; set; }

    /// <summary>
    /// Gets or sets response time percentiles.
    /// </summary>
    public ResponseTimePercentiles Percentiles { get; set; } = new();

    /// <summary>
    /// Gets or sets request counts by method.
    /// </summary>
    public Dictionary<string, long> ByMethod { get; set; } = new();
}

/// <summary>
/// Response time percentiles.
/// </summary>
public class ResponseTimePercentiles
{
    /// <summary>
    /// Gets or sets the 50th percentile (median).
    /// </summary>
    public TimeSpan P50 { get; set; }

    /// <summary>
    /// Gets or sets the 90th percentile.
    /// </summary>
    public TimeSpan P90 { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile.
    /// </summary>
    public TimeSpan P95 { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile.
    /// </summary>
    public TimeSpan P99 { get; set; }
}

/// <summary>
/// Tool execution metrics.
/// </summary>
public class ToolMetrics
{
    /// <summary>
    /// Gets or sets total execution count.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets successful execution count.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets failed execution count.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Gets or sets average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets execution counts by tool.
    /// </summary>
    public Dictionary<string, ToolExecutionMetrics> ByTool { get; set; } = new();
}

/// <summary>
/// Metrics for a specific tool.
/// </summary>
public class ToolExecutionMetrics
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public required string ToolName { get; set; }

    /// <summary>
    /// Gets or sets execution count.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets success count.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets failure count.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Gets or sets average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }
}

/// <summary>
/// Resource access metrics.
/// </summary>
public class ResourceMetrics
{
    /// <summary>
    /// Gets or sets total access count.
    /// </summary>
    public long TotalAccesses { get; set; }

    /// <summary>
    /// Gets or sets read count.
    /// </summary>
    public long ReadCount { get; set; }

    /// <summary>
    /// Gets or sets write count.
    /// </summary>
    public long WriteCount { get; set; }

    /// <summary>
    /// Gets or sets delete count.
    /// </summary>
    public long DeleteCount { get; set; }

    /// <summary>
    /// Gets or sets average access time.
    /// </summary>
    public TimeSpan AverageAccessTime { get; set; }

    /// <summary>
    /// Gets or sets access counts by resource.
    /// </summary>
    public Dictionary<string, ResourceAccessMetrics> ByResource { get; set; } = new();
}

/// <summary>
/// Metrics for a specific resource.
/// </summary>
public class ResourceAccessMetrics
{
    /// <summary>
    /// Gets or sets the resource URI.
    /// </summary>
    public required string ResourceUri { get; set; }

    /// <summary>
    /// Gets or sets access count.
    /// </summary>
    public long AccessCount { get; set; }

    /// <summary>
    /// Gets or sets operation counts.
    /// </summary>
    public Dictionary<string, long> OperationCounts { get; set; } = new();

    /// <summary>
    /// Gets or sets average access time.
    /// </summary>
    public TimeSpan AverageAccessTime { get; set; }
}

/// <summary>
/// Authentication metrics.
/// </summary>
public class AuthenticationMetrics
{
    /// <summary>
    /// Gets or sets total authentication attempts.
    /// </summary>
    public long TotalAttempts { get; set; }

    /// <summary>
    /// Gets or sets successful authentications.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets failed authentications.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Gets or sets average authentication time.
    /// </summary>
    public TimeSpan AverageAuthTime { get; set; }

    /// <summary>
    /// Gets or sets authentication attempts by provider.
    /// </summary>
    public Dictionary<string, AuthenticationProviderMetrics> ByProvider { get; set; } = new();
}

/// <summary>
/// Metrics for a specific authentication provider.
/// </summary>
public class AuthenticationProviderMetrics
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// Gets or sets attempt count.
    /// </summary>
    public long AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets success count.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets failure count.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Gets or sets average authentication time.
    /// </summary>
    public TimeSpan AverageAuthTime { get; set; }
}

/// <summary>
/// System metrics.
/// </summary>
public class SystemMetrics
{
    /// <summary>
    /// Gets or sets CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Gets or sets memory usage in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets thread count.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets active connection count.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Gets or sets uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets garbage collection stats.
    /// </summary>
    public GarbageCollectionStats GcStats { get; set; } = new();
}

/// <summary>
/// Garbage collection statistics.
/// </summary>
public class GarbageCollectionStats
{
    /// <summary>
    /// Gets or sets Gen0 collection count.
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gets or sets Gen1 collection count.
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gets or sets Gen2 collection count.
    /// </summary>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// Gets or sets total memory allocated.
    /// </summary>
    public long TotalMemoryBytes { get; set; }
}