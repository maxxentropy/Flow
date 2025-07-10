using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class SamplingServiceTests
{
    private readonly Mock<ILogger<SamplingService>> _loggerMock;
    private readonly Mock<ITransport> _transportMock;
    private readonly SamplingService _samplingService;
    
    public SamplingServiceTests()
    {
        _loggerMock = new Mock<ILogger<SamplingService>>();
        _transportMock = new Mock<ITransport>();
        _samplingService = new SamplingService(_loggerMock.Object);
    }
    
    [Fact]
    public void IsSamplingSupported_WithoutCapabilities_ReturnsFalse()
    {
        // Act
        var result = _samplingService.IsSamplingSupported;
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void IsSamplingSupported_WithSamplingCapability_ReturnsTrue()
    {
        // Arrange
        var capabilities = new ClientCapabilities
        {
            Sampling = new { }
        };
        
        // Act
        _samplingService.SetClientCapabilities(capabilities);
        
        // Assert
        _samplingService.IsSamplingSupported.Should().BeTrue();
    }
    
    [Fact]
    public async Task CreateMessageAsync_WithoutSamplingSupport_ThrowsException()
    {
        // Arrange
        var request = new CreateMessageRequest
        {
            Messages = new List<SamplingMessage>
            {
                new() { Role = "user", Content = new TextContent { Text = "Test" } }
            }
        };
        
        // Act & Assert
        await Assert.ThrowsAsync<ProtocolException>(() => 
            _samplingService.CreateMessageAsync(request));
    }
    
    [Fact]
    public async Task CreateMessageAsync_WithoutTransport_ThrowsException()
    {
        // Arrange
        _samplingService.SetClientCapabilities(new ClientCapabilities { Sampling = new { } });
        var request = new CreateMessageRequest
        {
            Messages = new List<SamplingMessage>
            {
                new() { Role = "user", Content = new TextContent { Text = "Test" } }
            }
        };
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _samplingService.CreateMessageAsync(request));
    }
    
    [Fact]
    public async Task CreateMessageAsync_SendsRequestAndReceivesResponse()
    {
        // Arrange
        _samplingService.SetClientCapabilities(new ClientCapabilities { Sampling = new { } });
        _samplingService.SetTransport(_transportMock.Object);
        
        var request = new CreateMessageRequest
        {
            Messages = new List<SamplingMessage>
            {
                new() { Role = "user", Content = new TextContent { Text = "Hello" } }
            }
        };
        
        // We'll create a raw JSON response to avoid polymorphic serialization issues
        var expectedResponseJson = @"{
            ""model"": ""test-model"",
            ""role"": ""assistant"",
            ""content"": {
                ""type"": ""text"",
                ""text"": ""Hello! How can I help?""
            }
        }";
        
        // Simulate response after request is sent
        _transportMock.Setup(x => x.SendMessageAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((msg, ct) =>
            {
                // Simulate receiving response
                Task.Delay(10, CancellationToken.None).ContinueWith(_ =>
                {
                    var response = $@"{{
                        ""jsonrpc"": ""2.0"",
                        ""id"": 1,
                        ""result"": {expectedResponseJson}
                    }}";
                    _transportMock.Raise(x => x.MessageReceived += null, 
                        new MessageReceivedEventArgs(response));
                }, CancellationToken.None);
            })
            .Returns(Task.CompletedTask);
        
        // Act
        var result = await _samplingService.CreateMessageAsync(request);
        
        // Assert
        result.Should().NotBeNull();
        result.Model.Should().Be("test-model");
        result.Role.Should().Be("assistant");
        result.Content.Should().BeOfType<TextContent>();
        ((TextContent)result.Content).Text.Should().Be("Hello! How can I help?");
        
        _transportMock.Verify(x => x.SendMessageAsync(
            It.Is<object>(o => o.ToString()!.Contains("sampling/createMessage")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task CreateMessageAsync_HandlesErrorResponse()
    {
        // Arrange
        _samplingService.SetClientCapabilities(new ClientCapabilities { Sampling = new { } });
        _samplingService.SetTransport(_transportMock.Object);
        
        var request = new CreateMessageRequest
        {
            Messages = new List<SamplingMessage>
            {
                new() { Role = "user", Content = new TextContent { Text = "Test" } }
            }
        };
        
        // Simulate error response
        _transportMock.Setup(x => x.SendMessageAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((msg, ct) =>
            {
                Task.Delay(10, CancellationToken.None).ContinueWith(_ =>
                {
                    var response = JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id = 1,
                        error = new
                        {
                            code = -32603,
                            message = "Internal error"
                        }
                    });
                    _transportMock.Raise(x => x.MessageReceived += null, 
                        new MessageReceivedEventArgs(response));
                }, CancellationToken.None);
            })
            .Returns(Task.CompletedTask);
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ProtocolException>(() => 
            _samplingService.CreateMessageAsync(request));
        ex.Message.Should().Contain("Internal error");
    }
    
    [Fact]
    public async Task CreateMessageAsync_HandlesTimeout()
    {
        // Arrange
        _samplingService.SetClientCapabilities(new ClientCapabilities { Sampling = new { } });
        _samplingService.SetTransport(_transportMock.Object);
        
        var request = new CreateMessageRequest
        {
            Messages = new List<SamplingMessage>
            {
                new() { Role = "user", Content = new TextContent { Text = "Test" } }
            },
            MaxTokens = 1000
        };
        
        // Don't send any response to trigger timeout
        _transportMock.Setup(x => x.SendMessageAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        // Act & Assert
        // Use a short timeout for the test
        using var cts = new CancellationTokenSource(100); // 100ms timeout
        
        await Assert.ThrowsAnyAsync<Exception>(async () => 
            await _samplingService.CreateMessageAsync(request, cts.Token));
    }
    
    [Fact]
    public void SetClientCapabilities_UpdatesCapabilities()
    {
        // Arrange
        var capabilities = new ClientCapabilities
        {
            Sampling = new SamplingCapability()
        };
        
        // Act
        _samplingService.SetClientCapabilities(capabilities);
        
        // Assert
        _samplingService.IsSamplingSupported.Should().BeTrue();
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Client capabilities updated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}