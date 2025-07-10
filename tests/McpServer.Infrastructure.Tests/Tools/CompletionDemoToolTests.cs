using FluentAssertions;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using McpServer.Domain.Tools;
using McpServer.Infrastructure.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TextContent = McpServer.Domain.Tools.TextContent;

namespace McpServer.Infrastructure.Tests.Tools;

public class CompletionDemoToolTests
{
    private readonly Mock<ILogger<CompletionDemoTool>> _loggerMock;
    private readonly Mock<ICompletionService> _completionServiceMock;
    private readonly CompletionDemoTool _tool;

    public CompletionDemoToolTests()
    {
        _loggerMock = new Mock<ILogger<CompletionDemoTool>>();
        _completionServiceMock = new Mock<ICompletionService>();
        _tool = new CompletionDemoTool(_loggerMock.Object, _completionServiceMock.Object);
    }

    [Fact]
    public void Name_ReturnsCorrectName()
    {
        _tool.Name.Should().Be("completion_demo");
    }

    [Fact]
    public void Description_ReturnsCorrectDescription()
    {
        _tool.Description.Should().Be("Demonstrates completion functionality for prompts and resources");
    }

    [Fact]
    public void Schema_HasCorrectStructure()
    {
        var schema = _tool.Schema;
        
        schema.Type.Should().Be("object");
        schema.Properties.Should().ContainKey("reference_type");
        schema.Properties.Should().ContainKey("reference_name");
        schema.Properties.Should().ContainKey("argument_name");
        schema.Properties.Should().ContainKey("argument_value");
        schema.Required.Should().BeEquivalentTo(new List<string> { "reference_type", "reference_name", "argument_name", "argument_value" });
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingArguments_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "completion_demo",
            Arguments = new Dictionary<string, object?>
            {
                ["reference_type"] = "ref/prompt"
                // Missing other required arguments
            }
        };

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<TextContent>();
        ((TextContent)result.Content[0]).Text.Should().Contain("Missing required arguments");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidArguments_ReturnsCompletionResults()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "completion_demo",
            Arguments = new Dictionary<string, object?>
            {
                ["reference_type"] = "ref/prompt",
                ["reference_name"] = "test-prompt",
                ["argument_name"] = "input",
                ["argument_value"] = "test"
            }
        };

        var completionResponse = new CompletionCompleteResponse
        {
            Completion = new[]
            {
                new CompletionItem
                {
                    Value = "test_value",
                    Label = "Test Value",
                    Description = "A test completion value"
                }
            },
            HasMore = false,
            Total = 1
        };

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(completionResponse);

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<TextContent>();
        ((TextContent)result.Content[0]).Text.Should().Contain("Found 1 completion(s)");
        ((TextContent)result.Content[0]).Text.Should().Contain("test_value");
        ((TextContent)result.Content[0]).Text.Should().Contain("Test Value");
        ((TextContent)result.Content[0]).Text.Should().Contain("A test completion value");

        _completionServiceMock.Verify(s => s.GetCompletionAsync(
            It.Is<CompletionReference>(r => r.Type == "ref/prompt" && r.Name == "test-prompt"),
            It.Is<CompletionArgument>(a => a.Name == "input" && a.Value == "test"),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoCompletions_ReturnsNoCompletionsMessage()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "completion_demo",
            Arguments = new Dictionary<string, object?>
            {
                ["reference_type"] = "ref/resource",
                ["reference_name"] = "test-resource",
                ["argument_name"] = "uri",
                ["argument_value"] = "nonexistent"
            }
        };

        var completionResponse = new CompletionCompleteResponse
        {
            Completion = Array.Empty<CompletionItem>(),
            HasMore = false,
            Total = 0
        };

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(completionResponse);

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<TextContent>();
        ((TextContent)result.Content[0]).Text.Should().Contain("No completions found");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleCompletions_ShowsAllResults()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "completion_demo",
            Arguments = new Dictionary<string, object?>
            {
                ["reference_type"] = "ref/resource",
                ["reference_name"] = "files",
                ["argument_name"] = "uri",
                ["argument_value"] = "file:"
            }
        };

        var completionResponse = new CompletionCompleteResponse
        {
            Completion = new[]
            {
                new CompletionItem
                {
                    Value = "file:///path/to/file1.txt",
                    Label = "file1.txt"
                },
                new CompletionItem
                {
                    Value = "file:///path/to/file2.txt",
                    Label = "file2.txt",
                    Description = "Second file"
                }
            },
            HasMore = true,
            Total = 5
        };

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(completionResponse);

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<TextContent>();
        ((TextContent)result.Content[0]).Text.Should().Contain("Found 5 completion(s)");
        ((TextContent)result.Content[0]).Text.Should().Contain("file:///path/to/file1.txt");
        ((TextContent)result.Content[0]).Text.Should().Contain("file:///path/to/file2.txt");
        ((TextContent)result.Content[0]).Text.Should().Contain("Second file");
        ((TextContent)result.Content[0]).Text.Should().Contain("(More completions available)");
    }

    [Fact]
    public async Task ExecuteAsync_WithCompletionServiceException_ReturnsError()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "completion_demo",
            Arguments = new Dictionary<string, object?>
            {
                ["reference_type"] = "ref/prompt",
                ["reference_name"] = "test-prompt",
                ["argument_name"] = "input",
                ["argument_value"] = "test"
            }
        };

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service error"));

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<TextContent>();
        ((TextContent)result.Content[0]).Text.Should().Contain("Error: Service error");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "completion_demo",
            Arguments = new Dictionary<string, object?>
            {
                ["reference_type"] = "ref/prompt",
                ["reference_name"] = "test-prompt",
                ["argument_name"] = "input",
                ["argument_value"] = "test"
            }
        };

        var cancellationToken = new CancellationToken(true);

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _tool.ExecuteAsync(request, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().Be(true);
        ((TextContent)result.Content[0]).Text.Should().Contain("canceled");

        _completionServiceMock.Verify(s => s.GetCompletionAsync(
            It.IsAny<CompletionReference>(),
            It.IsAny<CompletionArgument>(),
            cancellationToken), 
            Times.Once);
    }

    [Theory]
    [InlineData("ref/prompt")]
    [InlineData("ref/resource")]
    public async Task ExecuteAsync_WithDifferentReferenceTypes_HandlesCorrectly(string referenceType)
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "completion_demo",
            Arguments = new Dictionary<string, object?>
            {
                ["reference_type"] = referenceType,
                ["reference_name"] = "test-ref",
                ["argument_name"] = "arg",
                ["argument_value"] = "value"
            }
        };

        var completionResponse = new CompletionCompleteResponse
        {
            Completion = new[]
            {
                new CompletionItem { Value = "test_completion" }
            },
            HasMore = false,
            Total = 1
        };

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(completionResponse);

        // Act
        var result = await _tool.ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().NotBe(true);

        _completionServiceMock.Verify(s => s.GetCompletionAsync(
            It.Is<CompletionReference>(r => r.Type == referenceType),
            It.IsAny<CompletionArgument>(),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}