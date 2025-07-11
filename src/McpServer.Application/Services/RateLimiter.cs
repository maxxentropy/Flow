using System.Collections.Concurrent;
using McpServer.Domain.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Application.Services;

/// <summary>
/// Implementation of rate limiting using sliding window algorithm.
/// </summary>
public class RateLimiter : IRateLimiter
{
    private readonly ILogger<RateLimiter> _logger;
    private readonly RateLimitConfiguration _configuration;
    private readonly ConcurrentDictionary<string, SlidingWindow> _windows;
    private readonly object _cleanupLock = new();
    private DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow;

    public RateLimiter(ILogger<RateLimiter> logger, IOptions<RateLimitConfiguration> configuration)
    {
        _logger = logger;
        _configuration = configuration.Value;
        _windows = new ConcurrentDictionary<string, SlidingWindow>();
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(string identifier, string resource, CancellationToken cancellationToken = default)
    {
        // Check if identifier is in allowlist
        if (_configuration.IdentifierAllowlist.Contains(identifier))
        {
            return CreateAllowedResult(int.MaxValue, int.MaxValue);
        }

        // Periodic cleanup of old windows
        await CleanupOldWindowsAsync();

        var now = DateTimeOffset.UtcNow;
        
        // Check global limit first if configured
        if (_configuration.GlobalLimit.HasValue)
        {
            var globalKey = GetWindowKey(identifier, null);
            var globalWindow = _windows.GetOrAdd(globalKey, _ => new SlidingWindow(_configuration.GlobalWindowDuration));
            var globalCost = GetOperationCost(resource);
            
            var (globalAllowed, globalUsed) = globalWindow.CheckAndIncrement(now, _configuration.GlobalLimit.Value, globalCost, _configuration.UseSlidingWindow);
            
            if (!globalAllowed)
            {
                _logger.LogWarning("Global rate limit exceeded for {Identifier}. Used: {Used}/{Limit}", 
                    identifier, globalUsed, _configuration.GlobalLimit.Value);
                
                var globalResetsAt = globalWindow.GetResetTime(now, _configuration.GlobalWindowDuration);
                var retryAfter = globalResetsAt - now;
                
                return new RateLimitResult
                {
                    IsAllowed = false,
                    Remaining = 0,
                    Limit = _configuration.GlobalLimit.Value,
                    ResetsAt = globalResetsAt,
                    RetryAfter = retryAfter,
                    DenialReason = $"Global rate limit exceeded. {globalUsed}/{_configuration.GlobalLimit.Value} requests used."
                };
            }
        }
        
        // Check resource-specific limit
        var key = GetWindowKey(identifier, resource);
        var window = _windows.GetOrAdd(key, _ => CreateWindow(resource));

        // Get the appropriate limit configuration
        var (limit, windowDuration) = GetLimitConfiguration(resource);
        var cost = GetOperationCost(resource);

        // Check rate limit
        var (allowed, used) = window.CheckAndIncrement(now, limit, cost, _configuration.UseSlidingWindow);
        var remaining = Math.Max(0, limit - used);
        var resetsAt = window.GetResetTime(now, windowDuration);

        if (!allowed)
        {
            // If resource limit failed but global passed, we need to rollback the global increment
            if (_configuration.GlobalLimit.HasValue)
            {
                var globalKey = GetWindowKey(identifier, null);
                if (_windows.TryGetValue(globalKey, out var globalWindow))
                {
                    // Decrement the global counter since the request is denied
                    globalWindow.Increment(now, -cost, _configuration.GlobalLimit.Value, _configuration.UseSlidingWindow);
                }
            }
            
            _logger.LogWarning("Rate limit exceeded for {Identifier} on resource {Resource}. Used: {Used}/{Limit}", 
                identifier, resource, used, limit);

            var retryAfter = resetsAt - now;
            return new RateLimitResult
            {
                IsAllowed = false,
                Remaining = 0,
                Limit = limit,
                ResetsAt = resetsAt,
                RetryAfter = retryAfter,
                DenialReason = GetDenialMessage(resource, used, limit)
            };
        }

        _logger.LogDebug("Rate limit check passed for {Identifier} on resource {Resource}. Used: {Used}/{Limit}", 
            identifier, resource, used, limit);

        return new RateLimitResult
        {
            IsAllowed = true,
            Remaining = remaining,
            Limit = limit,
            ResetsAt = resetsAt
        };
    }

    public Task RecordRequestAsync(string identifier, string resource, int cost = 1, CancellationToken cancellationToken = default)
    {
        if (_configuration.IdentifierAllowlist.Contains(identifier))
        {
            return Task.CompletedTask;
        }

        var now = DateTimeOffset.UtcNow;
        
        // Record to global window if configured
        if (_configuration.GlobalLimit.HasValue)
        {
            var globalKey = GetWindowKey(identifier, null);
            var globalWindow = _windows.GetOrAdd(globalKey, _ => new SlidingWindow(_configuration.GlobalWindowDuration));
            globalWindow.Increment(now, cost, _configuration.GlobalLimit.Value, _configuration.UseSlidingWindow);
        }
        
        // Record to resource-specific window
        var key = GetWindowKey(identifier, resource);
        var window = _windows.GetOrAdd(key, _ => CreateWindow(resource));
        var (limit, _) = GetLimitConfiguration(resource);

        window.Increment(now, cost, limit, _configuration.UseSlidingWindow);
        
        return Task.CompletedTask;
    }

    public Task<RateLimitStatus> GetStatusAsync(string identifier, string? resource = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var status = new RateLimitStatus { Identifier = identifier };

        // Get global limit if configured
        if (_configuration.GlobalLimit.HasValue)
        {
            var globalKey = GetWindowKey(identifier, null);
            if (_windows.TryGetValue(globalKey, out var globalWindow))
            {
                var used = globalWindow.GetCurrentUsage(now, _configuration.GlobalWindowDuration, _configuration.UseSlidingWindow);
                status = status with { GlobalLimit = new ResourceRateLimit
                {
                    Resource = "global",
                    Used = used,
                    Limit = _configuration.GlobalLimit.Value,
                    ResetsAt = globalWindow.GetResetTime(now, _configuration.GlobalWindowDuration),
                    WindowDuration = _configuration.GlobalWindowDuration
                }};
            }
        }

        // Get resource-specific limits
        if (resource != null)
        {
            var key = GetWindowKey(identifier, resource);
            if (_windows.TryGetValue(key, out var window))
            {
                var (limit, windowDuration) = GetLimitConfiguration(resource);
                var used = window.GetCurrentUsage(now, windowDuration, _configuration.UseSlidingWindow);
                status.ResourceLimits[resource] = new ResourceRateLimit
                {
                    Resource = resource,
                    Used = used,
                    Limit = limit,
                    ResetsAt = window.GetResetTime(now, windowDuration),
                    WindowDuration = windowDuration
                };
            }
        }
        else
        {
            // Get all resource limits for this identifier
            foreach (var kvp in _windows.Where(w => w.Key.StartsWith($"{identifier}:", StringComparison.Ordinal)))
            {
                var parts = kvp.Key.Split(':');
                if (parts.Length == 2)
                {
                    var res = parts[1];
                    var (limit, windowDuration) = GetLimitConfiguration(res);
                    var used = kvp.Value.GetCurrentUsage(now, windowDuration, _configuration.UseSlidingWindow);
                    status.ResourceLimits[res] = new ResourceRateLimit
                    {
                        Resource = res,
                        Used = used,
                        Limit = limit,
                        ResetsAt = kvp.Value.GetResetTime(now, windowDuration),
                        WindowDuration = windowDuration
                    };
                }
            }
        }

        return Task.FromResult(status);
    }

    public Task ResetAsync(string identifier, string? resource = null, CancellationToken cancellationToken = default)
    {
        if (resource != null)
        {
            var key = GetWindowKey(identifier, resource);
            _windows.TryRemove(key, out _);
        }
        else
        {
            // Remove all windows for this identifier
            var keysToRemove = _windows.Keys.Where(k => k.StartsWith($"{identifier}:", StringComparison.Ordinal) || k == identifier).ToList();
            foreach (var key in keysToRemove)
            {
                _windows.TryRemove(key, out _);
            }
        }

        _logger.LogInformation("Reset rate limits for {Identifier} {Resource}", 
            identifier, resource ?? "all resources");

        return Task.CompletedTask;
    }

    private static string GetWindowKey(string identifier, string? resource)
    {
        return resource != null ? $"{identifier}:{resource}" : identifier;
    }

    private SlidingWindow CreateWindow(string resource)
    {
        var (_, windowDuration) = GetLimitConfiguration(resource);
        return new SlidingWindow(windowDuration);
    }

    private (int limit, TimeSpan windowDuration) GetLimitConfiguration(string resource)
    {
        // Check for resource-specific configuration
        if (_configuration.ResourceLimits.TryGetValue(resource, out var resourceConfig))
        {
            return (resourceConfig.Limit, resourceConfig.WindowDuration);
        }

        // Fall back to global configuration
        if (_configuration.GlobalLimit.HasValue)
        {
            return (_configuration.GlobalLimit.Value, _configuration.GlobalWindowDuration);
        }

        // Default if no configuration
        return (100, TimeSpan.FromMinutes(1));
    }

    private int GetOperationCost(string resource)
    {
        return _configuration.OperationCosts.TryGetValue(resource, out var cost) ? cost : 1;
    }

    private string GetDenialMessage(string resource, int used, int limit)
    {
        if (_configuration.ResourceLimits.TryGetValue(resource, out var config) && 
            !string.IsNullOrEmpty(config.ExceededMessage))
        {
            return config.ExceededMessage;
        }

        return $"Rate limit exceeded for {resource}. {used}/{limit} requests used.";
    }

    private static RateLimitResult CreateAllowedResult(int remaining, int limit)
    {
        return new RateLimitResult
        {
            IsAllowed = true,
            Remaining = remaining,
            Limit = limit,
            ResetsAt = DateTimeOffset.UtcNow.AddMinutes(1)
        };
    }

    private async Task CleanupOldWindowsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastCleanup < TimeSpan.FromMinutes(5))
        {
            return;
        }

