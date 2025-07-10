using McpServer.Domain.Monitoring;
using McpServer.Domain.Security;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.Web.Controllers;

/// <summary>
/// Controller for monitoring dashboard.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly IHealthCheckService _healthCheckService;
    private readonly ISessionService _sessionService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<MonitoringController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MonitoringController"/> class.
    /// </summary>
    public MonitoringController(
        IMetricsService metricsService,
        IHealthCheckService healthCheckService,
        ISessionService sessionService,
        IUserRepository userRepository,
        ILogger<MonitoringController> logger)
    {
        _metricsService = metricsService;
        _healthCheckService = healthCheckService;
        _sessionService = sessionService;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets dashboard overview data.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardData), 200)]
    public async Task<IActionResult> GetDashboard()
    {
        var metrics = _metricsService.GetSnapshot();
        var health = await _healthCheckService.CheckHealthAsync();
        
        var dashboard = new DashboardData
        {
            Timestamp = DateTime.UtcNow,
            Health = new DashboardHealth
            {
                Status = health.Status,
                Components = health.Components.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ComponentStatus
                    {
                        Name = kvp.Value.Name,
                        Status = kvp.Value.Status,
                        ResponseTime = kvp.Value.ResponseTime
                    })
            },
            Performance = new DashboardPerformance
            {
                RequestsPerMinute = CalculateRequestsPerMinute(metrics),
                AverageResponseTime = metrics.Requests.AverageResponseTime,
                ErrorRate = CalculateErrorRate(metrics),
                ActiveConnections = metrics.System.ActiveConnections
            },
            System = new DashboardSystem
            {
                CpuUsage = metrics.System.CpuUsagePercent,
                MemoryUsage = metrics.System.MemoryUsageBytes,
                Uptime = metrics.System.Uptime,
                ThreadCount = metrics.System.ThreadCount
            },
            Activity = new DashboardActivity
            {
                TotalRequests = metrics.Requests.TotalCount,
                TotalToolExecutions = metrics.Tools.TotalExecutions,
                TotalAuthentications = metrics.Authentication.TotalAttempts,
                TopEndpoints = GetTopEndpoints(metrics),
                TopTools = GetTopTools(metrics)
            }
        };

        return Ok(dashboard);
    }

    /// <summary>
    /// Gets real-time metrics stream.
    /// </summary>
    [HttpGet("stream")]
    [Produces("text/event-stream")]
    public async Task GetMetricsStream()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var cancellationToken = HttpContext.RequestAborted;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var metrics = _metricsService.GetSnapshot();
                var data = new
                {
                    timestamp = DateTime.UtcNow,
                    requests_per_second = CalculateRequestsPerSecond(metrics),
                    active_connections = metrics.System.ActiveConnections,
                    cpu_usage = metrics.System.CpuUsagePercent,
                    memory_mb = metrics.System.MemoryUsageBytes / (1024 * 1024),
                    error_rate = CalculateErrorRate(metrics)
                };

                await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(data)}\n\n");
                await Response.Body.FlushAsync();

                await Task.Delay(1000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics stream");
                break;
            }
        }
    }

    /// <summary>
    /// Gets historical metrics.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(HistoricalMetrics), 200)]
    public IActionResult GetHistory([FromQuery] int hours = 24)
    {
        if (hours < 1 || hours > 168) // Max 7 days
        {
            hours = 24;
        }

        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddHours(-hours);
        var interval = TimeSpan.FromHours(1);

        var dataPoints = new List<MetricsDataPoint>();

        for (var time = startTime; time < endTime; time += interval)
        {
            var snapshot = _metricsService.GetSnapshot(time, time + interval);
            dataPoints.Add(new MetricsDataPoint
            {
                Timestamp = time,
                RequestCount = snapshot.Requests.TotalCount,
                ErrorCount = snapshot.Requests.FailureCount,
                AverageResponseTime = snapshot.Requests.AverageResponseTime.TotalMilliseconds,
                ToolExecutions = snapshot.Tools.TotalExecutions,
                AuthAttempts = snapshot.Authentication.TotalAttempts
            });
        }

        return Ok(new HistoricalMetrics
        {
            StartTime = startTime,
            EndTime = endTime,
            DataPoints = dataPoints
        });
    }

    private static double CalculateRequestsPerMinute(MetricsSnapshot metrics)
    {
        if (metrics.Duration.TotalMinutes == 0) return 0;
        return metrics.Requests.TotalCount / metrics.Duration.TotalMinutes;
    }

    private static double CalculateRequestsPerSecond(MetricsSnapshot metrics)
    {
        if (metrics.Duration.TotalSeconds == 0) return 0;
        return metrics.Requests.TotalCount / metrics.Duration.TotalSeconds;
    }

    private static double CalculateErrorRate(MetricsSnapshot metrics)
    {
        if (metrics.Requests.TotalCount == 0) return 0;
        return (double)metrics.Requests.FailureCount / metrics.Requests.TotalCount * 100;
    }

    private static List<EndpointStats> GetTopEndpoints(MetricsSnapshot metrics)
    {
        return metrics.Requests.ByMethod
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => new EndpointStats
            {
                Endpoint = kvp.Key,
                RequestCount = kvp.Value
            })
            .ToList();
    }

    private static List<ToolStats> GetTopTools(MetricsSnapshot metrics)
    {
        return metrics.Tools.ByTool
            .OrderByDescending(kvp => kvp.Value.ExecutionCount)
            .Take(5)
            .Select(kvp => new ToolStats
            {
                ToolName = kvp.Key,
                ExecutionCount = kvp.Value.ExecutionCount,
                SuccessRate = kvp.Value.ExecutionCount > 0 
                    ? (double)kvp.Value.SuccessCount / kvp.Value.ExecutionCount * 100 
                    : 0
            })
            .ToList();
    }
}

