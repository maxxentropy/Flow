using System.Diagnostics;
using System.Globalization;
using McpServer.Domain.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Middleware;

/// <summary>
/// Middleware for capturing request/response metrics.
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsMiddleware> _logger;
    private readonly IMetricsService _metricsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsMiddleware"/> class.
    /// </summary>
    public MetricsMiddleware(
        RequestDelegate next,
        ILogger<MetricsMiddleware> logger,
        IMetricsService metricsService)
    {
        _next = next;
        _logger = logger;
        _metricsService = metricsService;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = $"{context.Request.Method} {context.Request.Path}";
        var success = true;

        try
        {
            await _next(context);
            
            // Consider 4xx and 5xx as failures
            if (context.Response.StatusCode >= 400)
            {
                success = false;
            }
        }
        catch (Exception ex)
        {
            success = false;
            _logger.LogError(ex, "Request {Method} failed", method);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            var metadata = new Dictionary<string, object>
            {
                ["status_code"] = context.Response.StatusCode,
                ["method"] = context.Request.Method,
                ["path"] = context.Request.Path.Value ?? "",
                ["user_agent"] = context.Request.Headers["User-Agent"].ToString()
            };

            if (context.Items.TryGetValue("UserId", out var userId) && userId != null)
            {
                metadata["user_id"] = userId.ToString() ?? "";
            }

            _metricsService.RecordRequest(method, stopwatch.Elapsed, success, metadata);
            
            // Also increment counters
            _metricsService.IncrementCounter("http_requests_total", new Dictionary<string, string>
            {
                ["method"] = context.Request.Method,
                ["status"] = context.Response.StatusCode.ToString(CultureInfo.InvariantCulture)
            });

            if (!success)
            {
                _metricsService.IncrementCounter("http_requests_failed_total", new Dictionary<string, string>
                {
                    ["method"] = context.Request.Method,
                    ["status"] = context.Response.StatusCode.ToString(CultureInfo.InvariantCulture)
                });
            }

            _logger.LogInformation("Request {Method} completed in {Duration}ms with status {StatusCode}",
                method, stopwatch.ElapsedMilliseconds, context.Response.StatusCode);
        }
    }
}

/// <summary>
/// Extension methods for MetricsMiddleware.
/// </summary>
public static class MetricsMiddlewareExtensions
{
    /// <summary>
    /// Adds metrics middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseMetrics(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MetricsMiddleware>();
    }
}