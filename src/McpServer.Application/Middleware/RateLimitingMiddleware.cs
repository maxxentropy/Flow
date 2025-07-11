using System.Globalization;
using System.Net;
using System.Text.Json;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.RateLimiting;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Middleware;

/// <summary>
/// Middleware that enforces rate limiting on MCP requests.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingMiddlewareOptions _options;

    public RateLimitingMiddleware(
        IRateLimiter rateLimiter, 
        ILogger<RateLimitingMiddleware> logger,
        RateLimitingMiddlewareOptions? options = null)
    {
        _rateLimiter = rateLimiter;
        _logger = logger;
        _options = options ?? new RateLimitingMiddlewareOptions();
    }

    /// <summary>
    /// Checks rate limit for an incoming request.
    /// </summary>
    /// <param name="identifier">The identifier for rate limiting (e.g., IP, session ID, API key).</param>
    /// <param name="method">The MCP method being called.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rate limit result.</returns>
    public async Task<RateLimitResult> CheckRateLimitAsync(
        string identifier, 
        string method, 
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                Remaining = int.MaxValue,
                Limit = int.MaxValue,
                ResetsAt = DateTimeOffset.UtcNow.AddYears(1)
            };
        }

        // Map method to resource for rate limiting
        var resource = MapMethodToResource(method);

        // Check rate limit
        var result = await _rateLimiter.CheckRateLimitAsync(identifier, resource, cancellationToken);

        // Log if rate limit is close to being exceeded
        if (result.IsAllowed && result.Remaining <= _options.WarningThreshold)
        {
            _logger.LogWarning("Rate limit warning for {Identifier} on {Resource}: {Remaining}/{Limit} requests remaining",
                identifier, resource, result.Remaining, result.Limit);
        }

        return result;
    }

    /// <summary>
    /// Creates a rate limit exceeded error response.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="result">The rate limit result.</param>
    /// <returns>JSON-RPC error response.</returns>
    public static JsonRpcResponse CreateRateLimitErrorResponse(object? requestId, RateLimitResult result)
    {
        var errorData = new
        {
            limit = result.Limit,
            remaining = result.Remaining,
            resetsAt = result.ResetsAt.ToUnixTimeSeconds(),
            retryAfter = result.RetryAfter?.TotalSeconds
        };

        return new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Id = requestId,
            Error = new JsonRpcError
            {
                Code = McpErrorCodes.RateLimitExceeded,
                Message = result.DenialReason ?? "Rate limit exceeded",
                Data = errorData
            }
        };
    }

    /// <summary>
    /// Adds rate limit headers to the response.
    /// </summary>
    /// <param name="headers">The headers collection to add to.</param>
    /// <param name="result">The rate limit result.</param>
    public static void AddRateLimitHeaders(IDictionary<string, string> headers, RateLimitResult result)
    {
        headers["X-RateLimit-Limit"] = result.Limit.ToString(CultureInfo.InvariantCulture);
        headers["X-RateLimit-Remaining"] = result.Remaining.ToString(CultureInfo.InvariantCulture);
        headers["X-RateLimit-Reset"] = result.ResetsAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        if (!result.IsAllowed && result.RetryAfter.HasValue)
        {
            headers["Retry-After"] = ((int)result.RetryAfter.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Extracts the identifier from the request context.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <returns>The identifier for rate limiting.</returns>
    public static string ExtractIdentifier(RateLimitContext context)
    {
        // Priority order: API Key > Session ID > IP Address
        if (!string.IsNullOrEmpty(context.ApiKey))
        {
            return $"apikey:{context.ApiKey}";
        }

        if (!string.IsNullOrEmpty(context.SessionId))
        {
            return $"session:{context.SessionId}";
        }

        if (context.IpAddress != null)
        {
            return $"ip:{context.IpAddress}";
        }

        // Fallback to a generic identifier
        return "anonymous";
    }

    private string MapMethodToResource(string method)
    {
        // Map MCP methods to rate limiting resources
        return method switch
        {
            "tools/call" => _options.UseDetailedResources ? "tools/call" : "tools",
            "tools/list" => _options.UseDetailedResources ? "tools/list" : "tools",
            "resources/read" => _options.UseDetailedResources ? "resources/read" : "resources",
            "resources/list" => _options.UseDetailedResources ? "resources/list" : "resources",
            "resources/subscribe" => _options.UseDetailedResources ? "resources/subscribe" : "resources",
            "resources/unsubscribe" => _options.UseDetailedResources ? "resources/unsubscribe" : "resources",
            "prompts/get" => _options.UseDetailedResources ? "prompts/get" : "prompts",
            "prompts/list" => _options.UseDetailedResources ? "prompts/list" : "prompts",
            "completion/complete" => "completion",
            "initialize" => "control",
            "ping" => "control",
            "cancel" => "control",
            _ => "other"
        };
    }
}

/// <summary>
/// Configuration options for rate limiting middleware.
/// </summary>
public class RateLimitingMiddlewareOptions
{
    /// <summary>
    /// Gets or sets whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use detailed resource names (e.g., "tools/call" vs "tools").
    /// </summary>
    public bool UseDetailedResources { get; set; } = true;

    /// <summary>
    /// Gets or sets the threshold for warning logs (requests remaining).
    /// </summary>
    public int WarningThreshold { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to include rate limit headers in responses.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log all rate limit checks (verbose).
    /// </summary>
    public bool LogAllChecks { get; set; }
}

/// <summary>
/// Context for extracting rate limit identifier.
/// </summary>
public record RateLimitContext
{
    /// <summary>
    /// Gets the IP address of the request.
    /// </summary>
    public IPAddress? IpAddress { get; init; }

    /// <summary>
    /// Gets the session ID.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the API key.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets additional context data.
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; init; }
}