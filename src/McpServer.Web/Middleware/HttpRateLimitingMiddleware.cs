using System.Globalization;
using System.Net;
using System.Text.Json;
using McpServer.Application.Middleware;
using McpServer.Application.Server;
using McpServer.Domain.RateLimiting;

namespace McpServer.Web.Middleware;

/// <summary>
/// ASP.NET Core middleware for applying rate limiting to HTTP requests.
/// </summary>
public class HttpRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpRateLimitingMiddleware> _logger;

    public HttpRateLimitingMiddleware(RequestDelegate next, ILogger<HttpRateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context, 
        RateLimitingMiddleware rateLimitingMiddleware,
        IMessageRouter messageRouter)
    {
        // Extract rate limit context from HTTP request
        var rateLimitContext = ExtractRateLimitContext(context);

        // For SSE/WebSocket endpoints, we need to handle rate limiting differently
        if (IsStreamingEndpoint(context))
        {
            // Store rate limit context for later use
            context.Items["RateLimitContext"] = rateLimitContext;
            await _next(context);
            return;
        }

        // For regular HTTP endpoints, check rate limit before processing
        var method = ExtractMethodFromRequest(context);
        if (!string.IsNullOrEmpty(method))
        {
            var identifier = RateLimitingMiddleware.ExtractIdentifier(rateLimitContext);
            var result = await rateLimitingMiddleware.CheckRateLimitAsync(identifier, method);

            if (!result.IsAllowed)
            {
                await WriteRateLimitResponse(context, result, rateLimitingMiddleware);
                return;
            }

            // Add rate limit headers to response
            AddRateLimitHeaders(context.Response, result);
        }

        await _next(context);
    }

    private static RateLimitContext ExtractRateLimitContext(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress;
        
        // Extract API key from header
        string? apiKey = null;
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValue))
        {
            apiKey = apiKeyValue.ToString();
        }

        // Extract session ID from cookie or header
        string? sessionId = null;
        if (context.Request.Cookies.TryGetValue("SessionId", out var cookieSessionId))
        {
            sessionId = cookieSessionId;
        }
        else if (context.Request.Headers.TryGetValue("X-Session-Id", out var headerSessionId))
        {
            sessionId = headerSessionId.ToString();
        }

        return new RateLimitContext
        {
            IpAddress = ipAddress,
            ApiKey = apiKey,
            SessionId = sessionId,
            AdditionalData = new Dictionary<string, object>
            {
                ["Path"] = context.Request.Path.Value ?? string.Empty,
                ["Method"] = context.Request.Method,
                ["UserAgent"] = context.Request.Headers.UserAgent.ToString()
            }
        };
    }

    private static bool IsStreamingEndpoint(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        return path.Contains("/sse") || path.Contains("/ws");
    }

    private static string? ExtractMethodFromRequest(HttpContext context)
    {
        // For POST requests to /sse, we need to peek at the body
        if (context.Request.Method == "POST" && context.Request.Path.Value?.Contains("/sse") == true)
        {
            // This is handled differently - the actual method will be in the JSON-RPC body
            return null;
        }

        // For other endpoints, we might map HTTP methods to MCP methods
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        
        if (path.Contains("/tools") && context.Request.Method == "GET")
            return "tools/list";
        if (path.Contains("/resources") && context.Request.Method == "GET")
            return "resources/list";
        
        return null;
    }

    private static async Task WriteRateLimitResponse(
        HttpContext context, 
        RateLimitResult result,
        RateLimitingMiddleware rateLimitingMiddleware)
    {
        context.Response.StatusCode = 429; // Too Many Requests
        context.Response.ContentType = "application/json";

        // Add rate limit headers
        AddRateLimitHeaders(context.Response, result);

        var response = new
        {
            error = new
            {
                code = -32650,
                message = result.DenialReason ?? "Rate limit exceeded",
                data = new
                {
                    limit = result.Limit,
                    remaining = result.Remaining,
                    resetsAt = result.ResetsAt.ToUnixTimeSeconds(),
                    retryAfter = result.RetryAfter?.TotalSeconds
                }
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static void AddRateLimitHeaders(HttpResponse response, RateLimitResult result)
    {
        response.Headers["X-RateLimit-Limit"] = result.Limit.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-RateLimit-Reset"] = result.ResetsAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        if (!result.IsAllowed && result.RetryAfter.HasValue)
        {
            response.Headers["Retry-After"] = ((int)result.RetryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
    }
}

/// <summary>
/// Extension methods for adding rate limiting middleware.
/// </summary>
public static class HttpRateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Adds HTTP rate limiting middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseHttpRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<HttpRateLimitingMiddleware>();
    }
}