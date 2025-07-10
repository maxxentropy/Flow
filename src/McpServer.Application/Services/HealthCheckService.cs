using System.Collections.Concurrent;
using System.Diagnostics;
using McpServer.Domain.Monitoring;
using McpServer.Domain.Security;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Service for performing health checks.
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IMcpServer _mcpServer;
    private readonly ISessionService _sessionService;
    private readonly IUserRepository _userRepository;
    private readonly ConcurrentDictionary<string, Func<CancellationToken, Task<ComponentHealthResult>>> _checks = new();
    private readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckService"/> class.
    /// </summary>
    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        IMcpServer mcpServer,
        ISessionService sessionService,
        IUserRepository userRepository)
    {
        _logger = logger;
        _mcpServer = mcpServer;
        _sessionService = sessionService;
        _userRepository = userRepository;
        
        RegisterDefaultChecks();
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new HealthCheckResult
        {
            Timestamp = DateTime.UtcNow
        };

        var tasks = _checks.Select(async kvp =>
        {
            try
            {
                var componentResult = await kvp.Value(cancellationToken);
                return (kvp.Key, componentResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check {CheckName} failed", kvp.Key);
                return (kvp.Key, new ComponentHealthResult
                {
                    Name = kvp.Key,
                    Status = HealthStatus.Unhealthy,
                    Error = ex.Message,
                    ResponseTime = TimeSpan.Zero
                });
            }
        });

        var results = await Task.WhenAll(tasks);
        
        foreach (var (name, componentResult) in results)
        {
            result.Components[name] = componentResult;
        }

        // Determine overall status
        if (result.Components.Values.Any(c => c.Status == HealthStatus.Unhealthy))
        {
            result.Status = HealthStatus.Unhealthy;
        }
        else if (result.Components.Values.Any(c => c.Status == HealthStatus.Degraded))
        {
            result.Status = HealthStatus.Degraded;
        }
        else
        {
            result.Status = HealthStatus.Healthy;
        }

        // Collect system info
        var process = Process.GetCurrentProcess();
        result.System = new SystemInfo
        {
            Version = "1.0.0",
            Uptime = DateTime.UtcNow - _startTime,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Hostname = Environment.MachineName,
            Memory = new MemoryInfo
            {
                UsedBytes = process.WorkingSet64,
                TotalBytes = process.WorkingSet64, // Would need OS-specific code for total
                AvailableBytes = 0 // Would need OS-specific code
            },
            Cpu = new CpuInfo
            {
                UsagePercent = 0, // Would need OS-specific code
                ProcessorCount = Environment.ProcessorCount,
                ProcessCpuTime = process.TotalProcessorTime
            }
        };

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        _logger.LogInformation("Health check completed: {Status} in {Duration}ms",
            result.Status, result.Duration.TotalMilliseconds);

        return result;
    }

    /// <inheritdoc/>
    public async Task<ComponentHealthResult> CheckComponentAsync(string checkName, CancellationToken cancellationToken = default)
    {
        if (!_checks.TryGetValue(checkName, out var check))
        {
            return new ComponentHealthResult
            {
                Name = checkName,
                Status = HealthStatus.Unhealthy,
                Error = $"Health check '{checkName}' not found"
            };
        }

        try
        {
            return await check(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check {CheckName} failed", checkName);
            return new ComponentHealthResult
            {
                Name = checkName,
                Status = HealthStatus.Unhealthy,
                Error = ex.Message,
                ResponseTime = TimeSpan.Zero
            };
        }
    }

    /// <inheritdoc/>
    public void RegisterCheck(string name, Func<CancellationToken, Task<ComponentHealthResult>> check)
    {
        _checks[name] = check;
        _logger.LogDebug("Registered health check: {CheckName}", name);
    }

    /// <inheritdoc/>
    public void UnregisterCheck(string name)
    {
        if (_checks.TryRemove(name, out _))
        {
            _logger.LogDebug("Unregistered health check: {CheckName}", name);
        }
    }

    private void RegisterDefaultChecks()
    {
        // MCP Server check
        RegisterCheck("mcp_server", ct =>
        {
            var stopwatch = Stopwatch.StartNew();
            var isInitialized = _mcpServer.IsInitialized;
            stopwatch.Stop();

            return Task.FromResult(new ComponentHealthResult
            {
                Name = "mcp_server",
                Status = isInitialized ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                ResponseTime = stopwatch.Elapsed,
                Description = isInitialized ? "MCP Server is initialized" : "MCP Server is not initialized",
                Data = new Dictionary<string, object>
                {
                    ["initialized"] = isInitialized
                }
            });
        });

        // Session service check
        RegisterCheck("session_service", async ct =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Try to clean up expired sessions as a health check
                var cleaned = await _sessionService.CleanupExpiredSessionsAsync(ct);
                stopwatch.Stop();

                return new ComponentHealthResult
                {
                    Name = "session_service",
                    Status = HealthStatus.Healthy,
                    ResponseTime = stopwatch.Elapsed,
                    Description = "Session service is operational",
                    Data = new Dictionary<string, object>
                    {
                        ["expired_sessions_cleaned"] = cleaned
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new ComponentHealthResult
                {
                    Name = "session_service",
                    Status = HealthStatus.Unhealthy,
                    ResponseTime = stopwatch.Elapsed,
                    Error = ex.Message
                };
            }
        });

        // User repository check
        RegisterCheck("user_repository", async ct =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Try to get a known user as a health check
                var user = await _userRepository.GetByIdAsync("admin", ct);
                stopwatch.Stop();

                return new ComponentHealthResult
                {
                    Name = "user_repository",
                    Status = user != null ? HealthStatus.Healthy : HealthStatus.Degraded,
                    ResponseTime = stopwatch.Elapsed,
                    Description = user != null ? "User repository is operational" : "User repository is operational but test user not found"
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new ComponentHealthResult
                {
                    Name = "user_repository",
                    Status = HealthStatus.Unhealthy,
                    ResponseTime = stopwatch.Elapsed,
                    Error = ex.Message
                };
            }
        });

        // Memory check
        RegisterCheck("memory", ct =>
        {
            var stopwatch = Stopwatch.StartNew();
            var process = Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            stopwatch.Stop();

            var status = memoryMB switch
            {
                < 500 => HealthStatus.Healthy,
                < 1000 => HealthStatus.Degraded,
                _ => HealthStatus.Unhealthy
            };

            return Task.FromResult(new ComponentHealthResult
            {
                Name = "memory",
                Status = status,
                ResponseTime = stopwatch.Elapsed,
                Description = $"Memory usage: {memoryMB}MB",
                Data = new Dictionary<string, object>
                {
                    ["memory_mb"] = memoryMB,
                    ["gc_gen0"] = GC.CollectionCount(0),
                    ["gc_gen1"] = GC.CollectionCount(1),
                    ["gc_gen2"] = GC.CollectionCount(2)
                }
            });
        });
    }
}