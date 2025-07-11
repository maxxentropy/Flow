using System.Net;

namespace McpServer.Domain.RateLimiting;

/// <summary>
/// Interface for rate limiting functionality.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Checks if a request is allowed based on rate limiting rules.
    /// </summary>
    /// <param name="identifier">The identifier for rate limiting (e.g., IP address, API key, session ID).</param>
    /// <param name="resource">The resource being accessed (e.g., "tools/call", "resources/read").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A rate limit result indicating if the request is allowed.</returns>
    Task<RateLimitResult> CheckRateLimitAsync(string identifier, string resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a request was made.
    /// </summary>
    /// <param name="identifier">The identifier for rate limiting.</param>
    /// <param name="resource">The resource being accessed.</param>
    /// <param name="cost">The cost of the request (default is 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordRequestAsync(string identifier, string resource, int cost = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current rate limit status for an identifier.
    /// </summary>
    /// <param name="identifier">The identifier to check.</param>
    /// <param name="resource">The resource to check (optional, null for global limits).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current rate limit status.</returns>
    Task<RateLimitStatus> GetStatusAsync(string identifier, string? resource = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets rate limits for a specific identifier.
    /// </summary>
    /// <param name="identifier">The identifier to reset.</param>
    /// <param name="resource">The resource to reset (optional, null to reset all).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResetAsync(string identifier, string? resource = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a rate limit check.
/// </summary>
public record RateLimitResult
{
    /// <summary>
    /// Gets whether the request is allowed.
    /// </summary>
    public required bool IsAllowed { get; init; }

    /// <summary>
    /// Gets the number of requests remaining in the current window.
    /// </summary>
    public required int Remaining { get; init; }

    /// <summary>
    /// Gets the total limit for the current window.
    /// </summary>
    public required int Limit { get; init; }

    /// <summary>
    /// Gets when the current rate limit window resets.
    /// </summary>
    public required DateTimeOffset ResetsAt { get; init; }

    /// <summary>
    /// Gets the retry-after duration if the request is not allowed.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    /// Gets a message explaining why the request was denied (if applicable).
    /// </summary>
    public string? DenialReason { get; init; }
}

/// <summary>
/// Current rate limit status for an identifier.
/// </summary>
public record RateLimitStatus
{
    /// <summary>
    /// Gets the identifier being tracked.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets the resource-specific limits and usage.
    /// </summary>
    public Dictionary<string, ResourceRateLimit> ResourceLimits { get; init; } = new();

    /// <summary>
    /// Gets the global rate limit (if any).
    /// </summary>
    public ResourceRateLimit? GlobalLimit { get; init; }
}

/// <summary>
/// Rate limit information for a specific resource.
/// </summary>
public record ResourceRateLimit
{
    /// <summary>
    /// Gets the resource name.
    /// </summary>
    public required string Resource { get; init; }

    /// <summary>
    /// Gets the number of requests used in the current window.
    /// </summary>
    public required int Used { get; init; }

    /// <summary>
    /// Gets the limit for the current window.
    /// </summary>
    public required int Limit { get; init; }

    /// <summary>
    /// Gets the remaining requests in the current window.
    /// </summary>
    public int Remaining => Math.Max(0, Limit - Used);

    /// <summary>
    /// Gets when the current window resets.
    /// </summary>
    public required DateTimeOffset ResetsAt { get; init; }

    /// <summary>
    /// Gets the window duration.
    /// </summary>
    public required TimeSpan WindowDuration { get; init; }
}

/// <summary>
/// Configuration for rate limiting.
/// </summary>
public class RateLimitConfiguration
{
    /// <summary>
    /// Gets or sets the global rate limit (requests per window).
    /// </summary>
    public int? GlobalLimit { get; set; }

    /// <summary>
    /// Gets or sets the global window duration.
    /// </summary>
    public TimeSpan GlobalWindowDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets resource-specific rate limits.
    /// </summary>
    public Dictionary<string, ResourceRateLimitConfig> ResourceLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to use sliding window algorithm (vs fixed window).
    /// </summary>
    public bool UseSlidingWindow { get; set; } = true;

    /// <summary>
    /// Gets or sets custom cost functions for specific operations.
    /// </summary>
    public Dictionary<string, int> OperationCosts { get; set; } = new();

    /// <summary>
    /// Gets or sets IP address allowlist (no rate limiting).
    /// </summary>
    public HashSet<IPAddress> IpAllowlist { get; set; } = new();

    /// <summary>
    /// Gets or sets identifier allowlist (no rate limiting).
    /// </summary>
    public HashSet<string> IdentifierAllowlist { get; set; } = new();
}

/// <summary>
/// Configuration for a specific resource's rate limit.
/// </summary>
public class ResourceRateLimitConfig
{
    /// <summary>
    /// Gets or sets the limit for this resource.
    /// </summary>
    public required int Limit { get; set; }

    /// <summary>
    /// Gets or sets the window duration for this resource.
    /// </summary>
    public required TimeSpan WindowDuration { get; set; }

    /// <summary>
    /// Gets or sets whether this resource has burst allowance.
    /// </summary>
    public int? BurstLimit { get; set; }

    /// <summary>
    /// Gets or sets custom error message when limit is exceeded.
    /// </summary>
    public string? ExceededMessage { get; set; }
}