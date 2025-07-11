namespace McpServer.Application.HighAvailability;

/// <summary>
/// Manages failover between multiple service instances.
/// </summary>
public interface IFailoverManager<T> where T : class
{
    /// <summary>
    /// Gets the current primary instance.
    /// </summary>
    T? Primary { get; }
    
    /// <summary>
    /// Gets all available instances.
    /// </summary>
    IReadOnlyList<T> AvailableInstances { get; }
    
    /// <summary>
    /// Gets the current failover statistics.
    /// </summary>
    FailoverStatistics Statistics { get; }
    
    /// <summary>
    /// Executes an operation with automatic failover.
    /// </summary>
    /// <typeparam name="TResult">Return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the operation.</returns>
    Task<TResult> ExecuteAsync<TResult>(Func<T, Task<TResult>> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an operation with automatic failover.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteAsync(Func<T, Task> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registers a service instance.
    /// </summary>
    /// <param name="instance">Service instance to register.</param>
    /// <param name="priority">Priority of the instance (higher = more preferred).</param>
    void RegisterInstance(T instance, int priority = 0);
    
    /// <summary>
    /// Unregisters a service instance.
    /// </summary>
    /// <param name="instance">Service instance to unregister.</param>
    void UnregisterInstance(T instance);
    
    /// <summary>
    /// Marks an instance as unhealthy.
    /// </summary>
    /// <param name="instance">Instance to mark as unhealthy.</param>
    /// <param name="reason">Reason for marking unhealthy.</param>
    void MarkUnhealthy(T instance, string reason);
    
    /// <summary>
    /// Marks an instance as healthy.
    /// </summary>
    /// <param name="instance">Instance to mark as healthy.</param>
    void MarkHealthy(T instance);
}

/// <summary>
/// Load balancing strategies.
/// </summary>
public enum LoadBalancingStrategy
{
    /// <summary>
    /// Round-robin selection.
    /// </summary>
    RoundRobin,
    
    /// <summary>
    /// Random selection.
    /// </summary>
    Random,
    
    /// <summary>
    /// Least connections selection.
    /// </summary>
    LeastConnections,
    
    /// <summary>
    /// Priority-based selection with failover.
    /// </summary>
    Priority
}

/// <summary>
/// Statistics for failover operations.
/// </summary>
public class FailoverStatistics
{
    /// <summary>
    /// Gets the total number of operations executed.
    /// </summary>
    public long TotalOperations { get; init; }
    
    /// <summary>
    /// Gets the number of successful operations.
    /// </summary>
    public long SuccessfulOperations { get; init; }
    
    /// <summary>
    /// Gets the number of failed operations.
    /// </summary>
    public long FailedOperations { get; init; }
    
    /// <summary>
    /// Gets the number of failover events.
    /// </summary>
    public long FailoverCount { get; init; }
    
    /// <summary>
    /// Gets the number of registered instances.
    /// </summary>
    public int RegisteredInstances { get; init; }
    
    /// <summary>
    /// Gets the number of healthy instances.
    /// </summary>
    public int HealthyInstances { get; init; }
    
    /// <summary>
    /// Gets the success rate percentage.
    /// </summary>
    public double SuccessRate => TotalOperations == 0 ? 0 : (double)SuccessfulOperations / TotalOperations * 100;
}

/// <summary>
/// Instance information for failover management.
/// </summary>
public class InstanceInfo<T> where T : class
{
    /// <summary>
    /// Gets the service instance.
    /// </summary>
    public T Instance { get; init; } = default!;
    
    /// <summary>
    /// Gets the priority of the instance.
    /// </summary>
    public int Priority { get; init; }
    
    /// <summary>
    /// Gets whether the instance is currently healthy.
    /// </summary>
    public bool IsHealthy { get; set; } = true;
    
    /// <summary>
    /// Gets the reason for being unhealthy (if applicable).
    /// </summary>
    public string? UnhealthyReason { get; set; }
    
    /// <summary>
    /// Gets the last health check time.
    /// </summary>
    public DateTimeOffset LastHealthCheck { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Gets the number of active connections/operations.
    /// </summary>
    public int ActiveConnections { get; set; }
    
    /// <summary>
    /// Gets the total number of operations processed.
    /// </summary>
    public long TotalOperations { get; set; }
    
    /// <summary>
    /// Gets the number of failed operations.
    /// </summary>
    public long FailedOperations { get; set; }
}

/// <summary>
/// Load balancer for distributing operations across multiple instances.
/// </summary>
public interface ILoadBalancer<T> where T : class
{
    /// <summary>
    /// Gets the load balancing strategy.
    /// </summary>
    LoadBalancingStrategy Strategy { get; }
    
    /// <summary>
    /// Selects the next instance for operation execution.
    /// </summary>
    /// <param name="availableInstances">Available healthy instances.</param>
    /// <returns>Selected instance or null if none available.</returns>
    T? SelectInstance(IReadOnlyList<InstanceInfo<T>> availableInstances);
    
    /// <summary>
    /// Notifies the load balancer that an operation completed on an instance.
    /// </summary>
    /// <param name="instance">Instance that completed the operation.</param>
    /// <param name="success">Whether the operation was successful.</param>
    void NotifyOperationCompleted(T instance, bool success);
}