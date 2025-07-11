using FluentAssertions;
using McpServer.Application.Caching;
using McpServer.Domain.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Caching;

public class ToolResultCacheTests
{
    private readonly Mock<ITool> _innerToolMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<ToolResultCache>> _loggerMock;
    private readonly ToolCacheOptions _options;
    private readonly ToolResultCache _toolCache;
    
    public ToolResultCacheTests()
    {
        _innerToolMock = new Mock<ITool>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<ToolResultCache>>();
        _options = new ToolCacheOptions
        {
            Enabled = true,
            DefaultExpiration = TimeSpan.FromMinutes(5),
            Priority = CachePriority.Normal,
            MaxResultSize = 1024 * 1024
        };
        
        _toolCache = new ToolResultCache(_innerToolMock.Object, _cacheServiceMock.Object, _loggerMock.Object, _options);
        
        // Setup inner tool
        _innerToolMock.Setup(t => t.Name).Returns("TestTool");
        _innerToolMock.Setup(t => t.Description).Returns("Test tool for caching");
        _innerToolMock.Setup(t => t.Schema).Returns(new ToolSchema
        {
            Type = "object",
            Properties = new Dictionary<string, object>
            {
                { "input", new { type = "string" } }
            }
        });
    }
    
    [Fact]
    public void Name_ReturnsInnerToolName()
    {
        // Act
        var name = _toolCache.Name;
        
        // Assert
        name.Should().Be("TestTool");
    }
    
    [Fact]
    public void Description_ReturnsInnerToolDescription()
    {
        // Act
        var description = _toolCache.Description;
        
        // Assert
        description.Should().Be("Test tool for caching");
    }
    
    [Fact]
    public void Schema_ReturnsInnerToolSchema()
    {
        // Act
        var schema = _toolCache.Schema;
        
        // Assert
        schema.Should().NotBeNull();
        schema.Type.Should().Be("object");
    }
    
