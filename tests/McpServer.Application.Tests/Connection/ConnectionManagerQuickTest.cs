using FluentAssertions;
using McpServer.Application.Connection;
using McpServer.Domain.Connection;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Connection;

/// <summary>
/// Quick test to verify ConnectionManager functionality
/// </summary>
public class ConnectionManagerQuickTest
{
    [Fact]
    public async Task ConnectionManager_BasicFunctionality_Works()
    {
        // Arrange
        var logger = new Mock<ILogger<ConnectionManager>>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        
        var options = new ConnectionManagerOptions
        {
            MaxConnections = 5,
            EnableMultiplexing = true,
            IdleTimeout = TimeSpan.FromMinutes(30),
            AutoCleanupIdleConnections = false
        };
        
        using var connectionManager = new ConnectionManager(logger.Object, loggerFactory.Object, Options.Create(options));
        
        var transport1 = new Mock<ITransport>();
        transport1.Setup(x => x.IsConnected).Returns(true);
        
        var transport2 = new Mock<ITransport>();
        transport2.Setup(x => x.IsConnected).Returns(true);
        
        // Act
        var conn1 = await connectionManager.AcceptConnectionAsync(transport1.Object, "test-conn-1");
        var conn2 = await connectionManager.AcceptConnectionAsync(transport2.Object, "test-conn-2");
        
        // Assert
        connectionManager.ActiveConnectionCount.Should().Be(2);
        connectionManager.GetConnection("test-conn-1").Should().NotBeNull();
        connectionManager.GetConnection("test-conn-2").Should().NotBeNull();
        
        // Test closing
        await connectionManager.CloseConnectionAsync("test-conn-1", "Test close");
        connectionManager.ActiveConnectionCount.Should().Be(1);
        connectionManager.GetConnection("test-conn-1").Should().BeNull();
        connectionManager.GetConnection("test-conn-2").Should().NotBeNull();
    }
}