using FluentAssertions;
using McpServer.Application.Server;
using McpServer.Application.Services;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Prompts;
using McpServer.Domain.Resources;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class CompletionServiceTests
{
    private readonly Mock<ILogger<CompletionService>> _loggerMock;
    private readonly Mock<IToolRegistry> _toolRegistryMock;
    private readonly Mock<IResourceRegistry> _resourceRegistryMock;
    private readonly Mock<IPromptRegistry> _promptRegistryMock;
    private readonly CompletionService _completionService;

    public CompletionServiceTests()
    {
        _loggerMock = new Mock<ILogger<CompletionService>>();
        _toolRegistryMock = new Mock<IToolRegistry>();
        _resourceRegistryMock = new Mock<IResourceRegistry>();
        _promptRegistryMock = new Mock<IPromptRegistry>();
        
        _completionService = new CompletionService(
            _loggerMock.Object,
            _toolRegistryMock.Object,
            _resourceRegistryMock.Object,
            _promptRegistryMock.Object);
    }

    [Fact]
    public async Task GetCompletionAsync_WithUnsupportedReferenceType_ReturnsEmptyCompletions()
    {
        // Arrange
        var reference = new CompletionReference
        {
            Type = "ref/unknown",
            Name = "test"
        };
        var argument = new CompletionArgument
        {
            Name = "testArg",
            Value = "testValue"
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetCompletionAsync_WithPromptReference_ReturnsPromptCompletions()
    {
        // Arrange
        var mockProvider = new Mock<IPromptProvider>();
        var prompts = new List<Prompt>
        {
            new()
            {
                Name = "test-prompt",
                Description = "Test prompt",
                Arguments = new List<PromptArgument>
                {
                    new()
                    {
                        Name = "input",
                        Description = "Input argument",
                        Required = true
                    }
                }
            }
        };

        mockProvider.Setup(p => p.ListPromptsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(prompts);

        _promptRegistryMock.Setup(r => r.GetPromptProviders())
                          .Returns(new[] { mockProvider.Object });

        var reference = new CompletionReference
        {
            Type = "ref/prompt",
            Name = "test-prompt"
        };
        var argument = new CompletionArgument
        {
            Name = "input",
            Value = ""
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().HaveCount(1);
        result.Completion[0].Value.Should().Be("input");
        result.Completion[0].Label.Should().Be("input");
        result.Completion[0].Description.Should().Be("Input argument");
        result.HasMore.Should().BeFalse();
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetCompletionAsync_WithPromptReference_FiltersByArgumentName()
    {
        // Arrange
        var mockProvider = new Mock<IPromptProvider>();
        var prompts = new List<Prompt>
        {
            new()
            {
                Name = "test-prompt",
                Arguments = new List<PromptArgument>
                {
                    new()
                    {
                        Name = "input",
                        Description = "Input argument"
                    },
                    new()
                    {
                        Name = "output",
                        Description = "Output argument"
                    }
                }
            }
        };

        mockProvider.Setup(p => p.ListPromptsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(prompts);

        _promptRegistryMock.Setup(r => r.GetPromptProviders())
                          .Returns(new[] { mockProvider.Object });

        var reference = new CompletionReference
        {
            Type = "ref/prompt",
            Name = "test-prompt"
        };
        var argument = new CompletionArgument
        {
            Name = "in",
            Value = "in"
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().HaveCount(1);
        result.Completion[0].Value.Should().Be("input");
    }

    [Fact]
    public async Task GetCompletionAsync_WithResourceReference_ReturnsResourceCompletions()
    {
        // Arrange
        var mockProvider = new Mock<IResourceProvider>();
        var resources = new List<Resource>
        {
            new()
            {
                Uri = "file:///path/to/test.txt",
                Name = "test.txt",
                Description = "Test file"
            },
            new()
            {
                Uri = "file:///path/to/example.md",
                Name = "example.md",
                Description = "Example markdown"
            }
        };

        mockProvider.Setup(p => p.ListResourcesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(resources);

        _resourceRegistryMock.Setup(r => r.GetResourceProviders())
                           .Returns(new[] { mockProvider.Object });

        var reference = new CompletionReference
        {
            Type = "ref/resource",
            Name = "file"
        };
        var argument = new CompletionArgument
        {
            Name = "uri",
            Value = "test"
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().HaveCount(1);
        result.Completion[0].Value.Should().Be("file:///path/to/test.txt");
        result.Completion[0].Label.Should().Be("test.txt");
        result.Completion[0].Description.Should().Be("Test file");
    }

    [Fact]
    public async Task GetCompletionAsync_WithResourceReference_FiltersResourcesByValue()
    {
        // Arrange
        var mockProvider = new Mock<IResourceProvider>();
        var resources = new List<Resource>
        {
            new()
            {
                Uri = "file:///path/to/test.txt",
                Name = "test.txt"
            },
            new()
            {
                Uri = "file:///path/to/example.md",
                Name = "example.md"
            }
        };

        mockProvider.Setup(p => p.ListResourcesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(resources);

        _resourceRegistryMock.Setup(r => r.GetResourceProviders())
                           .Returns(new[] { mockProvider.Object });

        var reference = new CompletionReference
        {
            Type = "ref/resource",
            Name = "file"
        };
        var argument = new CompletionArgument
        {
            Name = "uri",
            Value = "example"
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().HaveCount(1);
        result.Completion[0].Value.Should().Be("file:///path/to/example.md");
    }

    [Fact]
    public async Task GetCompletionAsync_WithPromptProviderException_ReturnsEmptyResults()
    {
        // Arrange
        var mockProvider = new Mock<IPromptProvider>();
        mockProvider.Setup(p => p.ListPromptsAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("Test exception"));

        _promptRegistryMock.Setup(r => r.GetPromptProviders())
                          .Returns(new[] { mockProvider.Object });

        var reference = new CompletionReference
        {
            Type = "ref/prompt",
            Name = "test"
        };
        var argument = new CompletionArgument
        {
            Name = "arg",
            Value = "value"
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetCompletionAsync_WithResourceProviderException_ReturnsEmptyResults()
    {
        // Arrange
        var mockProvider = new Mock<IResourceProvider>();
        mockProvider.Setup(p => p.ListResourcesAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("Test exception"));

        _resourceRegistryMock.Setup(r => r.GetResourceProviders())
                           .Returns(new[] { mockProvider.Object });

        var reference = new CompletionReference
        {
            Type = "ref/resource",
            Name = "test"
        };
        var argument = new CompletionArgument
        {
            Name = "uri",
            Value = "value"
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetCompletionAsync_WithMultipleProviders_CombinesResults()
    {
        // Arrange
        var provider1 = new Mock<IResourceProvider>();
        var provider2 = new Mock<IResourceProvider>();

        provider1.Setup(p => p.ListResourcesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[]
                 {
                     new Resource { Uri = "file:///test1.txt", Name = "test1.txt" }
                 });

        provider2.Setup(p => p.ListResourcesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[]
                 {
                     new Resource { Uri = "file:///test2.txt", Name = "test2.txt" }
                 });

        _resourceRegistryMock.Setup(r => r.GetResourceProviders())
                           .Returns(new[] { provider1.Object, provider2.Object });

        var reference = new CompletionReference
        {
            Type = "ref/resource",
            Name = "file"
        };
        var argument = new CompletionArgument
        {
            Name = "uri",
            Value = "test"
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().HaveCount(2);
        result.Completion.Should().Contain(c => c.Value == "file:///test1.txt");
        result.Completion.Should().Contain(c => c.Value == "file:///test2.txt");
    }

    [Fact]
    public async Task GetCompletionAsync_WithNonMatchingPromptName_ReturnsEmptyResults()
    {
        // Arrange
        var mockProvider = new Mock<IPromptProvider>();
        var prompts = new List<Prompt>
        {
            new()
            {
                Name = "different-prompt",
                Arguments = new List<PromptArgument>
                {
                    new() { Name = "arg1", Description = "Argument 1" }
                }
            }
        };

        mockProvider.Setup(p => p.ListPromptsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(prompts);

        _promptRegistryMock.Setup(r => r.GetPromptProviders())
                          .Returns(new[] { mockProvider.Object });

        var reference = new CompletionReference
        {
            Type = "ref/prompt",
            Name = "test-prompt"
        };
        var argument = new CompletionArgument
        {
            Name = "arg1",
            Value = ""
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompletionAsync_WithPromptWithoutArguments_ReturnsEmptyResults()
    {
        // Arrange
        var mockProvider = new Mock<IPromptProvider>();
        var prompts = new List<Prompt>
        {
            new()
            {
                Name = "test-prompt",
                Arguments = null
            }
        };

        mockProvider.Setup(p => p.ListPromptsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(prompts);

        _promptRegistryMock.Setup(r => r.GetPromptProviders())
                          .Returns(new[] { mockProvider.Object });

        var reference = new CompletionReference
        {
            Type = "ref/prompt",
            Name = "test-prompt"
        };
        var argument = new CompletionArgument
        {
            Name = "arg1",
            Value = ""
        };

        // Act
        var result = await _completionService.GetCompletionAsync(reference, argument);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().BeEmpty();
    }
}