        await Task.Run(() =>
        {
            lock (_cleanupLock)
            {
                if (now - _lastCleanup < TimeSpan.FromMinutes(5))
                {
                    return;
                }

                var cutoff = now.AddHours(-1);
                var keysToRemove = _windows
                    .Where(kvp => kvp.Value.LastAccessed < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _windows.TryRemove(key, out _);
                }

                if (keysToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} old rate limit windows", keysToRemove.Count);
                }

                _lastCleanup = now;
            }
        });
    }

    /// <summary>
    /// Internal sliding window implementation.
    /// </summary>
    private class SlidingWindow
    {
        private readonly TimeSpan _windowDuration;
        private readonly ConcurrentBag<(DateTimeOffset Timestamp, int Cost)> _requests;
        private readonly object _lock = new();
        private int _fixedWindowCount;
        private DateTimeOffset _fixedWindowStart;

        public DateTimeOffset LastAccessed { get; private set; }

        public SlidingWindow(TimeSpan windowDuration)
        {
            _windowDuration = windowDuration;
            _requests = new ConcurrentBag<(DateTimeOffset, int)>();
            _fixedWindowStart = DateTimeOffset.UtcNow;
            LastAccessed = DateTimeOffset.UtcNow;
        }

        public (bool allowed, int currentUsage) CheckAndIncrement(DateTimeOffset now, int limit, int cost, bool useSlidingWindow)
        {
            lock (_lock)
            {
                LastAccessed = now;

                if (useSlidingWindow)
                {
                    // Sliding window algorithm
                    var windowStart = now - _windowDuration;
                    var validRequests = _requests.Where(r => r.Timestamp > windowStart).ToList();
                    var currentUsage = validRequests.Sum(r => r.Cost);

                    if (currentUsage + cost > limit)
                    {
                        return (false, currentUsage);
                    }

                    _requests.Add((now, cost));
                    
                    // Clean up old requests
                    foreach (var request in _requests.Where(r => r.Timestamp <= windowStart).ToList())
                    {
                        _requests.TryTake(out _);
                    }

                    return (true, currentUsage + cost);
                }
                else
                {
                    // Fixed window algorithm
                    if (now >= _fixedWindowStart + _windowDuration)
                    {
                        _fixedWindowStart = now;
                        _fixedWindowCount = 0;
                    }

                    if (_fixedWindowCount + cost > limit)
                    {
                        return (false, _fixedWindowCount);
                    }

                    _fixedWindowCount += cost;
                    return (true, _fixedWindowCount);
                }
            }
        }

        public void Increment(DateTimeOffset now, int cost, int limit, bool useSlidingWindow)
        {
            CheckAndIncrement(now, limit, cost, useSlidingWindow);
        }

        public int GetCurrentUsage(DateTimeOffset now, TimeSpan windowDuration, bool useSlidingWindow)
        {
            lock (_lock)
            {
                if (useSlidingWindow)
                {
                    var windowStart = now - windowDuration;
                    return _requests.Where(r => r.Timestamp > windowStart).Sum(r => r.Cost);
                }
                else
                {
                    if (now >= _fixedWindowStart + _windowDuration)
                    {
                        return 0;
                    }
                    return _fixedWindowCount;
                }
            }
        }

        public DateTimeOffset GetResetTime(DateTimeOffset now, TimeSpan windowDuration)
        {
            lock (_lock)
            {
                return _fixedWindowStart + windowDuration;
            }
        }
    }
}