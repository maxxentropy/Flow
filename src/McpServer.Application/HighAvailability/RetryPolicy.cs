using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Application.HighAvailability;

/// <summary>
/// Configurable retry policy implementation.
/// </summary>
public class RetryPolicy : IRetryPolicy
{
    private readonly RetryPolicyOptions _options;
    private readonly ILogger<RetryPolicy> _logger;
    private readonly Random _random = new();
    
    private long _totalOperations = 0;
    private long _successfulOperations = 0;
    private long _failedOperations = 0;
    private long _totalRetryAttempts = 0;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicy"/> class.
    /// </summary>
    public RetryPolicy(
        RetryPolicyOptions options,
        ILogger<RetryPolicy> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc/>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        
        Interlocked.Increment(ref _totalOperations);
        
        var attempt = 0;
        Exception? lastException = null;
        
        while (attempt <= _options.MaxRetryAttempts)
        {
            try
            {
                var result = await operation();
                
                if (attempt > 0)
                {
                    _logger.LogInformation("Operation succeeded after {AttemptCount} attempts", attempt + 1);
                }
                
                Interlocked.Increment(ref _successfulOperations);
                Interlocked.Add(ref _totalRetryAttempts, attempt);
                
                return result;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt))
            {
                lastException = ex;
                attempt++;
                
                if (attempt <= _options.MaxRetryAttempts)
                {
                    var delay = CalculateDelay(attempt);
                    _logger.LogWarning(ex, "Operation failed on attempt {AttemptCount}, retrying in {Delay}ms", 
                        attempt, delay.TotalMilliseconds);
                    
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
        
        Interlocked.Increment(ref _failedOperations);
        Interlocked.Add(ref _totalRetryAttempts, _options.MaxRetryAttempts);
        
        _logger.LogError(lastException, "Operation failed after {MaxAttempts} attempts", _options.MaxRetryAttempts + 1);
        
        throw lastException ?? new InvalidOperationException("Operation failed without exception");
    }
    
    /// <inheritdoc/>
    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true; // Dummy return value
        }, cancellationToken);
    }
    
    /// <inheritdoc/>
    public RetryStatistics GetStatistics()
    {
        return new RetryStatistics
        {
            TotalOperations = Interlocked.Read(ref _totalOperations),
            SuccessfulOperations = Interlocked.Read(ref _successfulOperations),
            FailedOperations = Interlocked.Read(ref _failedOperations),
            TotalRetryAttempts = Interlocked.Read(ref _totalRetryAttempts)
        };
    }
    
    private bool ShouldRetry(Exception exception, int attempt)
    {
        if (attempt >= _options.MaxRetryAttempts)
            return false;
        
        // Use custom predicate if provided
        if (_options.ShouldRetryPredicate != null)
            return _options.ShouldRetryPredicate(exception);
        
        // Default retry logic for common transient failures
        return exception switch
        {
            TaskCanceledException tce => !tce.CancellationToken.IsCancellationRequested, // Only retry if not user-cancelled
            OperationCanceledException => false,
            ArgumentNullException => false,
            ArgumentException => false,
            InvalidOperationException => false,
            TimeoutException => true,
            HttpRequestException => true,
            _ => true // Retry unknown exceptions by default
        };
    }
    
    private TimeSpan CalculateDelay(int attempt)
    {
        var delay = _options.Strategy switch
        {
            RetryStrategy.FixedInterval => _options.BaseDelay,
            RetryStrategy.LinearBackoff => TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * attempt),
            RetryStrategy.ExponentialBackoff => TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)),
            _ => _options.BaseDelay
        };
        
        // Apply jitter to prevent thundering herd
        if (_options.JitterFactor > 0)
        {
            var jitter = _random.NextDouble() * _options.JitterFactor * delay.TotalMilliseconds;
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds + jitter);
        }
        
        // Ensure delay doesn't exceed maximum
        return delay > _options.MaxDelay ? _options.MaxDelay : delay;
    }
}

/// <summary>
/// Factory implementation for creating retry policies.
/// </summary>
public class RetryPolicyFactory : IRetryPolicyFactory
{
    private readonly ILogger<RetryPolicy> _logger;
    private readonly RetryPolicyOptions _defaultOptions;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicyFactory"/> class.
    /// </summary>
    public RetryPolicyFactory(
        ILogger<RetryPolicy> logger,
        IOptions<RetryPolicyOptions> defaultOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultOptions = defaultOptions?.Value ?? new RetryPolicyOptions();
    }
    
    /// <inheritdoc/>
    public IRetryPolicy Create(RetryPolicyOptions? options = null)
    {
        var effectiveOptions = options ?? _defaultOptions;
        return new RetryPolicy(effectiveOptions, _logger);
    }
    
    /// <inheritdoc/>
    public IRetryPolicy CreateForTransientFailures()
    {
        var options = new RetryPolicyOptions
        {
            MaxRetryAttempts = 3,
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(10),
            Strategy = RetryStrategy.ExponentialBackoff,
            JitterFactor = 0.2,
            ShouldRetryPredicate = IsTransientFailure
        };
        
        return new RetryPolicy(options, _logger);
    }
    
    /// <inheritdoc/>
    public IRetryPolicy CreateForNetworkOperations()
    {
        var options = new RetryPolicyOptions
        {
            MaxRetryAttempts = 5,
            BaseDelay = TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(30),
            Strategy = RetryStrategy.ExponentialBackoff,
            JitterFactor = 0.25,
            ShouldRetryPredicate = IsNetworkFailure
        };
        
        return new RetryPolicy(options, _logger);
    }
    
    private static bool IsTransientFailure(Exception exception)
    {
        return exception switch
        {
            TimeoutException => true,
            HttpRequestException => true,
            TaskCanceledException tce => !tce.CancellationToken.IsCancellationRequested,
            SocketException => true,
            IOException => true,
            _ => false
        };
    }
    
    private static bool IsNetworkFailure(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => true,
            SocketException => true,
            TimeoutException => true,
            TaskCanceledException tce => !tce.CancellationToken.IsCancellationRequested,
            IOException io => io.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                             io.Message.Contains("connection", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}