    [Fact]
    public async Task ExecuteAsync_WithCacheHit_ReturnsCachedResult()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "TestTool",
            Arguments = new Dictionary<string, object?> { { "input", "test" } }
        };
        
        var cachedResult = new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = "cached result" }
            }
        };
        
        _cacheServiceMock.Setup(c => c.TryGetValue<ToolResult>(It.IsAny<string>(), out It.Ref<ToolResult>.IsAny))
            .Returns(new CacheService.TryGetValueCallback<ToolResult>((string key, out ToolResult value) =>
            {
                value = cachedResult!;
                return true;
            }));
        
        // Act
        var result = await _toolCache.ExecuteAsync(request);
        
        // Assert
        result.Should().Be(cachedResult);
        _innerToolMock.Verify(t => t.ExecuteAsync(It.IsAny<ToolRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithCacheMiss_ExecutesToolAndCachesResult()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "TestTool",
            Arguments = new Dictionary<string, object?> { { "input", "test" } }
        };
        
        var toolResult = new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = "fresh result" }
            }
        };
        
        _cacheServiceMock.Setup(c => c.TryGetValue<ToolResult>(It.IsAny<string>(), out It.Ref<ToolResult>.IsAny))
            .Returns(new CacheService.TryGetValueCallback<ToolResult>((string key, out ToolResult value) =>
            {
                value = default!;
                return false;
            }));
        
        _innerToolMock.Setup(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);
        
        // Act
        var result = await _toolCache.ExecuteAsync(request);
        
        // Assert
        result.Should().Be(toolResult);
        _innerToolMock.Verify(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.Set(It.IsAny<string>(), toolResult, It.IsAny<CacheEntryOptions>()), Times.Once);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithFailedResult_DoesNotCacheResult()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "TestTool",
            Arguments = new Dictionary<string, object?> { { "input", "test" } }
        };
        
        var toolResult = new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = "error result" }
            },
            IsError = true
        };
        
        _cacheServiceMock.Setup(c => c.TryGetValue<ToolResult>(It.IsAny<string>(), out It.Ref<ToolResult>.IsAny))
            .Returns(new CacheService.TryGetValueCallback<ToolResult>((string key, out ToolResult value) =>
            {
                value = default!;
                return false;
            }));
        
        _innerToolMock.Setup(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);
        
        // Act
        var result = await _toolCache.ExecuteAsync(request);
        
        // Assert
        result.Should().Be(toolResult);
        _innerToolMock.Verify(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<ToolResult>(), It.IsAny<CacheEntryOptions>()), Times.Never);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithExcludedTool_DoesNotUseCache()
    {
        // Arrange
        _options.ExcludedTools.Add("TestTool");
        
        var request = new ToolRequest
        {
            Name = "TestTool",
            Arguments = new Dictionary<string, object?> { { "input", "test" } }
        };
        
        var toolResult = new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = "direct result" }
            }
        };
        
        _innerToolMock.Setup(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);
        
        // Act
        var result = await _toolCache.ExecuteAsync(request);
        
        // Assert
        result.Should().Be(toolResult);
        _innerToolMock.Verify(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.TryGetValue<ToolResult>(It.IsAny<string>(), out It.Ref<ToolResult>.IsAny), Times.Never);
        _cacheServiceMock.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<ToolResult>(), It.IsAny<CacheEntryOptions>()), Times.Never);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithStreamingContent_DoesNotCacheResult()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "TestTool",
            Arguments = new Dictionary<string, object?> { { "input", "test" } }
        };
        
        // Create a mock streaming content by using a custom content type
        var streamingContent = new Mock<ToolContent>();
        streamingContent.Setup(c => c.Type).Returns("stream");
        var toolResult = new ToolResult
        {
            Content = new List<ToolContent> { streamingContent.Object }
        };
        
        _cacheServiceMock.Setup(c => c.TryGetValue<ToolResult>(It.IsAny<string>(), out It.Ref<ToolResult>.IsAny))
            .Returns(new CacheService.TryGetValueCallback<ToolResult>((string key, out ToolResult value) =>
            {
                value = default!;
                return false;
            }));
        
        _innerToolMock.Setup(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);
        
        // Act
        var result = await _toolCache.ExecuteAsync(request);
        
        // Assert
        result.Should().Be(toolResult);
        _innerToolMock.Verify(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<ToolResult>(), It.IsAny<CacheEntryOptions>()), Times.Never);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithLargeResult_DoesNotCacheResult()
    {
        // Arrange
        _options.MaxResultSize = 10; // Very small limit
        
        var request = new ToolRequest
        {
            Name = "TestTool",
            Arguments = new Dictionary<string, object?> { { "input", "test" } }
        };
        
        var toolResult = new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = "This is a very long result that exceeds the maximum size limit" }
            }
        };
        
        _cacheServiceMock.Setup(c => c.TryGetValue<ToolResult>(It.IsAny<string>(), out It.Ref<ToolResult>.IsAny))
            .Returns(new CacheService.TryGetValueCallback<ToolResult>((string key, out ToolResult value) =>
            {
                value = default!;
                return false;
            }));
        
        _innerToolMock.Setup(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);
        
        // Act
        var result = await _toolCache.ExecuteAsync(request);
        
        // Assert
        result.Should().Be(toolResult);
        _innerToolMock.Verify(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<ToolResult>(), It.IsAny<CacheEntryOptions>()), Times.Never);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithToolConfiguration_UsesCacheConfiguration()
    {
        // Arrange
        _options.ToolConfigurations["TestTool"] = new ToolCacheConfiguration
        {
            Enabled = true,
            Expiration = TimeSpan.FromMinutes(10),
            Priority = CachePriority.High
        };
        
        var request = new ToolRequest
        {
            Name = "TestTool",
            Arguments = new Dictionary<string, object?> { { "input", "test" } }
        };
        
        var toolResult = new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = "configured result" }
            }
        };
        
        _cacheServiceMock.Setup(c => c.TryGetValue<ToolResult>(It.IsAny<string>(), out It.Ref<ToolResult>.IsAny))
            .Returns(new CacheService.TryGetValueCallback<ToolResult>((string key, out ToolResult value) =>
            {
                value = default!;
                return false;
            }));
        
        _innerToolMock.Setup(t => t.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);
        
        // Act
        var result = await _toolCache.ExecuteAsync(request);
        
        // Assert
        result.Should().Be(toolResult);
        _cacheServiceMock.Verify(c => c.Set(It.IsAny<string>(), toolResult, 
            It.Is<CacheEntryOptions>(opts => opts.Priority == CachePriority.High)), Times.Once);
    }
}

// Helper delegate for mocking TryGetValue
file static class CacheService
{
    public delegate bool TryGetValueCallback<T>(string key, out T value);
}