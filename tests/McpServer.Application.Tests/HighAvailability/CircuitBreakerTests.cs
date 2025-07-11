using FluentAssertions;
using McpServer.Application.HighAvailability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.HighAvailability;

public class CircuitBreakerTests
{
    private readonly Mock<ILogger<CircuitBreaker>> _loggerMock;
    private readonly CircuitBreakerOptions _options;
    private readonly CircuitBreaker _circuitBreaker;
    
    public CircuitBreakerTests()
    {
        _loggerMock = new Mock<ILogger<CircuitBreaker>>();
        _options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            OpenTimeout = TimeSpan.FromSeconds(1),
            OperationTimeout = TimeSpan.FromSeconds(5)
        };
        
        _circuitBreaker = new CircuitBreaker("test", _options, _loggerMock.Object);
    }
    
    [Fact]
    public void State_InitiallyIsClosed()
    {
        // Act
        var state = _circuitBreaker.State;
        
        // Assert
        state.Should().Be(CircuitBreakerState.Closed);
    }
    
    [Fact]
    public void FailureCount_InitiallyIsZero()
    {
        // Act
        var failureCount = _circuitBreaker.FailureCount;
        
        // Assert
        failureCount.Should().Be(0);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";
        
        // Act
        var result = await _circuitBreaker.ExecuteAsync(() => Task.FromResult(expectedResult));
        
        // Assert
        result.Should().Be(expectedResult);
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Closed);
        _circuitBreaker.FailureCount.Should().Be(0);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithFailingOperation_IncrementsFailureCount()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _circuitBreaker.ExecuteAsync<string>(() => throw new InvalidOperationException("Test failure")));
        
        _circuitBreaker.FailureCount.Should().Be(1);
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Closed);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithConsecutiveFailures_OpensCircuit()
    {
        // Arrange
        var operation = () => Task.FromException<string>(new InvalidOperationException("Test failure"));
        
        // Act - Fail enough times to open the circuit
        for (int i = 0; i < _options.FailureThreshold; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _circuitBreaker.ExecuteAsync(operation));
        }
        
        // Assert
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Open);
        _circuitBreaker.FailureCount.Should().Be(_options.FailureThreshold);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithOpenCircuit_ThrowsCircuitBreakerOpenException()
    {
        // Arrange - Open the circuit by failing operations
        var failingOperation = () => Task.FromException<string>(new InvalidOperationException("Test failure"));
        
        for (int i = 0; i < _options.FailureThreshold; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _circuitBreaker.ExecuteAsync(failingOperation));
        }
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<CircuitBreakerOpenException>(() => 
            _circuitBreaker.ExecuteAsync(() => Task.FromResult("should not execute")));
        
        exception.CircuitBreakerName.Should().Be("test");
        exception.RetryAfter.Should().BePositive();
    }
    
    [Fact]
    public async Task ExecuteAsync_AfterOpenTimeout_TransitionsToHalfOpen()
    {
        // Arrange - Open the circuit
        var failingOperation = () => Task.FromException<string>(new InvalidOperationException("Test failure"));
        
        for (int i = 0; i < _options.FailureThreshold; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _circuitBreaker.ExecuteAsync(failingOperation));
        }
        
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Open);
        
        // Act - Wait for timeout and attempt operation
        await Task.Delay(_options.OpenTimeout + TimeSpan.FromMilliseconds(100));
        
        var result = await _circuitBreaker.ExecuteAsync(() => Task.FromResult("success"));
        
        // Assert
        result.Should().Be("success");
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Closed);
        _circuitBreaker.FailureCount.Should().Be(0);
    }
    
    [Fact]
    public async Task ExecuteAsync_HalfOpenWithFailure_ReturnsToOpen()
    {
        // Arrange - Open the circuit
        var failingOperation = () => Task.FromException<string>(new InvalidOperationException("Test failure"));
        
        for (int i = 0; i < _options.FailureThreshold; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _circuitBreaker.ExecuteAsync(failingOperation));
        }
        
        // Wait for timeout to allow transition to half-open
        await Task.Delay(_options.OpenTimeout + TimeSpan.FromMilliseconds(100));
        
        // Act - Fail in half-open state
        await Assert.ThrowsAsync<InvalidOperationException>(() => _circuitBreaker.ExecuteAsync(failingOperation));
        
        // Assert
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Open);
    }
    
    [Fact]
    public void Trip_ManuallyOpensCircuit()
    {
        // Act
        _circuitBreaker.Trip();
        
        // Assert
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Open);
    }
    
    [Fact]
    public void Reset_ManuallyClosesCircuit()
    {
        // Arrange
        _circuitBreaker.Trip();
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Open);
        
        // Act
        _circuitBreaker.Reset();
        
        // Assert
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Closed);
        _circuitBreaker.FailureCount.Should().Be(0);
    }
    
    [Fact]
    public void GetStatistics_ReturnsCorrectStatistics()
    {
        // Act
        var stats = _circuitBreaker.GetStatistics();
        
        // Assert
        stats.Should().NotBeNull();
        stats.State.Should().Be(CircuitBreakerState.Closed);
        stats.TotalOperations.Should().Be(0);
        stats.SuccessfulOperations.Should().Be(0);
        stats.FailedOperations.Should().Be(0);
        stats.CurrentFailureCount.Should().Be(0);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperationAfterFailures_ResetsFailureCount()
    {
        // Arrange - Fail once but not enough to open circuit
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _circuitBreaker.ExecuteAsync<string>(() => throw new InvalidOperationException("Test failure")));
        
        _circuitBreaker.FailureCount.Should().Be(1);
        
        // Act - Succeed
        await _circuitBreaker.ExecuteAsync(() => Task.FromResult("success"));
        
        // Assert
        _circuitBreaker.FailureCount.Should().Be(0);
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Closed);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithOperationCanceledException_DoesNotCountAsFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _circuitBreaker.ExecuteAsync<string>(() => Task.FromCanceled<string>(cts.Token)));
        
        _circuitBreaker.FailureCount.Should().Be(0);
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Closed);
    }
    
    [Fact]
    public async Task ExecuteAsync_VoidOperation_WorksCorrectly()
    {
        // Arrange
        bool operationExecuted = false;
        
        // Act
        await _circuitBreaker.ExecuteAsync(() =>
        {
            operationExecuted = true;
            return Task.CompletedTask;
        });
        
        // Assert
        operationExecuted.Should().BeTrue();
        _circuitBreaker.State.Should().Be(CircuitBreakerState.Closed);
    }
}