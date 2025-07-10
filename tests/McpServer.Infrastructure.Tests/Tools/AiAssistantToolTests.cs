using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Tools;
using McpServer.Infrastructure.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SamplingTextContent = McpServer.Domain.Protocol.Messages.TextContent;

namespace McpServer.Infrastructure.Tests.Tools;

public class AiAssistantToolTests
{
    private readonly Mock<ILogger<AiAssistantTool>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ISamplingService> _samplingServiceMock;
    private readonly AiAssistantTool _tool;
    
    public AiAssistantToolTests()
    {
        _loggerMock = new Mock<ILogger<AiAssistantTool>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _samplingServiceMock = new Mock<ISamplingService>();
        
        _serviceProviderMock.Setup(x => x.GetService(typeof(ISamplingService)))
            .Returns(_samplingServiceMock.Object);
        
        _tool = new AiAssistantTool(_loggerMock.Object, _serviceProviderMock.Object);
    }
    
    [Fact]
    public void Properties_AreCorrectlySet()
    {
        // Assert
        _tool.Name.Should().Be("ai_assistant");
        _tool.Description.Should().Be("Get AI assistance for various tasks");
        _tool.Schema.Should().NotBeNull();
        _tool.Schema.Properties.Should().ContainKey("task");
        _tool.Schema.Required.Should().Contain("task");
    }
    
    [Fact]
    public async Task ExecuteAsync_WithoutSamplingService_ThrowsException()
    {
        // Arrange
        _serviceProviderMock.Setup(x => x.GetService(typeof(ISamplingService)))
            .Returns((ISamplingService?)null);
        
        var request = new ToolRequest
        {
            Name = "ai_assistant",
            Arguments = new Dictionary<string, object?> { ["task"] = "Test task" }
        };
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ToolExecutionException>(() => 
            _tool.ExecuteAsync(request));
        ex.Message.Should().Contain("Sampling service is not available");
    }
    
    [Fact]
    public async Task ExecuteAsync_WithoutSamplingSupport_ThrowsException()
    {
        // Arrange
        _samplingServiceMock.Setup(x => x.IsSamplingSupported).Returns(false);
        
        var request = new ToolRequest
        {
            Name = "ai_assistant",
            Arguments = new Dictionary<string, object?> { ["task"] = "Test task" }
        };
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ToolExecutionException>(() => 
            _tool.ExecuteAsync(request));
        ex.Message.Should().Contain("does not support sampling");
    }
    
    [Fact]
    public async Task ExecuteAsync_WithoutTask_ReturnsError()
    {
        // Arrange
        _samplingServiceMock.Setup(x => x.IsSamplingSupported).Returns(true);
        
        var request = new ToolRequest
        {
            Name = "ai_assistant",
            Arguments = new Dictionary<string, object?>()
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.IsError.Should().BeTrue();
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<McpServer.Domain.Tools.TextContent>();
        ((McpServer.Domain.Tools.TextContent)result.Content[0]).Text.Should().Contain("task");
    }
    
    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        _samplingServiceMock.Setup(x => x.IsSamplingSupported).Returns(true);
        _samplingServiceMock.Setup(x => x.CreateMessageAsync(It.IsAny<CreateMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateMessageResponse
            {
                Model = "claude-3",
                Role = "assistant",
                Content = new SamplingTextContent { Text = "Here's the help you requested." },
                StopReason = "endTurn"
            });
        
        var request = new ToolRequest
        {
            Name = "ai_assistant",
            Arguments = new Dictionary<string, object?> 
            { 
                ["task"] = "Help me write a test",
                ["context"] = "I need to test a sampling service"
            }
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<McpServer.Domain.Tools.TextContent>();
        var textContent = (McpServer.Domain.Tools.TextContent)result.Content[0];
        textContent.Text.Should().Contain("Here's the help you requested.");
        textContent.Text.Should().Contain("Model: claude-3");
        textContent.Text.Should().Contain("Stop Reason: endTurn");
        
        _samplingServiceMock.Verify(x => x.CreateMessageAsync(
            It.Is<CreateMessageRequest>(r => 
                r.Messages.Count == 1 &&
                r.Messages[0].Role == "user" &&
                ((SamplingTextContent)r.Messages[0].Content).Text.Contains("Help me write a test") &&
                ((SamplingTextContent)r.Messages[0].Content).Text.Contains("I need to test a sampling service")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithTemperatureAndMaxTokens_PassesThemToSampling()
    {
        // Arrange
        _samplingServiceMock.Setup(x => x.IsSamplingSupported).Returns(true);
        _samplingServiceMock.Setup(x => x.CreateMessageAsync(It.IsAny<CreateMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateMessageResponse
            {
                Model = "claude-3",
                Role = "assistant",
                Content = new SamplingTextContent { Text = "Response" }
            });
        
        var request = new ToolRequest
        {
            Name = "ai_assistant",
            Arguments = new Dictionary<string, object?> 
            { 
                ["task"] = "Test",
                ["temperature"] = 0.7,
                ["max_tokens"] = 500
            }
        };
        
        // Act
        await _tool.ExecuteAsync(request);
        
        // Assert
        _samplingServiceMock.Verify(x => x.CreateMessageAsync(
            It.Is<CreateMessageRequest>(r => 
                r.Temperature == 0.7 &&
                r.MaxTokens == 500),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithImageResponse_HandlesCorrectly()
    {
        // Arrange
        _samplingServiceMock.Setup(x => x.IsSamplingSupported).Returns(true);
        _samplingServiceMock.Setup(x => x.CreateMessageAsync(It.IsAny<CreateMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateMessageResponse
            {
                Model = "claude-3",
                Role = "assistant",
                Content = new McpServer.Domain.Protocol.Messages.ImageContent 
                { 
                    Data = "base64data",
                    MimeType = "image/png"
                }
            });
        
        var request = new ToolRequest
        {
            Name = "ai_assistant",
            Arguments = new Dictionary<string, object?> { ["task"] = "Generate an image" }
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request);
        
        // Assert
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<McpServer.Domain.Tools.TextContent>();
        ((McpServer.Domain.Tools.TextContent)result.Content[0]).Text.Should().Contain("Received non-text response");
    }
    
    [Fact]
    public async Task ExecuteAsync_WhenSamplingThrows_WrapsException()
    {
        // Arrange
        _samplingServiceMock.Setup(x => x.IsSamplingSupported).Returns(true);
        _samplingServiceMock.Setup(x => x.CreateMessageAsync(It.IsAny<CreateMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Sampling failed"));
        
        var request = new ToolRequest
        {
            Name = "ai_assistant",
            Arguments = new Dictionary<string, object?> { ["task"] = "Test" }
        };
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ToolExecutionException>(() => 
            _tool.ExecuteAsync(request));
        ex.Message.Should().Contain("Failed to get AI assistance");
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
    }
}