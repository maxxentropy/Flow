using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpServer.Application.Caching;
using McpServer.Domain.Transport;

namespace McpServer.Application.HighAvailability;

/// <summary>
/// Implementation of comprehensive health check service.
/// </summary>
public class HealthCheckService : IHealthCheckService, IDisposable
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly HealthCheckServiceOptions _options;
    private readonly ConcurrentDictionary<string, IHealthCheck> _healthChecks = new();
    private readonly ConcurrentDictionary<string, HealthCheckResult> _lastResults = new();
    private readonly SemaphoreSlim _checkSemaphore = new(1, 1);
    
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckService"/> class.
    /// </summary>
    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        IOptions<HealthCheckServiceOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new HealthCheckServiceOptions();
        
        // Register default health checks
        RegisterDefaultHealthChecks();
    }
    
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        await _checkSemaphore.WaitAsync(cancellationToken);
        try
        {
            var results = new List<HealthCheckResult>();
            var overallStatus = HealthStatus.Healthy;
            var data = new Dictionary<string, object>();
            
            // Execute all health checks in parallel
            var tasks = _healthChecks.Select(async kvp =>
            {
                try
                {
                    var result = await ExecuteHealthCheckAsync(kvp.Key, kvp.Value, cancellationToken);
                    _lastResults[kvp.Key] = result;
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Health check failed for {ComponentName}", kvp.Key);
                    var errorResult = new HealthCheckResult(HealthStatus.Unhealthy, $"Health check failed: {ex.Message}", ex);
                    _lastResults[kvp.Key] = errorResult;
                    return errorResult;
                }
            });
            
            results.AddRange(await Task.WhenAll(tasks));
            
            // Determine overall status
            if (results.Any(r => r.Status == HealthStatus.Unhealthy))
            {
                overallStatus = HealthStatus.Unhealthy;
            }
            else if (results.Any(r => r.Status == HealthStatus.Degraded))
            {
                overallStatus = HealthStatus.Degraded;
            }
            
            // Aggregate data
            data["componentCount"] = results.Count;
            data["healthyCount"] = results.Count(r => r.Status == HealthStatus.Healthy);
            data["degradedCount"] = results.Count(r => r.Status == HealthStatus.Degraded);
            data["unhealthyCount"] = results.Count(r => r.Status == HealthStatus.Unhealthy);
            data["lastChecked"] = DateTimeOffset.UtcNow;
            
            var description = $"Overall health: {overallStatus}. {data["healthyCount"]}/{data["componentCount"]} components healthy.";
            
            _logger.LogInformation("Health check completed: {Status} - {Description}", overallStatus, description);
            
            return new HealthCheckResult(overallStatus, description, null, data);
        }
        finally
        {
            _checkSemaphore.Release();
        }
    }
    
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckComponentHealthAsync(string componentName, CancellationToken cancellationToken = default)
    {
        if (!_healthChecks.TryGetValue(componentName, out var healthCheck))
        {
            return new HealthCheckResult(HealthStatus.Unhealthy, $"Component '{componentName}' not found");
        }
        
        try
        {
            var result = await ExecuteHealthCheckAsync(componentName, healthCheck, cancellationToken);
            _lastResults[componentName] = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for component {ComponentName}", componentName);
            var errorResult = new HealthCheckResult(HealthStatus.Unhealthy, $"Health check failed: {ex.Message}", ex);
            _lastResults[componentName] = errorResult;
            return errorResult;
        }
    }
    
    /// <inheritdoc/>
    public Task<Dictionary<string, HealthCheckResult>> GetAllComponentHealthAsync()
    {
        var results = _lastResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return Task.FromResult(results);
    }
    
    /// <inheritdoc/>
    public void RegisterHealthCheck(string name, IHealthCheck healthCheck, params string[] tags)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Health check name cannot be null or empty", nameof(name));
        
        ArgumentNullException.ThrowIfNull(healthCheck);
        
        _healthChecks[name] = healthCheck;
        _logger.LogInformation("Registered health check: {Name} with tags: {Tags}", name, string.Join(", ", tags));
    }
    
    /// <summary>
    /// Releases all resources used by the health check service.
    /// </summary>
    public void Dispose()
    {
        _checkSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
    
    private async Task<HealthCheckResult> ExecuteHealthCheckAsync(string name, IHealthCheck healthCheck, CancellationToken cancellationToken)
    {
        var timeout = _options.HealthCheckTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration(name, healthCheck, HealthStatus.Unhealthy, null)
            };
            
            var result = await healthCheck.CheckHealthAsync(context, timeoutCts.Token);
            stopwatch.Stop();
            
            var enhancedData = new Dictionary<string, object>(result.Data ?? new Dictionary<string, object>())
            {
                ["checkDuration"] = stopwatch.ElapsedMilliseconds,
                ["timestamp"] = DateTimeOffset.UtcNow
            };
            
            _logger.LogDebug("Health check {Name} completed in {Duration}ms with status {Status}", 
                name, stopwatch.ElapsedMilliseconds, result.Status);
            
            return new HealthCheckResult(result.Status, result.Description, result.Exception, enhancedData);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning("Health check {Name} timed out after {Timeout}ms", name, timeout.TotalMilliseconds);
            return new HealthCheckResult(HealthStatus.Unhealthy, $"Health check timed out after {timeout.TotalMilliseconds}ms");
        }
    }
    
    private void RegisterDefaultHealthChecks()
    {
        // Register memory health check
        RegisterHealthCheck("memory", new MemoryHealthCheck(), "system", "memory");
        
        // Register startup health check
        RegisterHealthCheck("startup", new StartupHealthCheck(), "system", "startup");
    }
}

/// <summary>
/// Configuration options for health check service.
/// </summary>
public class HealthCheckServiceOptions
{
    /// <summary>
    /// Gets or sets the timeout for individual health checks.
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Gets or sets whether to include detailed error information.
    /// </summary>
    public bool IncludeDetailedErrors { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the maximum number of concurrent health checks.
    /// </summary>
    public int MaxConcurrentChecks { get; set; } = 10;
}

/// <summary>
/// Memory usage health check.
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var memoryUsed = GC.GetTotalMemory(false);
        var memoryUsedMB = memoryUsed / 1024 / 1024;
        
        var data = new Dictionary<string, object>
        {
            ["memoryUsedBytes"] = memoryUsed,
            ["memoryUsedMB"] = memoryUsedMB,
            ["gen0Collections"] = GC.CollectionCount(0),
            ["gen1Collections"] = GC.CollectionCount(1),
            ["gen2Collections"] = GC.CollectionCount(2)
        };
        
        var status = memoryUsedMB switch
        {
            > 1000 => HealthStatus.Unhealthy,
            > 500 => HealthStatus.Degraded,
            _ => HealthStatus.Healthy
        };
        
        var description = $"Memory usage: {memoryUsedMB:F1} MB";
        
        return Task.FromResult(new HealthCheckResult(status, description, null, data));
    }
}

/// <summary>
/// Startup readiness health check.
/// </summary>
public class StartupHealthCheck : IHealthCheck
{
    private static bool _isReady = false;
    private static readonly object _lock = new();
    
    public static void MarkAsReady()
    {
        lock (_lock)
        {
            _isReady = true;
        }
    }
    
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var status = _isReady ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            var description = _isReady ? "Application is ready" : "Application is starting up";
            
            var data = new Dictionary<string, object>
            {
                ["isReady"] = _isReady,
                ["startupTime"] = DateTimeOffset.UtcNow
            };
            
            return Task.FromResult(new HealthCheckResult(status, description, null, data));
        }
    }
}