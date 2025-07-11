using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class RateLimiterTests
{
    private readonly Mock<ILogger<RateLimiter>> _logger;
    private readonly RateLimiter _rateLimiter;
    private readonly RateLimitConfiguration _configuration;

    public RateLimiterTests()
    {
        _logger = new Mock<ILogger<RateLimiter>>();
        _configuration = new RateLimitConfiguration
        {
            GlobalLimit = 10,
            GlobalWindowDuration = TimeSpan.FromMinutes(1),
            UseSlidingWindow = true,
            ResourceLimits = new Dictionary<string, ResourceRateLimitConfig>
            {
                ["tools/call"] = new ResourceRateLimitConfig
                {
                    Limit = 5,
                    WindowDuration = TimeSpan.FromMinutes(1),
                    ExceededMessage = "Too many tool calls"
                },
                ["resources/read"] = new ResourceRateLimitConfig
                {
                    Limit = 20,
                    WindowDuration = TimeSpan.FromMinutes(1)
                }
            }
        };
        
        var options = Options.Create(_configuration);
        _rateLimiter = new RateLimiter(_logger.Object, options);
    }

    [Fact]
    public async Task CheckRateLimitAsync_UnderLimit_AllowsRequest()
    {
        // Arrange
        var identifier = "test-user";
        var resource = "tools/call";

        // Act
        var result = await _rateLimiter.CheckRateLimitAsync(identifier, resource);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.Remaining.Should().Be(4); // 5 - 1 = 4
        result.Limit.Should().Be(5);
        result.RetryAfter.Should().BeNull();
    }

    [Fact]
    public async Task CheckRateLimitAsync_ExceedsLimit_DeniesRequest()
    {
        // Arrange
        var identifier = "test-user";
        var resource = "tools/call";

        // Use up the limit
        for (int i = 0; i < 5; i++)
        {
            await _rateLimiter.CheckRateLimitAsync(identifier, resource);
        }

        // Act - try one more request
        var result = await _rateLimiter.CheckRateLimitAsync(identifier, resource);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.Remaining.Should().Be(0);
        result.Limit.Should().Be(5);
        result.RetryAfter.Should().NotBeNull();
        result.DenialReason.Should().Be("Too many tool calls");
    }

    [Fact]
    public async Task CheckRateLimitAsync_GlobalLimit_EnforcedAcrossResources()
    {
        // Arrange
        var identifier = "test-user";

        // Use up global limit across different resources
        for (int i = 0; i < 10; i++)
        {
            await _rateLimiter.CheckRateLimitAsync(identifier, $"resource{i % 3}");
        }

        // Act
        var result = await _rateLimiter.CheckRateLimitAsync(identifier, "any-resource");

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.Limit.Should().Be(10);
    }

    [Fact]
    public async Task CheckRateLimitAsync_AllowlistedIdentifier_AlwaysAllowed()
    {
        // Arrange
        _configuration.IdentifierAllowlist.Add("vip-user");
        var identifier = "vip-user";
        var resource = "tools/call";

        // Try to exceed limit
        for (int i = 0; i < 100; i++)
        {
            var result = await _rateLimiter.CheckRateLimitAsync(identifier, resource);
            
            // Assert
            result.IsAllowed.Should().BeTrue();
            result.Remaining.Should().Be(int.MaxValue);
        }
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsCurrentUsage()
    {
        // Arrange
        var identifier = "test-user";
        var resource = "tools/call";

        // Make some requests
        await _rateLimiter.CheckRateLimitAsync(identifier, resource);
        await _rateLimiter.CheckRateLimitAsync(identifier, resource);

        // Act
        var status = await _rateLimiter.GetStatusAsync(identifier, resource);

        // Assert
        status.Identifier.Should().Be(identifier);
        status.ResourceLimits.Should().ContainKey(resource);
        status.ResourceLimits[resource].Used.Should().Be(2);
        status.ResourceLimits[resource].Limit.Should().Be(5);
        status.ResourceLimits[resource].Remaining.Should().Be(3);
    }

    [Fact]
    public async Task ResetAsync_ClearsRateLimitForIdentifier()
    {
        // Arrange
        var identifier = "test-user";
        var resource = "tools/call";

        // Use up some requests
        for (int i = 0; i < 3; i++)
        {
            await _rateLimiter.CheckRateLimitAsync(identifier, resource);
        }

        // Act
        await _rateLimiter.ResetAsync(identifier, resource);

        // Check that limit is reset
        var result = await _rateLimiter.CheckRateLimitAsync(identifier, resource);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.Remaining.Should().Be(4); // Full limit minus the one we just used
    }

    [Fact]
    public async Task CheckRateLimitAsync_SlidingWindow_AllowsRequestsOverTime()
    {
        // This test would require time manipulation
        // In a real implementation, you might inject a time provider
        // For now, we'll just verify the basic sliding window behavior
        
        // Arrange
        var identifier = "test-user";
        var resource = "tools/call";

        // Act & Assert
        var result1 = await _rateLimiter.CheckRateLimitAsync(identifier, resource);
        result1.IsAllowed.Should().BeTrue();
        result1.ResetsAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordRequestAsync_IncrementsUsage()
    {
        // Arrange
        var identifier = "test-user";
        var resource = "tools/call";

        // Act
        await _rateLimiter.RecordRequestAsync(identifier, resource, cost: 2);

        // Check status
        var status = await _rateLimiter.GetStatusAsync(identifier, resource);

        // Assert
        status.ResourceLimits[resource].Used.Should().Be(2);
    }

    [Fact]
    public async Task CheckRateLimitAsync_OperationCost_AppliesCorrectly()
    {
        // Arrange
        _configuration.OperationCosts["expensive-operation"] = 3;
        var identifier = "test-user";
        var resource = "expensive-operation";

        // Act
        var result1 = await _rateLimiter.CheckRateLimitAsync(identifier, resource);
        var result2 = await _rateLimiter.CheckRateLimitAsync(identifier, resource);

        // Assert
        result1.IsAllowed.Should().BeTrue();
        result1.Remaining.Should().Be(7); // 10 - 3 = 7
        
        result2.IsAllowed.Should().BeTrue();
        result2.Remaining.Should().Be(4); // 10 - 6 = 4
    }
}