using System.Collections.Concurrent;
using System.Diagnostics;
using McpServer.Domain.Monitoring;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Service for collecting and reporting performance metrics.
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly ILogger<MetricsService> _logger;
    private readonly ConcurrentDictionary<string, List<RequestMetric>> _requestMetrics = new();
    private readonly ConcurrentDictionary<string, List<ToolExecutionMetric>> _toolMetrics = new();
    private readonly ConcurrentDictionary<string, List<ResourceAccessMetric>> _resourceMetrics = new();
    private readonly ConcurrentDictionary<string, List<AuthenticationMetric>> _authMetrics = new();
    private readonly ConcurrentDictionary<string, double> _customMetrics = new();
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentDictionary<string, double> _gauges = new();
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsService"/> class.
    /// </summary>
    public MetricsService(ILogger<MetricsService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void RecordRequest(string method, TimeSpan duration, bool success, Dictionary<string, object>? metadata = null)
    {
        var metric = new RequestMetric
        {
            Method = method,
            Duration = duration,
            Success = success,
            Timestamp = DateTime.UtcNow,
            Metadata = metadata
        };

        _requestMetrics.AddOrUpdate(method, 
            new List<RequestMetric> { metric },
            (_, list) =>
            {
                lock (_lock)
                {
                    list.Add(metric);
                    return list;
                }
            });

        _logger.LogDebug("Recorded request metric for {Method}: {Duration}ms, Success: {Success}", 
            method, duration.TotalMilliseconds, success);
    }

    /// <inheritdoc/>
    public void RecordToolExecution(string toolName, TimeSpan duration, bool success)
    {
        var metric = new ToolExecutionMetric
        {
            ToolName = toolName,
            Duration = duration,
            Success = success,
            Timestamp = DateTime.UtcNow
        };

        _toolMetrics.AddOrUpdate(toolName,
            new List<ToolExecutionMetric> { metric },
            (_, list) =>
            {
                lock (_lock)
                {
                    list.Add(metric);
                    return list;
                }
            });

        _logger.LogDebug("Recorded tool execution metric for {ToolName}: {Duration}ms, Success: {Success}",
            toolName, duration.TotalMilliseconds, success);
    }

    /// <inheritdoc/>
    public void RecordResourceAccess(string resourceUri, string operation, TimeSpan duration, bool success)
    {
        var metric = new ResourceAccessMetric
        {
            ResourceUri = resourceUri,
            Operation = operation,
            Duration = duration,
            Success = success,
            Timestamp = DateTime.UtcNow
        };

        _resourceMetrics.AddOrUpdate(resourceUri,
            new List<ResourceAccessMetric> { metric },
            (_, list) =>
            {
                lock (_lock)
                {
                    list.Add(metric);
                    return list;
                }
            });

        _logger.LogDebug("Recorded resource access metric for {ResourceUri} ({Operation}): {Duration}ms, Success: {Success}",
            resourceUri, operation, duration.TotalMilliseconds, success);
    }

    /// <inheritdoc/>
    public void RecordAuthentication(string provider, bool success, TimeSpan duration)
    {
        var metric = new AuthenticationMetric
        {
            Provider = provider,
            Success = success,
            Duration = duration,
            Timestamp = DateTime.UtcNow
        };

        _authMetrics.AddOrUpdate(provider,
            new List<AuthenticationMetric> { metric },
            (_, list) =>
            {
                lock (_lock)
                {
                    list.Add(metric);
                    return list;
                }
            });

        _logger.LogDebug("Recorded authentication metric for {Provider}: {Duration}ms, Success: {Success}",
            provider, duration.TotalMilliseconds, success);
    }

    /// <inheritdoc/>
    public void RecordMetric(string name, double value, Dictionary<string, string>? tags = null)
    {
        var key = tags != null ? $"{name}:{string.Join(",", tags.Select(kvp => $"{kvp.Key}={kvp.Value}"))}" : name;
        _customMetrics.AddOrUpdate(key, value, (_, _) => value);
    }

    /// <inheritdoc/>
    public void IncrementCounter(string name, Dictionary<string, string>? tags = null, long increment = 1)
    {
        var key = tags != null ? $"{name}:{string.Join(",", tags.Select(kvp => $"{kvp.Key}={kvp.Value}"))}" : name;
        _counters.AddOrUpdate(key, increment, (_, current) => current + increment);
    }

    /// <inheritdoc/>
    public void RecordGauge(string name, double value, Dictionary<string, string>? tags = null)
    {
        var key = tags != null ? $"{name}:{string.Join(",", tags.Select(kvp => $"{kvp.Key}={kvp.Value}"))}" : name;
        _gauges.AddOrUpdate(key, value, (_, _) => value);
    }

    /// <inheritdoc/>
    public MetricsSnapshot GetSnapshot()
    {
        return GetSnapshot(DateTime.MinValue, DateTime.MaxValue);
    }

    /// <inheritdoc/>
    public MetricsSnapshot GetSnapshot(DateTime startTime, DateTime endTime)
    {
        var snapshot = new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Duration = DateTime.UtcNow - _startTime
        };

        // Request metrics
        var allRequests = _requestMetrics.Values
            .SelectMany(list => list)
            .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
            .ToList();

        if (allRequests.Count > 0)
        {
            snapshot.Requests = new RequestMetrics
            {
                TotalCount = allRequests.Count,
                SuccessCount = allRequests.Count(r => r.Success),
                FailureCount = allRequests.Count(r => !r.Success),
                AverageResponseTime = TimeSpan.FromMilliseconds(allRequests.Average(r => r.Duration.TotalMilliseconds)),
                MinResponseTime = allRequests.Min(r => r.Duration),
                MaxResponseTime = allRequests.Max(r => r.Duration),
                Percentiles = CalculatePercentiles(allRequests.Select(r => r.Duration).ToList()),
                ByMethod = allRequests.GroupBy(r => r.Method)
                    .ToDictionary(g => g.Key, g => (long)g.Count())
            };
        }

        // Tool metrics
        var allTools = _toolMetrics.Values
            .SelectMany(list => list)
            .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
            .ToList();

        if (allTools.Count > 0)
        {
            snapshot.Tools = new ToolMetrics
            {
                TotalExecutions = allTools.Count,
                SuccessCount = allTools.Count(t => t.Success),
                FailureCount = allTools.Count(t => !t.Success),
                AverageExecutionTime = TimeSpan.FromMilliseconds(allTools.Average(t => t.Duration.TotalMilliseconds)),
                ByTool = allTools.GroupBy(t => t.ToolName)
                    .ToDictionary(g => g.Key, g => new ToolExecutionMetrics
                    {
                        ToolName = g.Key,
                        ExecutionCount = g.Count(),
                        SuccessCount = g.Count(t => t.Success),
                        FailureCount = g.Count(t => !t.Success),
                        AverageExecutionTime = TimeSpan.FromMilliseconds(g.Average(t => t.Duration.TotalMilliseconds))
                    })
            };
        }

        // Resource metrics
        var allResources = _resourceMetrics.Values
            .SelectMany(list => list)
            .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
            .ToList();

        if (allResources.Count > 0)
        {
            snapshot.Resources = new ResourceMetrics
            {
                TotalAccesses = allResources.Count,
                ReadCount = allResources.Count(r => r.Operation.Equals("read", StringComparison.OrdinalIgnoreCase)),
                WriteCount = allResources.Count(r => r.Operation.Equals("write", StringComparison.OrdinalIgnoreCase)),
                DeleteCount = allResources.Count(r => r.Operation.Equals("delete", StringComparison.OrdinalIgnoreCase)),
                AverageAccessTime = TimeSpan.FromMilliseconds(allResources.Average(r => r.Duration.TotalMilliseconds)),
                ByResource = allResources.GroupBy(r => r.ResourceUri)
                    .ToDictionary(g => g.Key, g => new ResourceAccessMetrics
                    {
                        ResourceUri = g.Key,
                        AccessCount = g.Count(),
                        OperationCounts = g.GroupBy(r => r.Operation)
                            .ToDictionary(og => og.Key, og => (long)og.Count()),
                        AverageAccessTime = TimeSpan.FromMilliseconds(g.Average(r => r.Duration.TotalMilliseconds))
                    })
            };
        }

        // Authentication metrics
        var allAuth = _authMetrics.Values
            .SelectMany(list => list)
            .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
            .ToList();

        if (allAuth.Count > 0)
        {
            snapshot.Authentication = new AuthenticationMetrics
            {
                TotalAttempts = allAuth.Count,
                SuccessCount = allAuth.Count(a => a.Success),
                FailureCount = allAuth.Count(a => !a.Success),
                AverageAuthTime = TimeSpan.FromMilliseconds(allAuth.Average(a => a.Duration.TotalMilliseconds)),
                ByProvider = allAuth.GroupBy(a => a.Provider)
                    .ToDictionary(g => g.Key, g => new AuthenticationProviderMetrics
                    {
                        Provider = g.Key,
                        AttemptCount = g.Count(),
                        SuccessCount = g.Count(a => a.Success),
                        FailureCount = g.Count(a => !a.Success),
                        AverageAuthTime = TimeSpan.FromMilliseconds(g.Average(a => a.Duration.TotalMilliseconds))
                    })
            };
        }

        // System metrics
        var process = Process.GetCurrentProcess();
        snapshot.System = new SystemMetrics
        {
            CpuUsagePercent = 0, // Would need OS-specific implementation
            MemoryUsageBytes = process.WorkingSet64,
            ThreadCount = process.Threads.Count,
            ActiveConnections = 0, // Would need to track this separately
            Uptime = DateTime.UtcNow - _startTime,
            GcStats = new GarbageCollectionStats
            {
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                TotalMemoryBytes = GC.GetTotalMemory(false)
            }
        };

        // Custom metrics
        snapshot.CustomMetrics = new Dictionary<string, double>(_customMetrics);

        // Add counters and gauges
        foreach (var counter in _counters)
        {
            snapshot.CustomMetrics[$"counter.{counter.Key}"] = counter.Value;
        }

        foreach (var gauge in _gauges)
        {
            snapshot.CustomMetrics[$"gauge.{gauge.Key}"] = gauge.Value;
        }

        return snapshot;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _requestMetrics.Clear();
        _toolMetrics.Clear();
        _resourceMetrics.Clear();
        _authMetrics.Clear();
        _customMetrics.Clear();
        _counters.Clear();
        _gauges.Clear();
        
        _logger.LogInformation("Metrics have been reset");
    }

    private static ResponseTimePercentiles CalculatePercentiles(List<TimeSpan> durations)
    {
        if (durations.Count == 0)
        {
            return new ResponseTimePercentiles();
        }

        var sorted = durations.OrderBy(d => d).ToList();
        var count = sorted.Count;

        return new ResponseTimePercentiles
        {
            P50 = sorted[(int)(count * 0.5)],
            P90 = sorted[(int)(count * 0.9)],
            P95 = sorted[(int)(count * 0.95)],
            P99 = sorted[(int)(count * 0.99)]
        };
    }

    private class RequestMetric
    {
        public required string Method { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    private class ToolExecutionMetric
    {
        public required string ToolName { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class ResourceAccessMetric
    {
        public required string ResourceUri { get; set; }
        public required string Operation { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class AuthenticationMetric
    {
        public required string Provider { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; }
    }
}