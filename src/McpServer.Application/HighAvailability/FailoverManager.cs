using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Application.HighAvailability;

/// <summary>
/// Implementation of failover manager with load balancing.
/// </summary>
public class FailoverManager<T> : IFailoverManager<T> where T : class
{
    private readonly ILogger<FailoverManager<T>> _logger;
    private readonly FailoverOptions _options;
    private readonly ILoadBalancer<T> _loadBalancer;
    private readonly ConcurrentDictionary<T, InstanceInfo<T>> _instances = new();
    private readonly object _primaryLock = new();
    
    private long _totalOperations = 0;
    private long _successfulOperations = 0;
    private long _failedOperations = 0;
    private long _failoverCount = 0;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="FailoverManager{T}"/> class.
    /// </summary>
    public FailoverManager(
        ILogger<FailoverManager<T>> logger,
        IOptions<FailoverOptions> options,
        ILoadBalancer<T> loadBalancer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new FailoverOptions();
        _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
    }
    
    /// <inheritdoc/>
    public T? Primary
    {
        get
        {
            lock (_primaryLock)
            {
                var healthyInstances = GetHealthyInstances();
                return healthyInstances.OrderByDescending(i => i.Priority).FirstOrDefault()?.Instance;
            }
        }
    }
    
    /// <inheritdoc/>
    public IReadOnlyList<T> AvailableInstances
    {
        get
        {
            return GetHealthyInstances().Select(i => i.Instance).ToList();
        }
    }
    
    /// <inheritdoc/>
    public FailoverStatistics Statistics
    {
        get
        {
            var healthyCount = GetHealthyInstances().Count;
            
            return new FailoverStatistics
            {
                TotalOperations = Interlocked.Read(ref _totalOperations),
                SuccessfulOperations = Interlocked.Read(ref _successfulOperations),
                FailedOperations = Interlocked.Read(ref _failedOperations),
                FailoverCount = Interlocked.Read(ref _failoverCount),
                RegisteredInstances = _instances.Count,
                HealthyInstances = healthyCount
            };
        }
    }
    
    /// <inheritdoc/>
    public async Task<TResult> ExecuteAsync<TResult>(Func<T, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        Interlocked.Increment(ref _totalOperations);
        
        var healthyInstances = GetHealthyInstances();
        if (healthyInstances.Count == 0)
        {
            Interlocked.Increment(ref _failedOperations);
            throw new InvalidOperationException("No healthy instances available for failover");
        }
        
        var attemptedInstances = new HashSet<T>();
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < Math.Min(_options.MaxFailoverAttempts, healthyInstances.Count); attempt++)
        {
            var selectedInstance = _loadBalancer.SelectInstance(healthyInstances.Where(i => !attemptedInstances.Contains(i.Instance)).ToList());
            
            if (selectedInstance == null)
            {
                _logger.LogWarning("No more instances available for failover after {AttemptCount} attempts", attempt + 1);
                break;
            }
            
            attemptedInstances.Add(selectedInstance);
            
            // Get instance info for tracking
            InstanceInfo<T>? instanceInfo = null;
            _instances.TryGetValue(selectedInstance, out instanceInfo);
            
            try
            {
                // Increment active connections
                if (instanceInfo != null)
                {
                    instanceInfo.ActiveConnections++;
                }
                
                var result = await operation(selectedInstance);
                
                // Operation succeeded
                _loadBalancer.NotifyOperationCompleted(selectedInstance, true);
                Interlocked.Increment(ref _successfulOperations);
                
                if (instanceInfo != null)
                {
                    instanceInfo.TotalOperations++;
                    instanceInfo.ActiveConnections--;
                }
                
                if (attempt > 0)
                {
                    _logger.LogInformation("Operation succeeded on failover attempt {AttemptCount} using instance {Instance}", 
                        attempt + 1, selectedInstance.GetHashCode());
                }
                
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (instanceInfo != null)
                {
                    instanceInfo.FailedOperations++;
                    instanceInfo.ActiveConnections--;
                }
                
                _loadBalancer.NotifyOperationCompleted(selectedInstance, false);
                
                _logger.LogWarning(ex, "Operation failed on instance {Instance}, attempt {AttemptCount}", 
                    selectedInstance.GetHashCode(), attempt + 1);
                
                // Check if we should mark the instance as unhealthy
                if (ShouldMarkUnhealthy(ex))
                {
                    MarkUnhealthy(selectedInstance, ex.Message);
                    Interlocked.Increment(ref _failoverCount);
                }
                
                // Don't retry on certain types of exceptions
                if (!ShouldRetryOnFailover(ex))
                {
                    break;
                }
            }
        }
        
        Interlocked.Increment(ref _failedOperations);
        
