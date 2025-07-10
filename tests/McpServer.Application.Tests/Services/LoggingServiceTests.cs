using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.Protocol.Messages;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class LoggingServiceTests
{
    private readonly Mock<ILogger<LoggingService>> _loggerMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly LoggingService _loggingService;
    
    public LoggingServiceTests()
    {
        _loggerMock = new Mock<ILogger<LoggingService>>();
        _notificationServiceMock = new Mock<INotificationService>();
        _loggingService = new LoggingService(_loggerMock.Object, _notificationServiceMock.Object);
    }
    
    [Fact]
    public void MinimumLogLevel_DefaultsToInfo()
    {
        // Assert
        _loggingService.MinimumLogLevel.Should().Be(McpLogLevel.Info);
    }
    
    [Fact]
    public void SetLogLevel_WithValidLevel_UpdatesMinimumLevel()
    {
        // Act
        _loggingService.SetLogLevel(McpLogLevel.Warning);
        
        // Assert
        _loggingService.MinimumLogLevel.Should().Be(McpLogLevel.Warning);
    }
    
    [Fact]
    public void SetLogLevel_WithStringLevel_UpdatesMinimumLevel()
    {
        // Act
        _loggingService.SetLogLevel("error");
        
        // Assert
        _loggingService.MinimumLogLevel.Should().Be(McpLogLevel.Error);
    }
    
    [Fact]
    public void SetLogLevel_WithInvalidString_ThrowsException()
    {
        // Act & Assert
        var act = () => _loggingService.SetLogLevel("invalid");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid log level: invalid*");
    }
    
    [Fact]
    public async Task LogAsync_WithLevelBelowMinimum_DoesNotSendNotification()
    {
        // Arrange
        _loggingService.SetLogLevel(McpLogLevel.Warning);
        
        // Act
        await _loggingService.LogAsync(McpLogLevel.Info, "test message");
        
        // Assert
        _notificationServiceMock.Verify(x => x.SendNotificationAsync(
            It.IsAny<LogMessageNotification>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }
    
    [Fact]
    public async Task LogAsync_WithLevelAtOrAboveMinimum_SendsNotification()
    {
        // Arrange
        _loggingService.SetLogLevel(McpLogLevel.Warning);
        var testData = new { message = "test error", code = 500 };
        
        // Act
        await _loggingService.LogAsync(McpLogLevel.Error, testData, "test-logger");
        
        // Assert
        _notificationServiceMock.Verify(x => x.SendNotificationAsync(
            It.Is<LogMessageNotification>(n => 
                n.LogParams.Level == "error" &&
                n.LogParams.Logger == "test-logger" &&
                n.LogParams.Data != null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task LogDebugAsync_CallsLogAsyncWithDebugLevel()
    {
        // Arrange
        _loggingService.SetLogLevel(McpLogLevel.Debug);
        var testData = "debug message";
        
        // Act
        await _loggingService.LogDebugAsync(testData, "debug-logger");
        
        // Assert
        _notificationServiceMock.Verify(x => x.SendNotificationAsync(
            It.Is<LogMessageNotification>(n => 
                n.LogParams.Level == "debug" &&
                n.LogParams.Logger == "debug-logger"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task LogInfoAsync_CallsLogAsyncWithInfoLevel()
    {
        // Arrange
        var testData = "info message";
        
        // Act
        await _loggingService.LogInfoAsync(testData);
        
        // Assert
        _notificationServiceMock.Verify(x => x.SendNotificationAsync(
            It.Is<LogMessageNotification>(n => n.LogParams.Level == "info"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task LogWarningAsync_CallsLogAsyncWithWarningLevel()
    {
        // Arrange
        var testData = "warning message";
        
        // Act
        await _loggingService.LogWarningAsync(testData);
        
        // Assert
        _notificationServiceMock.Verify(x => x.SendNotificationAsync(
            It.Is<LogMessageNotification>(n => n.LogParams.Level == "warning"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task LogErrorAsync_CallsLogAsyncWithErrorLevel()
    {
        // Arrange
        var testData = "error message";
        
        // Act
        await _loggingService.LogErrorAsync(testData);
        
        // Assert
        _notificationServiceMock.Verify(x => x.SendNotificationAsync(
            It.Is<LogMessageNotification>(n => n.LogParams.Level == "error"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task LogCriticalAsync_CallsLogAsyncWithCriticalLevel()
    {
        // Arrange
        var testData = "critical message";
        
        // Act
        await _loggingService.LogCriticalAsync(testData);
        
        // Assert
        _notificationServiceMock.Verify(x => x.SendNotificationAsync(
            It.Is<LogMessageNotification>(n => n.LogParams.Level == "critical"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task LogAsync_SanitizesSensitiveData()
    {
        // Arrange
        var sensitiveData = new Dictionary<string, object>
        {
            ["message"] = "Login attempt",
            ["password"] = "secret123",
            ["api_key"] = "abc123def456"
        };
        
        // Act
        await _loggingService.LogAsync(McpLogLevel.Info, sensitiveData);
        
        // Assert
        _notificationServiceMock.Verify(x => x.SendNotificationAsync(
            It.Is<LogMessageNotification>(n => 
                ((Dictionary<string, object>)n.LogParams.Data)["password"].ToString() == "[REDACTED]" &&
                ((Dictionary<string, object>)n.LogParams.Data)["api_key"].ToString() == "[REDACTED]"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Theory]
    [InlineData("debug", McpLogLevel.Debug)]
    [InlineData("info", McpLogLevel.Info)]
    [InlineData("notice", McpLogLevel.Notice)]
    [InlineData("warning", McpLogLevel.Warning)]
    [InlineData("error", McpLogLevel.Error)]
    [InlineData("critical", McpLogLevel.Critical)]
    [InlineData("alert", McpLogLevel.Alert)]
    [InlineData("emergency", McpLogLevel.Emergency)]
    public void LogLevelExtensions_ToLogLevel_ConvertsCorrectly(string levelString, McpLogLevel expectedLevel)
    {
        // Act
        var result = levelString.ToLogLevel();
        
        // Assert
        result.Should().Be(expectedLevel);
    }
    
    [Theory]
    [InlineData(McpLogLevel.Debug, "debug")]
    [InlineData(McpLogLevel.Info, "info")]
    [InlineData(McpLogLevel.Notice, "notice")]
    [InlineData(McpLogLevel.Warning, "warning")]
    [InlineData(McpLogLevel.Error, "error")]
    [InlineData(McpLogLevel.Critical, "critical")]
    [InlineData(McpLogLevel.Alert, "alert")]
    [InlineData(McpLogLevel.Emergency, "emergency")]
    public void LogLevelExtensions_ToLogLevelString_ConvertsCorrectly(McpLogLevel level, string expectedString)
    {
        // Act
        var result = level.ToLogLevelString();
        
        // Assert
        result.Should().Be(expectedString);
    }
    
    [Theory]
    [InlineData(McpLogLevel.Debug, McpLogLevel.Info, false)]
    [InlineData(McpLogLevel.Info, McpLogLevel.Info, true)]
    [InlineData(McpLogLevel.Warning, McpLogLevel.Info, true)]
    [InlineData(McpLogLevel.Error, McpLogLevel.Warning, true)]
    [InlineData(McpLogLevel.Debug, McpLogLevel.Warning, false)]
    public void LogLevelExtensions_ShouldLog_WorksCorrectly(McpLogLevel messageLevel, McpLogLevel minimumLevel, bool shouldLog)
    {
        // Act
        var result = messageLevel.ShouldLog(minimumLevel);
        
        // Assert
        result.Should().Be(shouldLog);
    }
    
    [Fact]
    public async Task LogAsync_WhenNotificationServiceThrows_LogsErrorAndContinues()
    {
        // Arrange
        _notificationServiceMock.Setup(x => x.SendNotificationAsync(
            It.IsAny<LogMessageNotification>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification failed"));
        
        // Act
        var act = async () => await _loggingService.LogAsync(McpLogLevel.Info, "test");
        
        // Assert
        await act.Should().NotThrowAsync();
        
        _loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send log notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}