using FluentAssertions;
using McpServer.Application.Handlers;
using McpServer.Application.Messages;
using McpServer.Application.Server;
using McpServer.Application.Services;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Handlers;

public class InitializeHandlerTests
{
    private readonly Mock<ILogger<InitializeHandler>> _loggerMock;
    private readonly Mock<IMcpServer> _serverMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly IProtocolVersionNegotiator _versionNegotiator;
    private readonly InitializeHandler _handler;

    public InitializeHandlerTests()
    {
        _loggerMock = new Mock<ILogger<InitializeHandler>>();
        _serverMock = new Mock<IMcpServer>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceProviderMock.Setup(x => x.GetService(typeof(IMcpServer))).Returns(_serverMock.Object);
        
        // Create a real version negotiator with test configuration
        var versionConfig = new ProtocolVersionConfiguration
        {
            SupportedVersions = ["0.1.0", "0.2.0", "1.0.0"],
            CurrentVersion = "0.1.0",
            AllowBackwardCompatibility = true
        };
        _versionNegotiator = new ProtocolVersionNegotiator(
            new Mock<ILogger<ProtocolVersionNegotiator>>().Object,
            Options.Create(versionConfig));
        
        _handler = new InitializeHandler(_loggerMock.Object, _serviceProviderMock.Object, _versionNegotiator);
    }

    [Fact]
    public void CanHandle_Should_Return_True_For_InitializeRequest()
    {
        // Act
        var result = _handler.CanHandle(typeof(InitializeRequest));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_Should_Return_False_For_Other_Types()
    {
        // Act
        var result = _handler.CanHandle(typeof(string));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleMessageAsync_Should_Return_InitializeResponse()
    {
        // Arrange
        var request = new JsonRpcRequest<InitializeRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "initialize",
            Params = new InitializeRequest
            {
                ProtocolVersion = "0.1.0",
                ClientInfo = new ClientInfo { Name = "TestClient", Version = "1.0.0" },
                Capabilities = new ClientCapabilities()
            }
        };

        // Note: Connection-level initialization is handled by ConnectionAwareMessageRouter
        _serverMock.SetupGet(s => s.ServerInfo).Returns(new ServerInfo { Name = "TestServer", Version = "1.0.0" });
        _serverMock.SetupGet(s => s.Capabilities).Returns(new ServerCapabilities());

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().BeOfType<InitializeResponse>();
        var response = result as InitializeResponse;
        response!.ProtocolVersion.Should().Be("0.1.0");
        response.ServerInfo.Name.Should().Be("TestServer");
    }

    // Note: Duplicate initialization checks are now handled by ConnectionAwareMessageRouter
    // This test is no longer relevant as the handler doesn't check initialization state

    [Fact]
    public async Task HandleMessageAsync_Should_Throw_For_Unsupported_Protocol_Version()
    {
        // Arrange
        var request = new JsonRpcRequest<InitializeRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "initialize",
            Params = new InitializeRequest
            {
                ProtocolVersion = "2.0.0",
                ClientInfo = new ClientInfo { Name = "TestClient", Version = "1.0.0" },
                Capabilities = new ClientCapabilities()
            }
        };

        // Note: Connection-level initialization is handled by ConnectionAwareMessageRouter

        // Act & Assert
        var act = () => _handler.HandleMessageAsync(request);
        await act.Should().ThrowAsync<ProtocolException>()
            .WithMessage("*2.0.0*");
    }
}