/// <summary>
/// Dashboard data model.
/// </summary>
public class DashboardData
{
    public DateTime Timestamp { get; set; }
    public DashboardHealth Health { get; set; } = new();
    public DashboardPerformance Performance { get; set; } = new();
    public DashboardSystem System { get; set; } = new();
    public DashboardActivity Activity { get; set; } = new();
}

/// <summary>
/// Dashboard health data.
/// </summary>
public class DashboardHealth
{
    public HealthStatus Status { get; set; }
    public Dictionary<string, ComponentStatus> Components { get; set; } = new();
}

/// <summary>
/// Component status.
/// </summary>
public class ComponentStatus
{
    public required string Name { get; set; }
    public HealthStatus Status { get; set; }
    public TimeSpan ResponseTime { get; set; }
}

/// <summary>
/// Dashboard performance data.
/// </summary>
public class DashboardPerformance
{
    public double RequestsPerMinute { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public int ActiveConnections { get; set; }
}

/// <summary>
/// Dashboard system data.
/// </summary>
public class DashboardSystem
{
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public TimeSpan Uptime { get; set; }
    public int ThreadCount { get; set; }
}

/// <summary>
/// Dashboard activity data.
/// </summary>
public class DashboardActivity
{
    public long TotalRequests { get; set; }
    public long TotalToolExecutions { get; set; }
    public long TotalAuthentications { get; set; }
    public List<EndpointStats> TopEndpoints { get; set; } = new();
    public List<ToolStats> TopTools { get; set; } = new();
}

/// <summary>
/// Endpoint statistics.
/// </summary>
public class EndpointStats
{
    public required string Endpoint { get; set; }
    public long RequestCount { get; set; }
}

/// <summary>
/// Tool statistics.
/// </summary>
public class ToolStats
{
    public required string ToolName { get; set; }
    public long ExecutionCount { get; set; }
    public double SuccessRate { get; set; }
}

/// <summary>
/// Historical metrics.
/// </summary>
public class HistoricalMetrics
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<MetricsDataPoint> DataPoints { get; set; } = new();
}

/// <summary>
/// Metrics data point.
/// </summary>
public class MetricsDataPoint
{
    public DateTime Timestamp { get; set; }
    public long RequestCount { get; set; }
    public long ErrorCount { get; set; }
    public double AverageResponseTime { get; set; }
    public long ToolExecutions { get; set; }
    public long AuthAttempts { get; set; }
}