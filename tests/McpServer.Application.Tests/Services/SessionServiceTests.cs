using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpServer.Application.Services;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class SessionServiceTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly SessionService _sessionService;
    private readonly SessionOptions _options;

    public SessionServiceTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        
        _options = new SessionOptions
        {
            SessionTimeout = TimeSpan.FromHours(1),
            RefreshTokenTimeout = TimeSpan.FromDays(7),
            SlidingExpiration = TimeSpan.FromMinutes(30),
            MaxSessionsPerUser = 5,
            EnforceSessionLimits = true,
            TrackActivity = true,
            TokenSecret = "test-secret"
        };

        var logger = Mock.Of<ILogger<SessionService>>();
        var options = Options.Create(_options);
        
        _sessionService = new SessionService(
            logger,
            _sessionRepositoryMock.Object,
            _userRepositoryMock.Object,
            options);
    }

    [Fact]
    public async Task CreateSessionAsync_Should_Create_Session()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            IsActive = true
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        _sessionRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Session>(), default))
            .ReturnsAsync((Session s, System.Threading.CancellationToken _) => s);

        _sessionRepositoryMock.Setup(x => x.GetActiveSessionsByUserIdAsync("user-123", default))
            .ReturnsAsync(new List<Session>());

        // Act
        var session = await _sessionService.CreateSessionAsync("user-123", "oauth", "google");

        // Assert
        Assert.NotNull(session);
        Assert.Equal("user-123", session.UserId);
        Assert.Equal("oauth", session.AuthenticationMethod);
        Assert.Equal("google", session.AuthenticationProvider);
        Assert.True(session.IsActive);
        Assert.NotNull(session.Token);
        Assert.NotEmpty(session.Token);
        Assert.NotNull(session.RefreshToken);
        Assert.NotEmpty(session.RefreshToken);
        
        _sessionRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Session>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_Should_Fail_For_Inactive_User()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            IsActive = false
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sessionService.CreateSessionAsync("user-123", "oauth"));
    }

    [Fact]
    public async Task CreateSessionAsync_Should_Enforce_Session_Limits()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            IsActive = true
        };

        var existingSessions = Enumerable.Range(1, 5).Select(i => new Session
        {
            Id = $"session-{i}",
            UserId = "user-123",
            Token = $"token-{i}",
            AuthenticationMethod = "oauth",
            CreatedAt = DateTime.UtcNow.AddHours(-i),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-i * 10),
            IsActive = true
        }).ToList();

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        _sessionRepositoryMock.Setup(x => x.GetActiveSessionsByUserIdAsync("user-123", default))
            .ReturnsAsync(existingSessions);

        _sessionRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Session>(), default))
            .ReturnsAsync((Session s, System.Threading.CancellationToken _) => s);

        // Act
        var session = await _sessionService.CreateSessionAsync("user-123", "oauth");

        // Assert
        _sessionRepositoryMock.Verify(x => x.RevokeAsync("session-5", "Session limit exceeded", default), Times.Once);
    }

    [Fact]
    public async Task ValidateSessionAsync_Should_Return_Valid_Session()
    {
        // Arrange
        var session = new Session
        {
            Id = "session-123",
            UserId = "user-123",
            Token = "valid-token",
            AuthenticationMethod = "oauth",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
            IsActive = true
        };

        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            IsActive = true
        };

        _sessionRepositoryMock.Setup(x => x.GetByTokenAsync("valid-token", default))
            .ReturnsAsync(session);

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        // Act
        var result = await _sessionService.ValidateSessionAsync("valid-token");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("session-123", result.Id);
        
        _sessionRepositoryMock.Verify(x => x.UpdateActivityAsync("session-123", default), Times.Once);
    }

    [Fact]
    public async Task ValidateSessionAsync_Should_Return_Null_For_Expired_Session()
    {
        // Arrange
        var session = new Session
        {
            Id = "session-123",
            UserId = "user-123",
            Token = "expired-token",
            AuthenticationMethod = "oauth",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            LastActivityAt = DateTime.UtcNow.AddHours(-1),
            IsActive = true
        };

        _sessionRepositoryMock.Setup(x => x.GetByTokenAsync("expired-token", default))
            .ReturnsAsync(session);

        // Act
        var result = await _sessionService.ValidateSessionAsync("expired-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshSessionAsync_Should_Generate_New_Tokens()
    {
        // Arrange
        var session = new Session
        {
            Id = "session-123",
            UserId = "user-123",
            Token = "old-token",
            RefreshToken = "valid-refresh-token",
            AuthenticationMethod = "oauth",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-30),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(6),
            LastActivityAt = DateTime.UtcNow.AddHours(-1),
            IsActive = true
        };

        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            IsActive = true
        };

        _sessionRepositoryMock.Setup(x => x.GetByRefreshTokenAsync("valid-refresh-token", default))
            .ReturnsAsync(session);

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        _sessionRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Session>(), default))
            .ReturnsAsync((Session s, System.Threading.CancellationToken _) => s);

        // Act
        var result = await _sessionService.RefreshSessionAsync("valid-refresh-token");

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual("old-token", result.Token);
        Assert.NotEqual("valid-refresh-token", result.RefreshToken);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task EnforceSessionLimitAsync_Should_Revoke_Oldest_Sessions()
    {
        // Arrange
        var sessions = new List<Session>
        {
            new Session { Id = "s1", UserId = "u1", Token = "t1", AuthenticationMethod = "oauth", 
                LastActivityAt = DateTime.UtcNow.AddHours(-3), IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1) },
            new Session { Id = "s2", UserId = "u1", Token = "t2", AuthenticationMethod = "oauth", 
                LastActivityAt = DateTime.UtcNow.AddHours(-2), IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1) },
            new Session { Id = "s3", UserId = "u1", Token = "t3", AuthenticationMethod = "oauth", 
                LastActivityAt = DateTime.UtcNow.AddHours(-1), IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1) },
            new Session { Id = "s4", UserId = "u1", Token = "t4", AuthenticationMethod = "oauth", 
                LastActivityAt = DateTime.UtcNow.AddMinutes(-30), IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1) },
            new Session { Id = "s5", UserId = "u1", Token = "t5", AuthenticationMethod = "oauth", 
                LastActivityAt = DateTime.UtcNow.AddMinutes(-10), IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1) }
        };

        _sessionRepositoryMock.Setup(x => x.GetActiveSessionsByUserIdAsync("u1", default))
            .ReturnsAsync(sessions);

        _sessionRepositoryMock.Setup(x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        // Act
        var count = await _sessionService.EnforceSessionLimitAsync("u1", 3);

        // Assert
        Assert.Equal(2, count);
        _sessionRepositoryMock.Verify(x => x.RevokeAsync("s1", "Session limit exceeded", default), Times.Once);
        _sessionRepositoryMock.Verify(x => x.RevokeAsync("s2", "Session limit exceeded", default), Times.Once);
        _sessionRepositoryMock.Verify(x => x.RevokeAsync("s3", "Session limit exceeded", default), Times.Never);
    }
}