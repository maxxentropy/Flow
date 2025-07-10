using FluentAssertions;
using McpServer.Application.Handlers;
using McpServer.Application.Messages;
using McpServer.Application.Services;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Handlers;

public class LoggingHandlerTests
{
    private readonly Mock<ILogger<LoggingHandler>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILoggingService> _loggingServiceMock;
    private readonly LoggingHandler _handler;
    
    public LoggingHandlerTests()
    {
        _loggerMock = new Mock<ILogger<LoggingHandler>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggingServiceMock = new Mock<ILoggingService>();
        
        _serviceProviderMock.Setup(x => x.GetService(typeof(ILoggingService)))
            .Returns(_loggingServiceMock.Object);
        
        _handler = new LoggingHandler(_loggerMock.Object, _serviceProviderMock.Object);
    }
    
    [Fact]
    public void CanHandle_WithLoggingSetLevelRequest_ReturnsTrue()
    {
        // Act
        var result = _handler.CanHandle(typeof(LoggingSetLevelRequest));
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void CanHandle_WithOtherTypes_ReturnsFalse()
    {
        // Act
        var result = _handler.CanHandle(typeof(string));
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task HandleMessageAsync_WithValidSetLevelRequest_SetsLogLevel()
    {
        // Arrange
        var request = new JsonRpcRequest<LoggingSetLevelRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "logging/setLevel",
            Params = new LoggingSetLevelRequest { Level = "warning" }
        };
        
        // Act
        var result = await _handler.HandleMessageAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        _loggingServiceMock.Verify(x => x.SetLogLevel("warning"), Times.Once);
    }
    
    [Fact]
    public async Task HandleMessageAsync_WithInvalidLevel_ThrowsProtocolException()
    {
        // Arrange
        _loggingServiceMock.Setup(x => x.SetLogLevel("invalid"))
            .Throws(new ArgumentException("Invalid log level"));
        
        var request = new JsonRpcRequest<LoggingSetLevelRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "logging/setLevel",
            Params = new LoggingSetLevelRequest { Level = "invalid" }
        };
        
        // Act & Assert
        var act = () => _handler.HandleMessageAsync(request);
        await act.Should().ThrowAsync<ProtocolException>()
            .WithMessage("Invalid log level: invalid*");
    }
    
    [Fact]
    public async Task HandleMessageAsync_WithNullParams_ThrowsProtocolException()
    {
        // Arrange
        var request = new JsonRpcRequest<LoggingSetLevelRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "logging/setLevel",
            Params = null
        };
        
        // Act & Assert
        var act = () => _handler.HandleMessageAsync(request);
        await act.Should().ThrowAsync<ProtocolException>()
            .WithMessage("Logging setLevel request parameters cannot be null");
    }
    
    [Fact]
    public async Task HandleMessageAsync_WithInvalidMessageType_ThrowsArgumentException()
    {
        // Arrange
        var invalidMessage = "invalid message";
        
        // Act & Assert
        var act = () => _handler.HandleMessageAsync(invalidMessage);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid message type*");
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
    public async Task HandleMessageAsync_WithAllValidLevels_SetsLogLevel(string level)
    {
        // Arrange
        var request = new JsonRpcRequest<LoggingSetLevelRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "logging/setLevel",
            Params = new LoggingSetLevelRequest { Level = level }
        };
        
        // Act
        var result = await _handler.HandleMessageAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        _loggingServiceMock.Verify(x => x.SetLogLevel(level), Times.Once);
    }
}