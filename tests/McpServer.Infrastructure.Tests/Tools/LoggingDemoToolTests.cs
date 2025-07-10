using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Tools;
using McpServer.Infrastructure.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ToolsTextContent = McpServer.Domain.Tools.TextContent;

namespace McpServer.Infrastructure.Tests.Tools;

public class LoggingDemoToolTests
{
    private readonly Mock<ILogger<LoggingDemoTool>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILoggingService> _loggingServiceMock;
    private readonly LoggingDemoTool _tool;
    
    public LoggingDemoToolTests()
    {
        _loggerMock = new Mock<ILogger<LoggingDemoTool>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggingServiceMock = new Mock<ILoggingService>();
        
        _serviceProviderMock.Setup(x => x.GetService(typeof(ILoggingService)))
            .Returns(_loggingServiceMock.Object);
        
        _tool = new LoggingDemoTool(_loggerMock.Object, _serviceProviderMock.Object);
    }
    
    [Fact]
    public void Properties_AreCorrectlySet()
    {
        // Assert
        _tool.Name.Should().Be("logging_demo");
        _tool.Description.Should().Contain("logging functionality");
        _tool.Schema.Should().NotBeNull();
        _tool.Schema.Properties.Should().ContainKey("level");
        _tool.Schema.Required.Should().Contain("level");
    }
    
    [Fact]
    public async Task ExecuteAsync_WithoutLoggingService_ReturnsError()
    {
        // Arrange
        _serviceProviderMock.Setup(x => x.GetService(typeof(ILoggingService)))
            .Returns((ILoggingService?)null);
        
        var request = new ToolRequest
        {
            Name = "logging_demo",
            Arguments = new Dictionary<string, object?> { ["level"] = "info" }
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<ToolsTextContent>();
        ((ToolsTextContent)result.Content[0]).Text.Should().Contain("Logging service is not available");
    }
    
    [Fact]
    public async Task ExecuteAsync_WithoutLevel_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "logging_demo",
            Arguments = new Dictionary<string, object?>()
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<ToolsTextContent>();
        ((ToolsTextContent)result.Content[0]).Text.Should().Contain("level");
    }
    
    [Fact]
    public async Task ExecuteAsync_WithValidLevel_LogsMessage()
    {
        // Arrange
        _loggingServiceMock.Setup(x => x.MinimumLogLevel).Returns(McpLogLevel.Info);
        
        var request = new ToolRequest
        {
            Name = "logging_demo",
            Arguments = new Dictionary<string, object?> 
            { 
                ["level"] = "warning",
                ["message"] = "Test warning message",
                ["logger"] = "test-logger"
            }
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<ToolsTextContent>();
        
        var textContent = (ToolsTextContent)result.Content[0];
        textContent.Text.Should().Contain("Successfully sent warning log message");
        textContent.Text.Should().Contain("test-logger");
        textContent.Text.Should().Contain("minimum log level is info");
        
        _loggingServiceMock.Verify(x => x.LogAsync(
            McpLogLevel.Warning,
            It.Is<Dictionary<string, object>>(d => 
                d.ContainsKey("message") && 
                d["message"].ToString()!.Contains("Test warning message")),
            "test-logger",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithSimulateError_IncludesErrorInLogData()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "logging_demo",
            Arguments = new Dictionary<string, object?> 
            { 
                ["level"] = "error",
                ["simulate_error"] = true
            }
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        
        _loggingServiceMock.Verify(x => x.LogAsync(
            McpLogLevel.Error,
            It.Is<Dictionary<string, object>>(d => d.ContainsKey("error")),
            "demo",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithInvalidLevel_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "logging_demo",
            Arguments = new Dictionary<string, object?> { ["level"] = "invalid" }
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<ToolsTextContent>();
        ((ToolsTextContent)result.Content[0]).Text.Should().Contain("Invalid log level 'invalid'");
    }
    
    [Theory]
    [InlineData("debug")]
    [InlineData("info")]
    [InlineData("notice")]
    [InlineData("warning")]
    [InlineData("error")]
    [InlineData("critical")]
    [InlineData("alert")]
    [InlineData("emergency")]
    public async Task ExecuteAsync_WithAllValidLevels_LogsCorrectly(string level)
    {
        // Arrange
        var expectedLogLevel = level.ToLogLevel();
        
        var request = new ToolRequest
        {
            Name = "logging_demo",
            Arguments = new Dictionary<string, object?> { ["level"] = level }
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        
        _loggingServiceMock.Verify(x => x.LogAsync(
            expectedLogLevel,
            It.IsAny<Dictionary<string, object>>(),
            "demo",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task ExecuteAsync_WhenLoggingServiceThrows_ReturnsError()
    {
        // Arrange
        _loggingServiceMock.Setup(x => x.LogAsync(
            It.IsAny<McpLogLevel>(),
            It.IsAny<object>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Logging failed"));
        
        var request = new ToolRequest
        {
            Name = "logging_demo",
            Arguments = new Dictionary<string, object?> { ["level"] = "info" }
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<ToolsTextContent>();
        ((ToolsTextContent)result.Content[0]).Text.Should().Contain("Error executing logging demo");
    }
}