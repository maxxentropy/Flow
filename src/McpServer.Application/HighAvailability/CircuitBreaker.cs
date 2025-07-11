using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Application.HighAvailability;

/// <summary>
/// Thread-safe circuit breaker implementation.
/// </summary>
public class CircuitBreaker : ICircuitBreaker
{
    private readonly string _name;
    private readonly CircuitBreakerOptions _options;
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly object _stateLock = new();
    
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount = 0;
    private DateTimeOffset? _lastFailureTime;
    private DateTimeOffset _lastStateChange = DateTimeOffset.UtcNow;
    private long _totalOperations = 0;
    private long _successfulOperations = 0;
    private long _failedOperations = 0;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreaker"/> class.
    /// </summary>
    public CircuitBreaker(
        string name,
        CircuitBreakerOptions options,
        ILogger<CircuitBreaker> logger)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc/>
    public CircuitBreakerState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }
    
    /// <inheritdoc/>
    public int FailureCount
    {
        get
        {
            lock (_stateLock)
            {
                return _failureCount;
            }
        }
    }
    
    /// <inheritdoc/>
    public DateTimeOffset? LastFailureTime
    {
        get
        {
            lock (_stateLock)
            {
                return _lastFailureTime;
            }
        }
    }
    
    /// <inheritdoc/>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        
        // Check if we can execute
        EnsureCanExecute();
        
        Interlocked.Increment(ref _totalOperations);
        
        try
        {
            var result = await operation();
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(ex);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        
        // Check if we can execute
        EnsureCanExecute();
        
        Interlocked.Increment(ref _totalOperations);
        
        try
        {
            await operation();
            OnSuccess();
        }
        catch (Exception ex)
        {
            OnFailure(ex);
            throw;
        }
    }
    
    /// <inheritdoc/>
    public void Trip()
    {
        lock (_stateLock)
        {
            if (_state != CircuitBreakerState.Open)
            {
                _state = CircuitBreakerState.Open;
                _lastStateChange = DateTimeOffset.UtcNow;
                _logger.LogWarning("Circuit breaker '{Name}' manually tripped to Open state", _name);
            }
        }
    }
    
    /// <inheritdoc/>
    public void Reset()
    {
        lock (_stateLock)
        {
            _state = CircuitBreakerState.Closed;
            _failureCount = 0;
            _lastFailureTime = null;
            _lastStateChange = DateTimeOffset.UtcNow;
            _logger.LogInformation("Circuit breaker '{Name}' manually reset to Closed state", _name);
        }
    }
    
    /// <inheritdoc/>
    public CircuitBreakerStatistics GetStatistics()
    {
        lock (_stateLock)
        {
            return new CircuitBreakerStatistics
            {
                State = _state,
                TotalOperations = _totalOperations,
                SuccessfulOperations = _successfulOperations,
                FailedOperations = _failedOperations,
                CurrentFailureCount = _failureCount,
                LastFailureTime = _lastFailureTime,
                LastStateChange = _lastStateChange
            };
        }
    }
    
    private void EnsureCanExecute()
    {
        lock (_stateLock)
        {
            switch (_state)
            {
                case CircuitBreakerState.Closed:
                    return;
                    
                case CircuitBreakerState.Open:
                    var timeSinceFailure = DateTimeOffset.UtcNow - _lastStateChange;
                    if (timeSinceFailure >= _options.OpenTimeout)
                    {
                        _state = CircuitBreakerState.HalfOpen;
                        _logger.LogInformation("Circuit breaker '{Name}' transitioned to HalfOpen state", _name);
                        return;
                    }
                    
                    var retryAfter = _options.OpenTimeout - timeSinceFailure;
                    throw new CircuitBreakerOpenException(_name, retryAfter);
                    
                case CircuitBreakerState.HalfOpen:
                    return; // Allow one test operation
                    
                default:
                    throw new InvalidOperationException($"Unknown circuit breaker state: {_state}");
            }
        }
    }
    
    private void OnSuccess()
    {
        Interlocked.Increment(ref _successfulOperations);
        
        lock (_stateLock)
        {
            if (_state == CircuitBreakerState.HalfOpen)
            {
                _state = CircuitBreakerState.Closed;
                _failureCount = 0;
                _lastFailureTime = null;
                _lastStateChange = DateTimeOffset.UtcNow;
                _logger.LogInformation("Circuit breaker '{Name}' reset to Closed state after successful operation", _name);
            }
            else if (_state == CircuitBreakerState.Closed)
            {
                // Reset failure count on successful operation in closed state
                _failureCount = 0;
            }
        }
    }
    
    private void OnFailure(Exception exception)
    {
        Interlocked.Increment(ref _failedOperations);
        
        // Check if this exception should be counted as a failure
        if (!ShouldCountAsFailure(exception))
        {
            return;
        }
        
        lock (_stateLock)
        {
            _failureCount++;
            _lastFailureTime = DateTimeOffset.UtcNow;
            
            _logger.LogWarning(exception, "Circuit breaker '{Name}' recorded failure {FailureCount}/{Threshold}", 
                _name, _failureCount, _options.FailureThreshold);
            
            if (_state == CircuitBreakerState.HalfOpen)
            {
                // Any failure in half-open state trips the breaker
                _state = CircuitBreakerState.Open;
                _lastStateChange = DateTimeOffset.UtcNow;
                _logger.LogWarning("Circuit breaker '{Name}' opened due to failure in HalfOpen state", _name);
            }
            else if (_state == CircuitBreakerState.Closed && _failureCount >= _options.FailureThreshold)
            {
                _state = CircuitBreakerState.Open;
                _lastStateChange = DateTimeOffset.UtcNow;
                _logger.LogError("Circuit breaker '{Name}' opened due to {FailureCount} consecutive failures", 
                    _name, _failureCount);
            }
        }
    }
    
    private bool ShouldCountAsFailure(Exception exception)
    {
        // Don't count certain exceptions as failures
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
/// Factory implementation for creating circuit breakers.
/// </summary>
public class CircuitBreakerFactory : ICircuitBreakerFactory
{
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly CircuitBreakerOptions _defaultOptions;
    private readonly ConcurrentDictionary<string, ICircuitBreaker> _circuitBreakers = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerFactory"/> class.
    /// </summary>
    public CircuitBreakerFactory(
        ILogger<CircuitBreaker> logger,
        IOptions<CircuitBreakerOptions> defaultOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultOptions = defaultOptions?.Value ?? new CircuitBreakerOptions();
    }
    
    /// <inheritdoc/>
    public ICircuitBreaker Create(string name, CircuitBreakerOptions? options = null)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Circuit breaker name cannot be null or empty", nameof(name));
        
        var effectiveOptions = options ?? _defaultOptions;
        
        return _circuitBreakers.GetOrAdd(name, 
            _ => new CircuitBreaker(name, effectiveOptions, _logger));
    }
    
    /// <inheritdoc/>
    public ICircuitBreaker? Get(string name)
    {
        return _circuitBreakers.TryGetValue(name, out var circuitBreaker) ? circuitBreaker : null;
    }
    
    /// <inheritdoc/>
    public Dictionary<string, CircuitBreakerStatistics> GetAllStatistics()
    {
        return _circuitBreakers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetStatistics());
    }
}

/// <summary>
/// Configuration options for circuit breaker.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets the number of consecutive failures required to open the circuit.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;
    
    /// <summary>
    /// Gets or sets the timeout before attempting to close an open circuit.
    /// </summary>
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Gets or sets the timeout for operations executed through the circuit breaker.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);
}