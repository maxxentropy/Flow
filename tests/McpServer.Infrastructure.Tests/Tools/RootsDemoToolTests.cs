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

public class RootsDemoToolTests
{
    private readonly Mock<ILogger<RootsDemoTool>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IRootRegistry> _rootRegistryMock;
    private readonly RootsDemoTool _tool;

    public RootsDemoToolTests()
    {
        _loggerMock = new Mock<ILogger<RootsDemoTool>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _rootRegistryMock = new Mock<IRootRegistry>();

        _serviceProviderMock.Setup(x => x.GetService(typeof(IRootRegistry)))
            .Returns(_rootRegistryMock.Object);

        _tool = new RootsDemoTool(_loggerMock.Object, _serviceProviderMock.Object);
    }

    [Fact]
    public void Name_ReturnsCorrectValue()
    {
        // Assert
        _tool.Name.Should().Be("roots_demo");
    }

    [Fact]
    public void Description_ReturnsCorrectValue()
    {
        // Assert
        _tool.Description.Should().Be("Demonstrates MCP roots functionality by showing current roots and testing URI access");
    }

    [Fact]
    public void Schema_HasCorrectStructure()
    {
        // Assert
        var schema = _tool.Schema;
        schema.Type.Should().Be("object");
        schema.Properties.Should().ContainKey("action");
        schema.Properties.Should().ContainKey("uri");
        schema.Properties.Should().ContainKey("name");
        schema.Required.Should().Contain("action");
    }

    [Fact]
    public async Task ExecuteAsync_WithListAction_ReturnsRootsList()
    {
        // Arrange
        var roots = new List<Root>
        {
            new Root { Uri = "file:///project1", Name = "Project 1" },
            new Root { Uri = "file:///project2", Name = "Project 2" }
        };

        _rootRegistryMock.Setup(x => x.Roots)
            .Returns(roots.AsReadOnly());

        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> { ["action"] = "list" }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("Project 1")
            .And.Contain("Project 2")
            .And.Contain("2 total");
    }

    [Fact]
    public async Task ExecuteAsync_WithListActionAndEmptyRoots_ReturnsNoRootsMessage()
    {
        // Arrange
        _rootRegistryMock.Setup(x => x.Roots)
            .Returns(new List<Root>().AsReadOnly());

        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> { ["action"] = "list" }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("No roots configured");
    }

    [Fact]
    public async Task ExecuteAsync_WithCheckActionAndValidUri_ReturnsAccessInfo()
    {
        // Arrange
        var testUri = "file:///project1/file.txt";
        var containingRoot = new Root { Uri = "file:///project1", Name = "Project 1" };

        _rootRegistryMock.Setup(x => x.IsWithinRootBoundaries(testUri))
            .Returns(true);
        _rootRegistryMock.Setup(x => x.GetContainingRoot(testUri))
            .Returns(containingRoot);

        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> 
            { 
                ["action"] = "check",
                ["uri"] = testUri
            }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("Access allowed: Yes")
            .And.Contain("Project 1");
    }

    [Fact]
    public async Task ExecuteAsync_WithCheckActionAndInvalidUri_ReturnsAccessDenied()
    {
        // Arrange
        var testUri = "file:///unauthorized/file.txt";

        _rootRegistryMock.Setup(x => x.IsWithinRootBoundaries(testUri))
            .Returns(false);
        _rootRegistryMock.Setup(x => x.GetContainingRoot(testUri))
            .Returns((Root?)null);
        _rootRegistryMock.Setup(x => x.HasRoots)
            .Returns(true);

        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> 
            { 
                ["action"] = "check",
                ["uri"] = testUri
            }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("Access allowed: No")
            .And.Contain("outside all configured roots");
    }

    [Fact]
    public async Task ExecuteAsync_WithCheckActionButMissingUri_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> { ["action"] = "check" }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("'uri' parameter is required for check action");
    }

    [Fact]
    public async Task ExecuteAsync_WithAddAction_AddsRootAndReturnsSuccess()
    {
        // Arrange
        var uri = "file:///new-project";
        var name = "New Project";

        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> 
            { 
                ["action"] = "add",
                ["uri"] = uri,
                ["name"] = name
            }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain($"Added root: {uri}")
            .And.Contain($"({name})");

        _rootRegistryMock.Verify(x => x.AddRoot(It.Is<Root>(r => r.Uri == uri && r.Name == name)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithAddActionButMissingUri_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> { ["action"] = "add" }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("'uri' parameter is required for add action");
    }

    [Fact]
    public async Task ExecuteAsync_WithRemoveAction_RemovesRootAndReturnsSuccess()
    {
        // Arrange
        var uri = "file:///project-to-remove";

        _rootRegistryMock.Setup(x => x.RemoveRoot(uri))
            .Returns(true);

        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> 
            { 
                ["action"] = "remove",
                ["uri"] = uri
            }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain($"Removed root: {uri}");

        _rootRegistryMock.Verify(x => x.RemoveRoot(uri), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithRemoveActionForNonExistentRoot_ReturnsNotFound()
    {
        // Arrange
        var uri = "file:///nonexistent";

        _rootRegistryMock.Setup(x => x.RemoveRoot(uri))
            .Returns(false);

        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> 
            { 
                ["action"] = "remove",
                ["uri"] = uri
            }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain($"Root not found: {uri}");
    }

    [Fact]
    public async Task ExecuteAsync_WithClearAction_ClearsAllRootsAndReturnsSuccess()
    {
        // Arrange
        _rootRegistryMock.Setup(x => x.Roots)
            .Returns(new List<Root> 
            { 
                new Root { Uri = "file:///project1", Name = "Project 1" },
                new Root { Uri = "file:///project2", Name = "Project 2" }
            }.AsReadOnly());

        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> { ["action"] = "clear" }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("Cleared all roots (2 removed)");

        _rootRegistryMock.Verify(x => x.ClearRoots(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownAction_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> { ["action"] = "unknown" }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("Unknown action 'unknown'");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingActionParameter_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?>()
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("'action' parameter is required");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullArguments_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = null
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("'action' parameter is required");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRootRegistryNotAvailable_ReturnsError()
    {
        // Arrange
        _serviceProviderMock.Setup(x => x.GetService(typeof(IRootRegistry)))
            .Returns((object?)null);

        var tool = new RootsDemoTool(_loggerMock.Object, _serviceProviderMock.Object);
        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> { ["action"] = "list" }
        };

        // Act
        var result = await tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("Root registry service is not available");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRootRegistryThrowsException_ReturnsError()
    {
        // Arrange
        _rootRegistryMock.Setup(x => x.Roots)
            .Throws(new InvalidOperationException("Registry error"));

        var request = new ToolRequest
        {
            Name = "roots_demo",
            Arguments = new Dictionary<string, object?> { ["action"] = "list" }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        result.Content.Should().ContainSingle()
            .Which.Should().BeOfType<ToolsTextContent>()
            .Which.Text.Should().Contain("Error executing action 'list'");
    }
}