namespace McpServer.Application.HighAvailability;

/// <summary>
/// Retry policy for handling transient failures.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes an operation with retry logic.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the operation.</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an operation with retry logic.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the retry statistics.
    /// </summary>
    RetryStatistics GetStatistics();
}

/// <summary>
/// Retry strategy types.
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    /// Fixed interval between retries.
    /// </summary>
    FixedInterval,
    
    /// <summary>
    /// Exponential backoff with jitter.
    /// </summary>
    ExponentialBackoff,
    
    /// <summary>
    /// Linear backoff.
    /// </summary>
    LinearBackoff
}

/// <summary>
/// Retry policy options.
/// </summary>
public class RetryPolicyOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Gets or sets the base delay between retries.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Gets or sets the retry strategy.
    /// </summary>
    public RetryStrategy Strategy { get; set; } = RetryStrategy.ExponentialBackoff;
    
    /// <summary>
    /// Gets or sets the jitter factor for randomization (0.0 to 1.0).
    /// </summary>
    public double JitterFactor { get; set; } = 0.1;
    
    /// <summary>
    /// Gets or sets the predicate to determine if an exception should trigger a retry.
    /// </summary>
    public Func<Exception, bool>? ShouldRetryPredicate { get; set; }
}

/// <summary>
/// Statistics for retry operations.
/// </summary>
public class RetryStatistics
{
    /// <summary>
    /// Gets the total number of operations attempted.
    /// </summary>
    public long TotalOperations { get; init; }
    
    /// <summary>
    /// Gets the total number of successful operations (eventually).
    /// </summary>
    public long SuccessfulOperations { get; init; }
    
    /// <summary>
    /// Gets the total number of failed operations (after all retries).
    /// </summary>
    public long FailedOperations { get; init; }
    
    /// <summary>
    /// Gets the total number of retry attempts made.
    /// </summary>
    public long TotalRetryAttempts { get; init; }
    
    /// <summary>
    /// Gets the average number of attempts per operation.
    /// </summary>
    public double AverageAttemptsPerOperation => TotalOperations == 0 ? 0 : (double)TotalRetryAttempts / TotalOperations;
    
    /// <summary>
    /// Gets the success rate after retries.
    /// </summary>
    public double SuccessRate => TotalOperations == 0 ? 0 : (double)SuccessfulOperations / TotalOperations * 100;
}

/// <summary>
/// Factory for creating retry policies.
/// </summary>
public interface IRetryPolicyFactory
{
    /// <summary>
    /// Creates a retry policy with the specified options.
    /// </summary>
    /// <param name="options">Retry policy options.</param>
    /// <returns>Retry policy instance.</returns>
    IRetryPolicy Create(RetryPolicyOptions? options = null);
    
    /// <summary>
    /// Creates a retry policy for transient failures.
    /// </summary>
    /// <returns>Retry policy configured for common transient failures.</returns>
    IRetryPolicy CreateForTransientFailures();
    
    /// <summary>
    /// Creates a retry policy for network operations.
    /// </summary>
    /// <returns>Retry policy configured for network-related failures.</returns>
    IRetryPolicy CreateForNetworkOperations();
}