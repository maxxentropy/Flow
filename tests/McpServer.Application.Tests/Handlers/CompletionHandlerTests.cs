using FluentAssertions;
using McpServer.Application.Handlers;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Handlers;

public class CompletionHandlerTests
{
    private readonly Mock<ILogger<CompletionHandler>> _loggerMock;
    private readonly Mock<ICompletionService> _completionServiceMock;
    private readonly CompletionHandler _handler;

    public CompletionHandlerTests()
    {
        _loggerMock = new Mock<ILogger<CompletionHandler>>();
        _completionServiceMock = new Mock<ICompletionService>();
        _handler = new CompletionHandler(_loggerMock.Object, _completionServiceMock.Object);
    }

    [Fact]
    public void CanHandle_WithCompletionCompleteRequest_ReturnsTrue()
    {
        // Act
        var result = _handler.CanHandle(typeof(CompletionCompleteRequest));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithOtherType_ReturnsFalse()
    {
        // Act
        var result = _handler.CanHandle(typeof(InitializeRequest));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleMessageAsync_WithValidRequest_ReturnsCompletionResponse()
    {
        // Arrange
        var completionRequest = new CompletionCompleteRequest
        {
            Ref = new CompletionReference
            {
                Type = "ref/prompt",
                Name = "test-prompt"
            },
            Argument = new CompletionArgument
            {
                Name = "input",
                Value = "test"
            }
        };

        var request = new JsonRpcRequest<CompletionCompleteRequest>
        {
            Jsonrpc = "2.0",
            Id = "test-id",
            Method = "completion/complete",
            Params = completionRequest
        };

        var expectedResponse = new CompletionCompleteResponse
        {
            Completion = new[]
            {
                new CompletionItem
                {
                    Value = "input",
                    Label = "input",
                    Description = "Input parameter"
                }
            },
            HasMore = false,
            Total = 1
        };

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().BeEquivalentTo(expectedResponse);
        
        _completionServiceMock.Verify(s => s.GetCompletionAsync(
            It.Is<CompletionReference>(r => r.Type == "ref/prompt" && r.Name == "test-prompt"),
            It.Is<CompletionArgument>(a => a.Name == "input" && a.Value == "test"),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_WithNullParams_ReturnsEmptyResponse()
    {
        // Arrange
        var request = new JsonRpcRequest<CompletionCompleteRequest>
        {
            Jsonrpc = "2.0",
            Id = "test-id",
            Method = "completion/complete",
            Params = null
        };

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().BeOfType<CompletionCompleteResponse>();
        var response = (CompletionCompleteResponse)result!;
        response.Completion.Should().BeEmpty();
        response.HasMore.Should().BeFalse();
        response.Total.Should().Be(0);

        _completionServiceMock.Verify(s => s.GetCompletionAsync(
            It.IsAny<CompletionReference>(),
            It.IsAny<CompletionArgument>(),
            It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_WithInvalidMessageType_ThrowsArgumentException()
    {
        // Arrange
        var invalidMessage = "invalid message";

        // Act & Assert
        await _handler.Invoking(h => h.HandleMessageAsync(invalidMessage))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("message");
    }

    [Fact]
    public async Task HandleMessageAsync_WithServiceException_ReturnsEmptyResponse()
    {
        // Arrange
        var completionRequest = new CompletionCompleteRequest
        {
            Ref = new CompletionReference
            {
                Type = "ref/prompt",
                Name = "test-prompt"
            },
            Argument = new CompletionArgument
            {
                Name = "input",
                Value = "test"
            }
        };

        var request = new JsonRpcRequest<CompletionCompleteRequest>
        {
            Jsonrpc = "2.0",
            Id = "test-id",
            Method = "completion/complete",
            Params = completionRequest
        };

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service error"));

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().BeOfType<CompletionCompleteResponse>();
        var response = (CompletionCompleteResponse)result!;
        response.Completion.Should().BeEmpty();
        response.HasMore.Should().BeFalse();
        response.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleMessageAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var completionRequest = new CompletionCompleteRequest
        {
            Ref = new CompletionReference
            {
                Type = "ref/prompt",
                Name = "test-prompt"
            },
            Argument = new CompletionArgument
            {
                Name = "input",
                Value = "test"
            }
        };

        var request = new JsonRpcRequest<CompletionCompleteRequest>
        {
            Jsonrpc = "2.0",
            Id = "test-id",
            Method = "completion/complete",
            Params = completionRequest
        };

        var cancellationToken = new CancellationToken(true);

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _handler.HandleMessageAsync(request, cancellationToken);

        // Assert
        result.Should().BeOfType<CompletionCompleteResponse>();
        var response = (CompletionCompleteResponse)result!;
        response.Completion.Should().BeEmpty();
        
        _completionServiceMock.Verify(s => s.GetCompletionAsync(
            It.IsAny<CompletionReference>(),
            It.IsAny<CompletionArgument>(),
            cancellationToken), 
            Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_WithResourceReference_HandlesCorrectly()
    {
        // Arrange
        var completionRequest = new CompletionCompleteRequest
        {
            Ref = new CompletionReference
            {
                Type = "ref/resource",
                Name = "file-resource"
            },
            Argument = new CompletionArgument
            {
                Name = "uri",
                Value = "file://"
            }
        };

        var request = new JsonRpcRequest<CompletionCompleteRequest>
        {
            Jsonrpc = "2.0",
            Id = "test-id",
            Method = "completion/complete",
            Params = completionRequest
        };

        var expectedResponse = new CompletionCompleteResponse
        {
            Completion = new[]
            {
                new CompletionItem
                {
                    Value = "file:///path/to/resource.txt",
                    Label = "resource.txt",
                    Description = "Resource file"
                }
            },
            HasMore = false,
            Total = 1
        };

        _completionServiceMock.Setup(s => s.GetCompletionAsync(
                It.IsAny<CompletionReference>(),
                It.IsAny<CompletionArgument>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.HandleMessageAsync(request);

        // Assert
        result.Should().BeEquivalentTo(expectedResponse);
        
        _completionServiceMock.Verify(s => s.GetCompletionAsync(
            It.Is<CompletionReference>(r => r.Type == "ref/resource" && r.Name == "file-resource"),
            It.Is<CompletionArgument>(a => a.Name == "uri" && a.Value == "file://"),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}