        _logger.LogError(lastException, "All failover attempts exhausted for operation");
        throw lastException ?? new InvalidOperationException("All failover attempts failed");
    }
    
    /// <inheritdoc/>
    public async Task ExecuteAsync(Func<T, Task> operation, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async instance =>
        {
            await operation(instance);
            return true; // Dummy return value
        }, cancellationToken);
    }
    
    /// <inheritdoc/>
    public void RegisterInstance(T instance, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(instance);
        
        var instanceInfo = new InstanceInfo<T>
        {
            Instance = instance,
            Priority = priority,
            IsHealthy = true,
            LastHealthCheck = DateTimeOffset.UtcNow
        };
        
        _instances.AddOrUpdate(instance, instanceInfo, (_, existing) =>
        {
            // Create new instance info with updated priority since Priority is init-only
            return new InstanceInfo<T>
            {
                Instance = existing.Instance,
                Priority = priority,
                IsHealthy = existing.IsHealthy,
                UnhealthyReason = existing.UnhealthyReason,
                LastHealthCheck = existing.LastHealthCheck,
                ActiveConnections = existing.ActiveConnections,
                TotalOperations = existing.TotalOperations,
                FailedOperations = existing.FailedOperations
            };
        });
        
        _logger.LogInformation("Registered instance {Instance} with priority {Priority}", 
            instance.GetHashCode(), priority);
    }
    
    /// <inheritdoc/>
    public void UnregisterInstance(T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        
        if (_instances.TryRemove(instance, out _))
        {
            _logger.LogInformation("Unregistered instance {Instance}", instance.GetHashCode());
        }
    }
    
    /// <inheritdoc/>
    public void MarkUnhealthy(T instance, string reason)
    {
        ArgumentNullException.ThrowIfNull(instance);
        
        if (_instances.TryGetValue(instance, out var instanceInfo))
        {
            instanceInfo.IsHealthy = false;
            instanceInfo.UnhealthyReason = reason;
            instanceInfo.LastHealthCheck = DateTimeOffset.UtcNow;
            
            _logger.LogWarning("Marked instance {Instance} as unhealthy: {Reason}", 
                instance.GetHashCode(), reason);
        }
    }
    
    /// <inheritdoc/>
    public void MarkHealthy(T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        
        if (_instances.TryGetValue(instance, out var instanceInfo))
        {
            instanceInfo.IsHealthy = true;
            instanceInfo.UnhealthyReason = null;
            instanceInfo.LastHealthCheck = DateTimeOffset.UtcNow;
            
            _logger.LogInformation("Marked instance {Instance} as healthy", instance.GetHashCode());
        }
    }
    
    private List<InstanceInfo<T>> GetHealthyInstances()
    {
        return _instances.Values.Where(i => i.IsHealthy).ToList();
    }
    
    private static bool ShouldMarkUnhealthy(Exception exception)
    {
        return exception switch
        {
            TimeoutException => true,
            HttpRequestException => true,
            SocketException => true,
            InvalidOperationException => false, // Usually a usage error, not a health issue
            ArgumentException => false,
            _ => true // Mark as unhealthy for unknown exceptions
        };
    }
    
    private static bool ShouldRetryOnFailover(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => false,
            ArgumentNullException => false,
            ArgumentException => false,
            _ => true
        };
    }
}

/// <summary>
/// Load balancer implementations.
/// </summary>
public class RoundRobinLoadBalancer<T> : ILoadBalancer<T> where T : class
{
    private int _currentIndex = -1;
    
    /// <inheritdoc/>
    public LoadBalancingStrategy Strategy => LoadBalancingStrategy.RoundRobin;
    
    /// <inheritdoc/>
    public T? SelectInstance(IReadOnlyList<InstanceInfo<T>> availableInstances)
    {
        if (!availableInstances.Any())
            return null;
        
        var nextIndex = Interlocked.Increment(ref _currentIndex) % availableInstances.Count;
        return availableInstances[nextIndex].Instance;
    }
    
    /// <inheritdoc/>
    public void NotifyOperationCompleted(T instance, bool success)
    {
        // No action needed for round-robin
    }
}

/// <summary>
/// Priority-based load balancer with failover.
/// </summary>
public class PriorityLoadBalancer<T> : ILoadBalancer<T> where T : class
{
    /// <inheritdoc/>
    public LoadBalancingStrategy Strategy => LoadBalancingStrategy.Priority;
    
    /// <inheritdoc/>
    public T? SelectInstance(IReadOnlyList<InstanceInfo<T>> availableInstances)
    {
        if (!availableInstances.Any())
            return null;
        
        // Select the instance with the highest priority
        return availableInstances.OrderByDescending(i => i.Priority).First().Instance;
    }
    
    /// <inheritdoc/>
    public void NotifyOperationCompleted(T instance, bool success)
    {
        // No action needed for priority-based selection
    }
}

/// <summary>
/// Least connections load balancer.
/// </summary>
public class LeastConnectionsLoadBalancer<T> : ILoadBalancer<T> where T : class
{
    /// <inheritdoc/>
    public LoadBalancingStrategy Strategy => LoadBalancingStrategy.LeastConnections;
    
    /// <inheritdoc/>
    public T? SelectInstance(IReadOnlyList<InstanceInfo<T>> availableInstances)
    {
        if (!availableInstances.Any())
            return null;
        
        // Select the instance with the least active connections
        return availableInstances.OrderBy(i => i.ActiveConnections).First().Instance;
    }
    
    /// <inheritdoc/>
    public void NotifyOperationCompleted(T instance, bool success)
    {
        // Connection count is managed by the failover manager
    }
}

/// <summary>
/// Configuration options for failover behavior.
/// </summary>
public class FailoverOptions
{
    /// <summary>
    /// Gets or sets the maximum number of failover attempts.
    /// </summary>
    public int MaxFailoverAttempts { get; set; } = 3;
    
    /// <summary>
    /// Gets or sets the health check interval.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Gets or sets the timeout for marking an instance as unhealthy.
    /// </summary>
    public TimeSpan UnhealthyTimeout { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Gets or sets the load balancing strategy.
    /// </summary>
    public LoadBalancingStrategy LoadBalancingStrategy { get; set; } = LoadBalancingStrategy.Priority;
}