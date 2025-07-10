using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using McpServer.Domain.Security;
using McpServer.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Infrastructure.Tests.Security;

public class OAuthAuthenticationProviderTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IOAuthProvider> _googleProviderMock;
    private readonly OAuthAuthenticationProvider _provider;

    public OAuthAuthenticationProviderTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _googleProviderMock = new Mock<IOAuthProvider>();
        _googleProviderMock.Setup(x => x.Name).Returns("Google");

        var logger = Mock.Of<ILogger<OAuthAuthenticationProvider>>();
        _provider = new OAuthAuthenticationProvider(
            logger,
            _userRepositoryMock.Object,
            new[] { _googleProviderMock.Object });
    }

    [Fact]
    public void Scheme_Should_Be_OAuth()
    {
        Assert.Equal("OAuth", _provider.Scheme);
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Fail_With_Invalid_Format()
    {
        // Act
        var result = await _provider.AuthenticateAsync("invalid-format");

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Contains("Invalid OAuth token format", result.FailureReason);
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Fail_With_Unknown_Provider()
    {
        // Act
        var result = await _provider.AuthenticateAsync("Unknown:token123");

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Contains("Unknown OAuth provider", result.FailureReason);
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Create_New_User()
    {
        // Arrange
        var userInfo = new OAuthUserInfo
        {
            Id = "google-123",
            Email = "newuser@gmail.com",
            EmailVerified = true,
            Name = "New User",
            Picture = "https://example.com/photo.jpg"
        };

        _googleProviderMock.Setup(x => x.GetUserInfoAsync("token123", default))
            .ReturnsAsync(userInfo);

        _userRepositoryMock.Setup(x => x.GetByExternalLoginAsync("Google", "google-123", default))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(x => x.GetByEmailAsync("newuser@gmail.com", default))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, System.Threading.CancellationToken _) => u);

        // Act
        var result = await _provider.AuthenticateAsync("Google:token123");

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Principal);
        Assert.Equal("newuser@gmail.com", result.Principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("New User", result.Principal.FindFirst("display_name")?.Value);
        Assert.Equal("Google", result.Principal.FindFirst("oauth_provider")?.Value);

        _userRepositoryMock.Verify(x => x.CreateAsync(It.Is<User>(u => 
            u.Email == "newuser@gmail.com" &&
            u.DisplayName == "New User" &&
            u.AvatarUrl == "https://example.com/photo.jpg"), default), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Link_To_Existing_User_By_Email()
    {
        // Arrange
        var existingUser = new User
        {
            Id = "existing-user",
            Email = "existing@gmail.com",
            Username = "existinguser",
            Roles = new List<string> { "user", "premium" }
        };

        var userInfo = new OAuthUserInfo
        {
            Id = "google-456",
            Email = "existing@gmail.com",
            Name = "Existing User"
        };

        _googleProviderMock.Setup(x => x.GetUserInfoAsync("token456", default))
            .ReturnsAsync(userInfo);

        _userRepositoryMock.Setup(x => x.GetByExternalLoginAsync("Google", "google-456", default))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(x => x.GetByEmailAsync("existing@gmail.com", default))
            .ReturnsAsync(existingUser);
        _userRepositoryMock.Setup(x => x.LinkExternalLoginAsync(existingUser.Id, It.IsAny<ExternalLogin>(), default))
            .ReturnsAsync(true);
        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, System.Threading.CancellationToken _) => u);

        // Act
        var result = await _provider.AuthenticateAsync("Google:token456");

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Principal);
        Assert.Equal("existing-user", result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Contains("premium", result.Principal.FindAll(ClaimTypes.Role).Select(c => c.Value));

        _userRepositoryMock.Verify(x => x.LinkExternalLoginAsync(existingUser.Id, 
            It.Is<ExternalLogin>(el => el.Provider == "Google" && el.ProviderUserId == "google-456"), default), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Use_Existing_External_Login()
    {
        // Arrange
        var existingUser = new User
        {
            Id = "oauth-user",
            Email = "oauth@gmail.com",
            Username = "oauthuser",
            ExternalLogins = new List<ExternalLogin>
            {
                new ExternalLogin { Provider = "Google", ProviderUserId = "google-789" }
            }
        };

        var userInfo = new OAuthUserInfo
        {
            Id = "google-789",
            Email = "oauth@gmail.com",
            Name = "OAuth User"
        };

        _googleProviderMock.Setup(x => x.GetUserInfoAsync("token789", default))
            .ReturnsAsync(userInfo);

        _userRepositoryMock.Setup(x => x.GetByExternalLoginAsync("Google", "google-789", default))
            .ReturnsAsync(existingUser);
        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, System.Threading.CancellationToken _) => u);

        // Act
        var result = await _provider.AuthenticateAsync("Google:token789");

        // Assert
        Assert.True(result.IsAuthenticated);
        Assert.NotNull(result.Principal);
        Assert.Equal("oauth-user", result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u => u.LastLoginAt != null), default), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Handle_Provider_Errors()
    {
        // Arrange
        _googleProviderMock.Setup(x => x.GetUserInfoAsync("badtoken", default))
            .ThrowsAsync(new InvalidOperationException("Invalid token"));

        // Act
        var result = await _provider.AuthenticateAsync("Google:badtoken");

        // Assert
        Assert.False(result.IsAuthenticated);
        Assert.Equal("OAuth authentication failed", result.FailureReason);
    }
}