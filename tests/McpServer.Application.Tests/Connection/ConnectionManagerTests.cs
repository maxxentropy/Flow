using FluentAssertions;
using McpServer.Application.Connection;
using McpServer.Domain.Connection;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Connection;

public class ConnectionManagerTests : IDisposable
{
    private readonly Mock<ILogger<ConnectionManager>> _logger;
    private readonly Mock<ILoggerFactory> _loggerFactory;
    private readonly ConnectionManager _connectionManager;
    private readonly ConnectionManagerOptions _options;

    public ConnectionManagerTests()
    {
        _logger = new Mock<ILogger<ConnectionManager>>();
        _loggerFactory = new Mock<ILoggerFactory>();
        _loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        
        _options = new ConnectionManagerOptions
        {
            MaxConnections = 5,
            EnableMultiplexing = true,
            IdleTimeout = TimeSpan.FromMinutes(30),
            AutoCleanupIdleConnections = false // Disable for tests
        };
        
        _connectionManager = new ConnectionManager(_logger.Object, _loggerFactory.Object, Options.Create(_options));
    }

    [Fact]
    public async Task AcceptConnectionAsync_Should_CreateNewConnection()
    {
        // Arrange
        var transport = new Mock<ITransport>();
        transport.Setup(x => x.IsConnected).Returns(true);

        // Act
        var connection = await _connectionManager.AcceptConnectionAsync(transport.Object);

        // Assert
        connection.Should().NotBeNull();
        connection.ConnectionId.Should().StartWith("conn_");
        connection.Transport.Should().BeSameAs(transport.Object);
        connection.State.Should().Be(ConnectionState.Connected);
        connection.IsInitialized.Should().BeFalse();
        _connectionManager.ActiveConnectionCount.Should().Be(1);
    }

    [Fact]
    public async Task AcceptConnectionAsync_WithCustomId_Should_UseProvidedId()
    {
        // Arrange
        var transport = new Mock<ITransport>();
        var customId = "custom-connection-123";

        // Act
        var connection = await _connectionManager.AcceptConnectionAsync(transport.Object, customId);

        // Assert
        connection.ConnectionId.Should().Be(customId);
    }

    [Fact]
    public async Task AcceptConnectionAsync_Should_RaiseConnectionEstablishedEvent()
    {
        // Arrange
        var transport = new Mock<ITransport>();
        ConnectionEventArgs? eventArgs = null;
        _connectionManager.ConnectionEstablished += (sender, args) => eventArgs = args;

        // Act
        var connection = await _connectionManager.AcceptConnectionAsync(transport.Object);

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.Connection.Should().BeSameAs(connection);
        eventArgs.Reason.Should().BeNull();
    }

    [Fact]
    public async Task AcceptConnectionAsync_WhenMaxConnectionsReached_Should_ThrowException()
    {
        // Arrange
        var transports = new List<Mock<ITransport>>();
        for (int i = 0; i < _options.MaxConnections; i++)
        {
            var transport = new Mock<ITransport>();
            transports.Add(transport);
            await _connectionManager.AcceptConnectionAsync(transport.Object);
        }

        var extraTransport = new Mock<ITransport>();

        // Act & Assert
        var act = () => _connectionManager.AcceptConnectionAsync(extraTransport.Object);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Maximum number of connections ({_options.MaxConnections}) reached");
    }

    [Fact]
    public async Task AcceptConnectionAsync_WhenMultiplexingDisabled_Should_ThrowOnSecondConnection()
    {
        // Arrange
        var options = new ConnectionManagerOptions { EnableMultiplexing = false };
        var connectionManager = new ConnectionManager(_logger.Object, _loggerFactory.Object, Options.Create(options));
        
        var transport1 = new Mock<ITransport>();
        var transport2 = new Mock<ITransport>();
        
        await connectionManager.AcceptConnectionAsync(transport1.Object);

        // Act & Assert
        var act = () => connectionManager.AcceptConnectionAsync(transport2.Object);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Connection multiplexing is disabled and a connection already exists");
    }

    [Fact]
    public async Task GetConnection_Should_ReturnExistingConnection()
    {
        // Arrange
        var transport = new Mock<ITransport>();
        var connection = await _connectionManager.AcceptConnectionAsync(transport.Object);

        // Act
        var retrieved = _connectionManager.GetConnection(connection.ConnectionId);

        // Assert
        retrieved.Should().BeSameAs(connection);
    }

    [Fact]
    public void GetConnection_WithInvalidId_Should_ReturnNull()
    {
        // Act
        var result = _connectionManager.GetConnection("invalid-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CloseConnectionAsync_Should_RemoveConnection()
    {
        // Arrange
        var transport = new Mock<ITransport>();
        var connection = await _connectionManager.AcceptConnectionAsync(transport.Object);

        // Act
        await _connectionManager.CloseConnectionAsync(connection.ConnectionId, "Test close");

        // Assert
        _connectionManager.ActiveConnectionCount.Should().Be(0);
        _connectionManager.GetConnection(connection.ConnectionId).Should().BeNull();
        transport.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseConnectionAsync_Should_RaiseConnectionClosedEvent()
    {
        // Arrange
        var transport = new Mock<ITransport>();
        var connection = await _connectionManager.AcceptConnectionAsync(transport.Object);
        ConnectionEventArgs? eventArgs = null;
        _connectionManager.ConnectionClosed += (sender, args) => eventArgs = args;

        // Act
        await _connectionManager.CloseConnectionAsync(connection.ConnectionId, "Test reason");

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.Connection.ConnectionId.Should().Be(connection.ConnectionId);
        eventArgs.Reason.Should().Be("Test reason");
    }

    [Fact]
    public async Task CloseAllConnectionsAsync_Should_CloseAllConnections()
    {
        // Arrange
        var transports = new List<Mock<ITransport>>();
        for (int i = 0; i < 3; i++)
        {
            var transport = new Mock<ITransport>();
            transports.Add(transport);
            await _connectionManager.AcceptConnectionAsync(transport.Object);
        }

        // Act
        await _connectionManager.CloseAllConnectionsAsync("Shutdown");

        // Assert
        _connectionManager.ActiveConnectionCount.Should().Be(0);
        foreach (var transport in transports)
        {
            transport.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    // Note: BroadcastAsync tests removed as they require internal SetState method
    // These are tested in integration tests where we have more control

    [Fact]
    public async Task OnTransportDisconnected_Should_CloseConnection()
    {
        // Arrange
        var transport = new Mock<ITransport>();
        var connection = await _connectionManager.AcceptConnectionAsync(transport.Object);

        // Act
        transport.Raise(x => x.Disconnected += null, new DisconnectedEventArgs("Transport error"));

        // Give async handler time to execute
        await Task.Delay(100);

        // Assert
        _connectionManager.GetConnection(connection.ConnectionId).Should().BeNull();
        _connectionManager.ActiveConnectionCount.Should().Be(0);
    }

    public void Dispose()
    {
        _connectionManager?.Dispose();
        GC.SuppressFinalize(this);
    }
}