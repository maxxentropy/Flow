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

public class RootsHandlerTests
{
    private readonly Mock<ILogger<RootsHandler>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IRootRegistry> _rootRegistryMock;
    private readonly RootsHandler _handler;

    public RootsHandlerTests()
    {
        _loggerMock = new Mock<ILogger<RootsHandler>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _rootRegistryMock = new Mock<IRootRegistry>();
        
        _serviceProviderMock.Setup(x => x.GetService(typeof(IRootRegistry)))
            .Returns(_rootRegistryMock.Object);
        
        _handler = new RootsHandler(_loggerMock.Object, _serviceProviderMock.Object);
    }

    [Fact]
    public void CanHandle_WithRootsListRequest_ReturnsTrue()
    {
        // Act
        var result = _handler.CanHandle(typeof(RootsListRequest));

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
    public async Task HandleMessageAsync_WithValidRootsListRequest_ReturnsRootsList()
    {
        // Arrange
        var roots = new List<Root>
        {
            new Root { Uri = "file:///home/user/project1", Name = "Project 1" },
            new Root { Uri = "file:///home/user/project2", Name = "Project 2" }
        };

        _rootRegistryMock.Setup(x => x.Roots)
            .Returns(roots.AsReadOnly());

        var request = new JsonRpcRequest<RootsListRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "roots/list",
            Params = new RootsListRequest()
        };

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<RootsListResponse>();
        
        var response = (RootsListResponse)result!;
        response.Roots.Should().HaveCount(2);
        response.Roots.Should().BeEquivalentTo(roots);
    }

    [Fact]
    public async Task HandleMessageAsync_WithEmptyRootsList_ReturnsEmptyList()
    {
        // Arrange
        _rootRegistryMock.Setup(x => x.Roots)
            .Returns(new List<Root>().AsReadOnly());

        var request = new JsonRpcRequest<RootsListRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "roots/list",
            Params = new RootsListRequest()
        };

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<RootsListResponse>();
        
        var response = (RootsListResponse)result!;
        response.Roots.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleMessageAsync_WhenRootRegistryThrows_ThrowsProtocolException()
    {
        // Arrange
        _rootRegistryMock.Setup(x => x.Roots)
            .Throws(new InvalidOperationException("Registry error"));

        var request = new JsonRpcRequest<RootsListRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "roots/list",
            Params = new RootsListRequest()
        };

        // Act & Assert
        var act = () => _handler.HandleMessageAsync(request);
        await act.Should().ThrowAsync<ProtocolException>()
            .WithMessage("Failed to retrieve roots list");
    }

    [Fact]
    public async Task HandleMessageAsync_WhenRootRegistryNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        _serviceProviderMock.Setup(x => x.GetService(typeof(IRootRegistry)))
            .Returns((object?)null);

        var handler = new RootsHandler(_loggerMock.Object, _serviceProviderMock.Object);
        var request = new JsonRpcRequest<RootsListRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "roots/list",
            Params = new RootsListRequest()
        };

        // Act & Assert
        var act = () => handler.HandleMessageAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("IRootRegistry is not registered");
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

    [Fact]
    public async Task HandleMessageAsync_WithLargeRootsList_ReturnsAllRoots()
    {
        // Arrange
        var roots = new List<Root>();
        for (int i = 0; i < 1000; i++)
        {
            roots.Add(new Root { Uri = $"file:///project{i}", Name = $"Project {i}" });
        }

        _rootRegistryMock.Setup(x => x.Roots)
            .Returns(roots.AsReadOnly());

        var request = new JsonRpcRequest<RootsListRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "roots/list",
            Params = new RootsListRequest()
        };

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<RootsListResponse>();
        
        var response = (RootsListResponse)result!;
        response.Roots.Should().HaveCount(1000);
        response.Roots.Should().BeEquivalentTo(roots);
    }

    [Fact]
    public async Task HandleMessageAsync_WithRootsContainingSpecialCharacters_ReturnsCorrectRoots()
    {
        // Arrange
        var roots = new List<Root>
        {
            new Root { Uri = "file:///home/user/project with spaces", Name = "Project With Spaces" },
            new Root { Uri = "file:///home/user/项目", Name = "Chinese Project" },
            new Root { Uri = "file:///home/user/проект", Name = "Russian Project" },
            new Root { Uri = "https://api.example.com/v1", Name = "API Endpoint" }
        };

        _rootRegistryMock.Setup(x => x.Roots)
            .Returns(roots.AsReadOnly());

        var request = new JsonRpcRequest<RootsListRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "roots/list",
            Params = new RootsListRequest()
        };

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<RootsListResponse>();
        
        var response = (RootsListResponse)result!;
        response.Roots.Should().HaveCount(4);
        response.Roots.Should().BeEquivalentTo(roots);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public async Task HandleMessageAsync_WithVariousRootCounts_ReturnsCorrectCount(int rootCount)
    {
        // Arrange
        var roots = new List<Root>();
        for (int i = 0; i < rootCount; i++)
        {
            roots.Add(new Root { Uri = $"file:///project{i}", Name = $"Project {i}" });
        }

        _rootRegistryMock.Setup(x => x.Roots)
            .Returns(roots.AsReadOnly());

        var request = new JsonRpcRequest<RootsListRequest>
        {
            Jsonrpc = "2.0",
            Id = 1,
            Method = "roots/list",
            Params = new RootsListRequest()
        };

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<RootsListResponse>();
        
        var response = (RootsListResponse)result!;
        response.Roots.Should().HaveCount(rootCount);
    }
}