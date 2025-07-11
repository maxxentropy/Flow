namespace McpServer.Application.HighAvailability;

/// <summary>
/// Circuit breaker pattern implementation for fault tolerance.
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    CircuitBreakerState State { get; }
    
    /// <summary>
    /// Gets the failure count in the current window.
    /// </summary>
    int FailureCount { get; }
    
    /// <summary>
    /// Gets the last failure time.
    /// </summary>
    DateTimeOffset? LastFailureTime { get; }
    
    /// <summary>
    /// Executes an operation through the circuit breaker.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the operation.</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an operation through the circuit breaker.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Manually trips the circuit breaker to the open state.
    /// </summary>
    void Trip();
    
    /// <summary>
    /// Manually resets the circuit breaker to the closed state.
    /// </summary>
    void Reset();
    
    /// <summary>
    /// Gets statistics about the circuit breaker.
    /// </summary>
    CircuitBreakerStatistics GetStatistics();
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed - operations flow normally.
    /// </summary>
    Closed,
    
    /// <summary>
    /// Circuit is open - operations are blocked.
    /// </summary>
    Open,
    
    /// <summary>
    /// Circuit is half-open - testing if service has recovered.
    /// </summary>
    HalfOpen
}

/// <summary>
/// Statistics for circuit breaker operations.
/// </summary>
public class CircuitBreakerStatistics
{
    /// <summary>
    /// Gets the current state.
    /// </summary>
    public CircuitBreakerState State { get; init; }
    
    /// <summary>
    /// Gets the total number of operations attempted.
    /// </summary>
    public long TotalOperations { get; init; }
    
    /// <summary>
    /// Gets the total number of successful operations.
    /// </summary>
    public long SuccessfulOperations { get; init; }
    
    /// <summary>
    /// Gets the total number of failed operations.
    /// </summary>
    public long FailedOperations { get; init; }
    
    /// <summary>
    /// Gets the current failure count in the window.
    /// </summary>
    public int CurrentFailureCount { get; init; }
    
    /// <summary>
    /// Gets the last failure time.
    /// </summary>
    public DateTimeOffset? LastFailureTime { get; init; }
    
    /// <summary>
    /// Gets the last state change time.
    /// </summary>
    public DateTimeOffset LastStateChange { get; init; }
    
    /// <summary>
    /// Gets the success rate percentage.
    /// </summary>
    public double SuccessRate => TotalOperations == 0 ? 0 : (double)SuccessfulOperations / TotalOperations * 100;
}

/// <summary>
/// Exception thrown when circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Gets the circuit breaker name.
    /// </summary>
    public string CircuitBreakerName { get; }
    
    /// <summary>
    /// Gets the time until the circuit can be tested again.
    /// </summary>
    public TimeSpan RetryAfter { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
    /// </summary>
    public CircuitBreakerOpenException(string circuitBreakerName, TimeSpan retryAfter)
        : base($"Circuit breaker '{circuitBreakerName}' is open. Retry after {retryAfter.TotalSeconds:F1} seconds.")
    {
        CircuitBreakerName = circuitBreakerName;
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Factory for creating circuit breakers.
/// </summary>
public interface ICircuitBreakerFactory
{
    /// <summary>
    /// Creates a circuit breaker with the specified name and options.
    /// </summary>
    /// <param name="name">Circuit breaker name.</param>
    /// <param name="options">Configuration options.</param>
    /// <returns>Circuit breaker instance.</returns>
    ICircuitBreaker Create(string name, CircuitBreakerOptions? options = null);
    
    /// <summary>
    /// Gets an existing circuit breaker by name.
    /// </summary>
    /// <param name="name">Circuit breaker name.</param>
    /// <returns>Circuit breaker instance or null if not found.</returns>
    ICircuitBreaker? Get(string name);
    
    /// <summary>
    /// Gets all circuit breaker statistics.
    /// </summary>
    /// <returns>Dictionary of circuit breaker names and their statistics.</returns>
    Dictionary<string, CircuitBreakerStatistics> GetAllStatistics();
}