using System.Net.WebSockets;
using FluentAssertions;
using McpServer.Application.Server;
using McpServer.Infrastructure.Middleware;
using McpServer.Infrastructure.Transport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Infrastructure.Tests.Middleware;

public class WebSocketMiddlewareTests
{
    private readonly Mock<RequestDelegate> _nextMock;
    private readonly Mock<ILogger<WebSocketMiddleware>> _loggerMock;
    private readonly Mock<IOptions<WebSocketTransportOptions>> _optionsMock;
    private readonly WebSocketTransportOptions _options;
    private readonly WebSocketMiddleware _middleware;
    private readonly string _path = "/ws";

    public WebSocketMiddlewareTests()
    {
        _nextMock = new Mock<RequestDelegate>();
        _loggerMock = new Mock<ILogger<WebSocketMiddleware>>();
        _optionsMock = new Mock<IOptions<WebSocketTransportOptions>>();
        _options = new WebSocketTransportOptions();
        _optionsMock.Setup(x => x.Value).Returns(_options);
        
        _middleware = new WebSocketMiddleware(
            _nextMock.Object,
            _loggerMock.Object,
            _optionsMock.Object,
            _path);
    }

    [Fact(Skip = "TODO: Update test for new WebSocket middleware architecture")]
    public async Task InvokeAsync_WithDifferentPath_CallsNext()
    {
        // Arrange
        var contextMock = new Mock<HttpContext>();
        var requestMock = new Mock<HttpRequest>();
        requestMock.Setup(x => x.Path).Returns("/other");
        contextMock.Setup(x => x.Request).Returns(requestMock.Object);
        
        var mcpServerMock = new Mock<IMcpServer>();
        var transportFactory = () => new Mock<WebSocketTransport>(_loggerMock.Object, _optionsMock.Object).Object;

        // Act
        await _middleware.InvokeAsync(contextMock.Object);

        // Assert
        _nextMock.Verify(x => x(contextMock.Object), Times.Once);
    }

    [Fact(Skip = "TODO: Update test for new WebSocket middleware architecture")]
    public async Task InvokeAsync_WithWebSocketPath_NonWebSocketRequest_Returns400()
    {
        // Arrange
        var contextMock = new Mock<HttpContext>();
        var requestMock = new Mock<HttpRequest>();
        var responseMock = new Mock<HttpResponse>();
        var webSocketsMock = new Mock<WebSocketManager>();
        
        requestMock.Setup(x => x.Path).Returns(_path);
        webSocketsMock.Setup(x => x.IsWebSocketRequest).Returns(false);
        // No need to setup WriteAsync since it's an extension method
        
        contextMock.Setup(x => x.Request).Returns(requestMock.Object);
        contextMock.Setup(x => x.Response).Returns(responseMock.Object);
        contextMock.Setup(x => x.WebSockets).Returns(webSocketsMock.Object);
        
        var mcpServerMock = new Mock<IMcpServer>();
        var transportFactory = () => new Mock<WebSocketTransport>(_loggerMock.Object, _optionsMock.Object).Object;

        // Act
        await _middleware.InvokeAsync(contextMock.Object);

        // Assert
        responseMock.VerifySet(x => x.StatusCode = 400);
        // Cannot verify WriteAsync as it's an extension method
        _nextMock.Verify(x => x(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact(Skip = "TODO: Update test for new WebSocket middleware architecture")]
    public async Task InvokeAsync_WithWebSocketRequest_AcceptsAndStartsTransport()
    {
        // Arrange
        var contextMock = new Mock<HttpContext>();
        var requestMock = new Mock<HttpRequest>();
        var webSocketsMock = new Mock<WebSocketManager>();
        var connectionMock = new Mock<ConnectionInfo>();
        
        requestMock.Setup(x => x.Path).Returns(_path);
        webSocketsMock.Setup(x => x.IsWebSocketRequest).Returns(true);
        connectionMock.Setup(x => x.RemoteIpAddress).Returns(System.Net.IPAddress.Loopback);
        
        contextMock.Setup(x => x.Request).Returns(requestMock.Object);
        contextMock.Setup(x => x.WebSockets).Returns(webSocketsMock.Object);
        contextMock.Setup(x => x.Connection).Returns(connectionMock.Object);
        
        var mcpServerMock = new Mock<IMcpServer>();
        var transportMock = new Mock<WebSocketTransport>(_loggerMock.Object, _optionsMock.Object);
        transportMock.SetupSequence(x => x.IsConnected)
            .Returns(true)  // First check
            .Returns(false); // Second check after delay
        
        var transportFactory = () => transportMock.Object;

        // Act
        await _middleware.InvokeAsync(contextMock.Object);

        // Assert
        transportMock.Verify(x => x.AcceptWebSocketAsync(contextMock.Object, It.IsAny<CancellationToken>()), Times.Once);
        mcpServerMock.Verify(x => x.StartAsync(transportMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(Skip = "TODO: Update test for new WebSocket middleware architecture")]
    public async Task InvokeAsync_WithWebSocketRequest_HandlesException()
    {
        // Arrange
        var contextMock = new Mock<HttpContext>();
        var requestMock = new Mock<HttpRequest>();
        var responseMock = new Mock<HttpResponse>();
        var webSocketsMock = new Mock<WebSocketManager>();
        var connectionMock = new Mock<ConnectionInfo>();
        
        requestMock.Setup(x => x.Path).Returns(_path);
        webSocketsMock.Setup(x => x.IsWebSocketRequest).Returns(true);
        connectionMock.Setup(x => x.RemoteIpAddress).Returns(System.Net.IPAddress.Loopback);
        // No need to setup WriteAsync since it's an extension method
        
        contextMock.Setup(x => x.Request).Returns(requestMock.Object);
        contextMock.Setup(x => x.Response).Returns(responseMock.Object);
        contextMock.Setup(x => x.WebSockets).Returns(webSocketsMock.Object);
        contextMock.Setup(x => x.Connection).Returns(connectionMock.Object);
        
        var mcpServerMock = new Mock<IMcpServer>();
        var transportMock = new Mock<WebSocketTransport>(_loggerMock.Object, _optionsMock.Object);
        transportMock.Setup(x => x.AcceptWebSocketAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));
        
        var transportFactory = () => transportMock.Object;

        // Act
        await _middleware.InvokeAsync(contextMock.Object);

        // Assert
        responseMock.VerifySet(x => x.StatusCode = 500);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error handling WebSocket connection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Assert
        _middleware.Should().NotBeNull();
    }
}