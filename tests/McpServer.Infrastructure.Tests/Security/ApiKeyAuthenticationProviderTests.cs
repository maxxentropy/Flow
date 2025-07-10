using System.Security.Claims;
using FluentAssertions;
using McpServer.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Infrastructure.Tests.Security;

public class ApiKeyAuthenticationProviderTests
{
    private readonly Mock<ILogger<ApiKeyAuthenticationProvider>> _loggerMock;
    private readonly Mock<IOptions<ApiKeyAuthenticationOptions>> _optionsMock;
    private readonly ApiKeyAuthenticationOptions _options;
    private readonly ApiKeyAuthenticationProvider _provider;

    public ApiKeyAuthenticationProviderTests()
    {
        _loggerMock = new Mock<ILogger<ApiKeyAuthenticationProvider>>();
        _optionsMock = new Mock<IOptions<ApiKeyAuthenticationOptions>>();
        _options = new ApiKeyAuthenticationOptions();
        _optionsMock.Setup(x => x.Value).Returns(_options);
        _provider = new ApiKeyAuthenticationProvider(_loggerMock.Object, _optionsMock.Object);
    }

    [Fact]
    public void Scheme_ReturnsApiKey()
    {
        // Assert
        _provider.Scheme.Should().Be("ApiKey");
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyCredentials_ReturnsFailure()
    {
        // Act
        var result = await _provider.AuthenticateAsync("");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.FailureReason.Should().Be("API key is required");
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidApiKey_ReturnsFailure()
    {
        // Arrange
        _options.ApiKeys["client1"] = new ApiKeyConfiguration
        {
            Key = "valid-key",
            ClientName = "Client 1",
            Enabled = true
        };

        // Act
        var result = await _provider.AuthenticateAsync("invalid-key");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.FailureReason.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task AuthenticateAsync_WithDisabledApiKey_ReturnsFailure()
    {
        // Arrange
        _options.ApiKeys["client1"] = new ApiKeyConfiguration
        {
            Key = "disabled-key",
            ClientName = "Client 1",
            Enabled = false
        };

        // Act
        var result = await _provider.AuthenticateAsync("disabled-key");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.FailureReason.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task AuthenticateAsync_WithExpiredApiKey_ReturnsFailure()
    {
        // Arrange
        _options.ApiKeys["client1"] = new ApiKeyConfiguration
        {
            Key = "expired-key",
            ClientName = "Client 1",
            Enabled = true,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var result = await _provider.AuthenticateAsync("expired-key");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.FailureReason.Should().Be("API key has expired");
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidApiKey_ReturnsSuccess()
    {
        // Arrange
        _options.ApiKeys["client1"] = new ApiKeyConfiguration
        {
            Key = "valid-key",
            ClientName = "Client 1",
            Enabled = true,
            Roles = new List<string> { "user", "developer" },
            Permissions = new List<string> { "read:data", "write:data" }
        };

        // Act
        var result = await _provider.AuthenticateAsync("valid-key");

        // Assert
        result.IsAuthenticated.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Identity!.Name.Should().Be("Client 1");
        result.Principal.Identity.IsAuthenticated.Should().BeTrue();
        result.Principal.IsInRole("user").Should().BeTrue();
        result.Principal.IsInRole("developer").Should().BeTrue();
        result.Principal.HasClaim("permission", "read:data").Should().BeTrue();
        result.Principal.HasClaim("permission", "write:data").Should().BeTrue();
        result.Principal.HasClaim("auth_method", "apikey").Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithFutureExpirationDate_ReturnsSuccess()
    {
        // Arrange
        _options.ApiKeys["client1"] = new ApiKeyConfiguration
        {
            Key = "future-key",
            ClientName = "Client 1",
            Enabled = true,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        // Act
        var result = await _provider.AuthenticateAsync("future-key");

        // Assert
        result.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithoutRolesOrPermissions_StillCreatesValidPrincipal()
    {
        // Arrange
        _options.ApiKeys["client1"] = new ApiKeyConfiguration
        {
            Key = "minimal-key",
            ClientName = "Minimal Client",
            Enabled = true
        };

        // Act
        var result = await _provider.AuthenticateAsync("minimal-key");

        // Assert
        result.IsAuthenticated.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Identity!.Name.Should().Be("Minimal Client");
        result.Principal.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "Minimal Client");
    }
}