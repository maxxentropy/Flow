using McpServer.Domain.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.Web.Controllers;

/// <summary>
/// Controller for metrics and monitoring.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly ILogger<MetricsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsController"/> class.
    /// </summary>
    public MetricsController(
        IMetricsService metricsService,
        ILogger<MetricsController> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets current metrics snapshot.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(MetricsSnapshot), 200)]
    public IActionResult GetMetrics()
    {
        var snapshot = _metricsService.GetSnapshot();
        return Ok(snapshot);
    }

    /// <summary>
    /// Gets metrics for a specific time range.
    /// </summary>
    [HttpGet("range")]
    [ProducesResponseType(typeof(MetricsSnapshot), 200)]
    [ProducesResponseType(400)]
    public IActionResult GetMetricsRange([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime)
    {
        var start = startTime ?? DateTime.UtcNow.AddHours(-1);
        var end = endTime ?? DateTime.UtcNow;

        if (start > end)
        {
            return BadRequest(new { error = "Start time must be before end time" });
        }

        if (end - start > TimeSpan.FromDays(7))
        {
            return BadRequest(new { error = "Time range cannot exceed 7 days" });
        }

        var snapshot = _metricsService.GetSnapshot(start, end);
        return Ok(snapshot);
    }

    /// <summary>
    /// Gets metrics in Prometheus format.
    /// </summary>
    [HttpGet("prometheus")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), 200)]
    public IActionResult GetPrometheusMetrics()
    {
        var snapshot = _metricsService.GetSnapshot();
        var lines = new List<string>();

        // Request metrics
        if (snapshot.Requests.TotalCount > 0)
        {
            lines.Add("# HELP mcpserver_requests_total Total number of requests");
            lines.Add("# TYPE mcpserver_requests_total counter");
            lines.Add($"mcpserver_requests_total {snapshot.Requests.TotalCount}");

            lines.Add("# HELP mcpserver_requests_success_total Total number of successful requests");
            lines.Add("# TYPE mcpserver_requests_success_total counter");
            lines.Add($"mcpserver_requests_success_total {snapshot.Requests.SuccessCount}");

            lines.Add("# HELP mcpserver_requests_failed_total Total number of failed requests");
            lines.Add("# TYPE mcpserver_requests_failed_total counter");
            lines.Add($"mcpserver_requests_failed_total {snapshot.Requests.FailureCount}");

            lines.Add("# HELP mcpserver_request_duration_seconds Request duration in seconds");
            lines.Add("# TYPE mcpserver_request_duration_seconds summary");
            lines.Add($"mcpserver_request_duration_seconds{{quantile=\"0.5\"}} {snapshot.Requests.Percentiles.P50.TotalSeconds}");
            lines.Add($"mcpserver_request_duration_seconds{{quantile=\"0.9\"}} {snapshot.Requests.Percentiles.P90.TotalSeconds}");
            lines.Add($"mcpserver_request_duration_seconds{{quantile=\"0.95\"}} {snapshot.Requests.Percentiles.P95.TotalSeconds}");
            lines.Add($"mcpserver_request_duration_seconds{{quantile=\"0.99\"}} {snapshot.Requests.Percentiles.P99.TotalSeconds}");
            lines.Add($"mcpserver_request_duration_seconds_sum {snapshot.Requests.AverageResponseTime.TotalSeconds * snapshot.Requests.TotalCount}");
            lines.Add($"mcpserver_request_duration_seconds_count {snapshot.Requests.TotalCount}");

            // Requests by method
            foreach (var method in snapshot.Requests.ByMethod)
            {
                lines.Add($"mcpserver_requests_by_method{{method=\"{method.Key}\"}} {method.Value}");
            }
        }

        // Tool metrics
        if (snapshot.Tools.TotalExecutions > 0)
        {
            lines.Add("# HELP mcpserver_tool_executions_total Total number of tool executions");
            lines.Add("# TYPE mcpserver_tool_executions_total counter");
            lines.Add($"mcpserver_tool_executions_total {snapshot.Tools.TotalExecutions}");

            foreach (var tool in snapshot.Tools.ByTool)
            {
                lines.Add($"mcpserver_tool_executions_by_tool{{tool=\"{tool.Key}\"}} {tool.Value.ExecutionCount}");
                lines.Add($"mcpserver_tool_success_by_tool{{tool=\"{tool.Key}\"}} {tool.Value.SuccessCount}");
                lines.Add($"mcpserver_tool_failures_by_tool{{tool=\"{tool.Key}\"}} {tool.Value.FailureCount}");
            }
        }

        // Authentication metrics
        if (snapshot.Authentication.TotalAttempts > 0)
        {
            lines.Add("# HELP mcpserver_auth_attempts_total Total number of authentication attempts");
            lines.Add("# TYPE mcpserver_auth_attempts_total counter");
            lines.Add($"mcpserver_auth_attempts_total {snapshot.Authentication.TotalAttempts}");

            lines.Add("# HELP mcpserver_auth_success_total Total number of successful authentications");
            lines.Add("# TYPE mcpserver_auth_success_total counter");
            lines.Add($"mcpserver_auth_success_total {snapshot.Authentication.SuccessCount}");

            foreach (var provider in snapshot.Authentication.ByProvider)
            {
                lines.Add($"mcpserver_auth_by_provider{{provider=\"{provider.Key}\"}} {provider.Value.AttemptCount}");
            }
        }

        // System metrics
        lines.Add("# HELP mcpserver_memory_usage_bytes Memory usage in bytes");
        lines.Add("# TYPE mcpserver_memory_usage_bytes gauge");
        lines.Add($"mcpserver_memory_usage_bytes {snapshot.System.MemoryUsageBytes}");

        lines.Add("# HELP mcpserver_cpu_usage_percent CPU usage percentage");
        lines.Add("# TYPE mcpserver_cpu_usage_percent gauge");
        lines.Add($"mcpserver_cpu_usage_percent {snapshot.System.CpuUsagePercent}");

        lines.Add("# HELP mcpserver_thread_count Number of threads");
        lines.Add("# TYPE mcpserver_thread_count gauge");
        lines.Add($"mcpserver_thread_count {snapshot.System.ThreadCount}");

        lines.Add("# HELP mcpserver_gc_collections_total Garbage collection count");
        lines.Add("# TYPE mcpserver_gc_collections_total counter");
        lines.Add($"mcpserver_gc_collections_total{{generation=\"0\"}} {snapshot.System.GcStats.Gen0Collections}");
        lines.Add($"mcpserver_gc_collections_total{{generation=\"1\"}} {snapshot.System.GcStats.Gen1Collections}");
        lines.Add($"mcpserver_gc_collections_total{{generation=\"2\"}} {snapshot.System.GcStats.Gen2Collections}");

        lines.Add("# HELP mcpserver_uptime_seconds Service uptime in seconds");
        lines.Add("# TYPE mcpserver_uptime_seconds counter");
        lines.Add($"mcpserver_uptime_seconds {snapshot.System.Uptime.TotalSeconds}");

        // Custom metrics
        foreach (var metric in snapshot.CustomMetrics)
        {
            if (metric.Key.StartsWith("counter.", StringComparison.Ordinal))
            {
                var name = metric.Key.Replace("counter.", "");
                lines.Add($"# HELP {name} Custom counter");
                lines.Add($"# TYPE {name} counter");
                lines.Add($"{name} {metric.Value}");
            }
            else if (metric.Key.StartsWith("gauge.", StringComparison.Ordinal))
            {
                var name = metric.Key.Replace("gauge.", "");
                lines.Add($"# HELP {name} Custom gauge");
                lines.Add($"# TYPE {name} gauge");
                lines.Add($"{name} {metric.Value}");
            }
        }

        return Content(string.Join("\n", lines), "text/plain");
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    [HttpPost("reset")]
    [ProducesResponseType(200)]
    public IActionResult ResetMetrics()
    {
        _metricsService.Reset();
        _logger.LogWarning("Metrics have been reset");
        return Ok(new { message = "Metrics reset successfully", timestamp = DateTime.UtcNow });
    }
}