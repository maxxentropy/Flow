using System.Security.Claims;
using FluentAssertions;
using McpServer.Application.Services;
using McpServer.Domain.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class AuthenticationServiceTests
{
    private readonly Mock<ILogger<AuthenticationService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly List<IAuthenticationProvider> _providers;
    private readonly AuthenticationService _authenticationService;

    public AuthenticationServiceTests()
    {
        _loggerMock = new Mock<ILogger<AuthenticationService>>();
        _configurationMock = new Mock<IConfiguration>();
        _providers = new List<IAuthenticationProvider>();
        _authenticationService = new AuthenticationService(
            _loggerMock.Object,
            _configurationMock.Object,
            _providers);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyScheme_ReturnsFailure()
    {
        // Act
        var result = await _authenticationService.AuthenticateAsync("", "credentials");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.FailureReason.Should().Be("Authentication scheme is required");
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyCredentials_ReturnsFailure()
    {
        // Act
        var result = await _authenticationService.AuthenticateAsync("Bearer", "");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.FailureReason.Should().Be("Credentials are required");
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnsupportedScheme_ReturnsFailure()
    {
        // Act
        var result = await _authenticationService.AuthenticateAsync("Unknown", "credentials");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.FailureReason.Should().Be("Unsupported authentication scheme: Unknown");
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidProvider_CallsProvider()
    {
        // Arrange
        var providerMock = new Mock<IAuthenticationProvider>();
        providerMock.Setup(p => p.Scheme).Returns("Bearer");
        providerMock.Setup(p => p.AuthenticateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthenticationResult.Success(new ClaimsPrincipal()));
        
        _providers.Add(providerMock.Object);
        var service = new AuthenticationService(_loggerMock.Object, _configurationMock.Object, _providers);

        // Act
        var result = await service.AuthenticateAsync("Bearer", "token");

        // Assert
        result.IsAuthenticated.Should().BeTrue();
        providerMock.Verify(p => p.AuthenticateAsync("token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenProviderThrows_ReturnsFailure()
    {
        // Arrange
        var providerMock = new Mock<IAuthenticationProvider>();
        providerMock.Setup(p => p.Scheme).Returns("Bearer");
        providerMock.Setup(p => p.AuthenticateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider error"));
        
        _providers.Add(providerMock.Object);
        var service = new AuthenticationService(_loggerMock.Object, _configurationMock.Object, _providers);

        // Act
        var result = await service.AuthenticateAsync("Bearer", "token");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.FailureReason.Should().Be("Authentication error occurred");
    }

    [Fact]
    public async Task AuthorizeAsync_WithNullPrincipal_ReturnsFalse()
    {
        // Act
        var result = await _authenticationService.AuthorizeAsync(null!, "resource", "action");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_WithUnauthenticatedPrincipal_ReturnsFalse()
    {
        // Arrange
        var principal = new ClaimsPrincipal();

        // Act
        var result = await _authenticationService.AuthorizeAsync(principal, "resource", "action");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_WithAdminRole_ReturnsTrue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "admin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authenticationService.AuthorizeAsync(principal, "resource", "action");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithSpecificPermission_ReturnsTrue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "user"),
            new Claim("permission", "resource:action")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authenticationService.AuthorizeAsync(principal, "resource", "action");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithWildcardResourcePermission_ReturnsTrue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "user"),
            new Claim("permission", "resource:*")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authenticationService.AuthorizeAsync(principal, "resource", "action");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithWildcardActionPermission_ReturnsTrue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "user"),
            new Claim("permission", "*:action")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authenticationService.AuthorizeAsync(principal, "resource", "action");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithFullWildcardPermission_ReturnsTrue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "user"),
            new Claim("permission", "*:*")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authenticationService.AuthorizeAsync(principal, "resource", "action");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithoutPermission_ReturnsFalse()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "user"),
            new Claim("permission", "other:permission")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authenticationService.AuthorizeAsync(principal, "resource", "action");

        // Assert
        result.Should().BeFalse();
    }
}