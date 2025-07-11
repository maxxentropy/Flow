using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Resilience;

/// <summary>
/// Implements the circuit breaker pattern for fault tolerance.
/// </summary>
public class CircuitBreaker : ICircuitBreaker
{
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly CircuitBreakerOptions _options;
    private readonly object _lock = new();
    
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private DateTime _openedTime;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreaker"/> class.
    /// </summary>
    public CircuitBreaker(ILogger<CircuitBreaker> logger, CircuitBreakerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
    
    /// <inheritdoc/>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open && 
                    DateTime.UtcNow - _openedTime >= _options.OpenDuration)
                {
                    _state = CircuitState.HalfOpen;
                    _logger.LogInformation("Circuit breaker transitioned to HalfOpen");
                }
                return _state;
            }
        }
    }
    
    /// <inheritdoc/>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (State == CircuitState.Open)
        {
            throw new CircuitBreakerOpenException("Circuit breaker is open");
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await operation().ConfigureAwait(false);
            
            OnSuccess();
            
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("Operation completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }
    
    /// <inheritdoc/>
    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async () =>
        {
            await operation().ConfigureAwait(false);
            return 0; // Dummy return value
        }, cancellationToken).ConfigureAwait(false);
    }
    
    /// <inheritdoc/>
    public void Reset()
    {
        lock (_lock)
        {
            _state = CircuitState.Closed;
            _failureCount = 0;
            _logger.LogInformation("Circuit breaker reset to Closed state");
        }
    }
    
    private void OnSuccess()
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
                _logger.LogInformation("Circuit breaker transitioned to Closed after successful operation");
            }
            else if (_state == CircuitState.Closed)
            {
                // Reset failure count on success in closed state
                if (_failureCount > 0 && DateTime.UtcNow - _lastFailureTime > _options.FailureCountWindow)
                {
                    _failureCount = 0;
                }
            }
        }
    }
    
    private void OnFailure(Exception exception)
    {
        lock (_lock)
        {
            _lastFailureTime = DateTime.UtcNow;
            _failureCount++;
            
            _logger.LogWarning(exception, 
                "Operation failed. Failure count: {FailureCount}/{Threshold}", 
                _failureCount, _options.FailureThreshold);
            
            if (_state == CircuitState.HalfOpen)
            {
                Open();
            }
            else if (_state == CircuitState.Closed && _failureCount >= _options.FailureThreshold)
            {
                Open();
            }
        }
    }
    
    private void Open()
    {
        _state = CircuitState.Open;
        _openedTime = DateTime.UtcNow;
        _logger.LogWarning("Circuit breaker opened due to excessive failures");
    }
}

/// <summary>
/// Circuit breaker interface.
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    CircuitState State { get; }
    
    /// <summary>
    /// Executes an operation with circuit breaker protection.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an operation with circuit breaker protection.
    /// </summary>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resets the circuit breaker to closed state.
    /// </summary>
    void Reset();
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed and operations are allowed.
    /// </summary>
    Closed,
    
    /// <summary>
    /// Circuit is open and operations are blocked.
    /// </summary>
    Open,
    
    /// <summary>
    /// Circuit is half-open and testing if operations succeed.
    /// </summary>
    HalfOpen
}

/// <summary>
/// Circuit breaker configuration options.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets the number of failures before opening the circuit.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;
    
    /// <summary>
    /// Gets or sets the time window for counting failures.
    /// </summary>
    public TimeSpan FailureCountWindow { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// Gets or sets the duration to keep the circuit open.
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Exception thrown when circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
    /// </summary>
    public CircuitBreakerOpenException(string message) : base(message)
    {
    }
}