using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Trace;

namespace McpServer.Infrastructure.Middleware;

/// <summary>
/// Middleware for enriching OpenTelemetry traces with additional information.
/// </summary>
public class OpenTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ActivitySource ActivitySource = new("McpServer.Infrastructure", "1.0.0");

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenTelemetryMiddleware"/> class.
    /// </summary>
    public OpenTelemetryMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        
        if (activity != null)
        {
            // Add custom tags
            activity.SetTag("http.user_agent", context.Request.Headers["User-Agent"].ToString());
            activity.SetTag("http.real_ip", GetRealIp(context));
            
            // Add user information if available
            if (context.Items.TryGetValue("UserId", out var userId) && userId != null)
            {
                activity.SetTag("user.id", userId.ToString());
            }
            
            if (context.Items.TryGetValue("SessionId", out var sessionId) && sessionId != null)
            {
                activity.SetTag("session.id", sessionId.ToString());
            }
            
            // Add request ID for correlation
            if (context.TraceIdentifier != null)
            {
                activity.SetTag("request.id", context.TraceIdentifier);
            }
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        
        // Add response information
        if (activity != null)
        {
            activity.SetTag("http.response.status_code", context.Response.StatusCode);
            
            if (context.Response.StatusCode >= 400)
            {
                activity.SetStatus(ActivityStatusCode.Error, $"HTTP {context.Response.StatusCode}");
            }
        }
    }

    private static string GetRealIp(HttpContext context)
    {
        // Check for X-Forwarded-For header
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        // Check for X-Real-IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// Extension methods for OpenTelemetryMiddleware.
/// </summary>
public static class OpenTelemetryMiddlewareExtensions
{
    /// <summary>
    /// Adds OpenTelemetry enrichment middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseOpenTelemetryEnrichment(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<OpenTelemetryMiddleware>();
    }
}