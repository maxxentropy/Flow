using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpServer.Application.Services;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Application.Tests.Services;

public class UserProfileServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly UserProfileService _profileService;

    public UserProfileServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        var logger = Mock.Of<ILogger<UserProfileService>>();
        
        _profileService = new UserProfileService(logger, _userRepositoryMock.Object);
    }

    [Fact]
    public async Task GetProfileAsync_Should_Return_Profile_For_Existing_User()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            Username = "testuser",
            DisplayName = "Test User",
            AvatarUrl = "https://example.com/avatar.jpg",
            Bio = "Test bio",
            Location = "Test City",
            Website = "https://example.com",
            Roles = new List<string> { "user", "premium" },
            Permissions = new List<string> { "read", "write" },
            CustomClaims = new Dictionary<string, string> { { "department", "engineering" } },
            Preferences = new UserPreferences { Language = "en", Theme = "dark" },
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        // Act
        var profile = await _profileService.GetProfileAsync("user-123");

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(user.Id, profile.UserId);
        Assert.Equal(user.Email, profile.Email);
        Assert.Equal(user.Username, profile.Username);
        Assert.Equal(user.DisplayName, profile.DisplayName);
        Assert.Equal(user.AvatarUrl, profile.AvatarUrl);
        Assert.Equal(user.Bio, profile.Bio);
        Assert.Equal(user.Location, profile.Location);
        Assert.Equal(user.Website, profile.Website);
        Assert.Equal(2, profile.Roles.Count);
        Assert.Contains("user", profile.Roles);
        Assert.Contains("premium", profile.Roles);
        Assert.Equal(2, profile.Permissions.Count);
        Assert.Contains("read", profile.Permissions);
        Assert.Contains("write", profile.Permissions);
        Assert.Single(profile.CustomClaims);
        Assert.Equal("engineering", profile.CustomClaims["department"]);
    }

    [Fact]
    public async Task GetProfileAsync_Should_Return_Null_For_NonExistent_User()
    {
        // Arrange
        _userRepositoryMock.Setup(x => x.GetByIdAsync("nonexistent", default))
            .ReturnsAsync((User?)null);

        // Act
        var profile = await _profileService.GetProfileAsync("nonexistent");

        // Assert
        Assert.Null(profile);
    }

    [Fact]
    public async Task UpdateProfileAsync_Should_Update_User_Profile()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            DisplayName = "Old Name",
            Bio = "Old bio",
            Location = "Old City",
            Website = "https://old.com"
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, System.Threading.CancellationToken _) => u);

        var updates = new ProfileUpdateRequest
        {
            DisplayName = "New Name",
            Bio = "New bio",
            Location = "New City",
            Website = "https://new.com"
        };

        // Act
        var profile = await _profileService.UpdateProfileAsync("user-123", updates);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("New Name", profile.DisplayName);
        Assert.Equal("New bio", profile.Bio);
        Assert.Equal("New City", profile.Location);
        Assert.Equal("https://new.com", profile.Website);
        
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u => 
            u.DisplayName == "New Name" && 
            u.Bio == "New bio" &&
            u.Location == "New City" &&
            u.Website == "https://new.com" &&
            u.UpdatedAt != null), default), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_Should_Throw_For_NonExistent_User()
    {
        // Arrange
        _userRepositoryMock.Setup(x => x.GetByIdAsync("nonexistent", default))
            .ReturnsAsync((User?)null);

        var updates = new ProfileUpdateRequest { DisplayName = "New Name" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _profileService.UpdateProfileAsync("nonexistent", updates));
    }

    [Fact]
    public async Task AddClaimAsync_Should_Add_New_Claim()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            CustomClaims = new Dictionary<string, string>()
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, System.Threading.CancellationToken _) => u);

        // Act
        var result = await _profileService.AddClaimAsync("user-123", "department", "engineering");

        // Assert
        Assert.True(result);
        Assert.Single(user.CustomClaims);
        Assert.Equal("engineering", user.CustomClaims["department"]);
        
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u => 
            u.CustomClaims.ContainsKey("department") && 
            u.CustomClaims["department"] == "engineering"), default), Times.Once);
    }

    [Fact]
    public async Task AddClaimAsync_Should_Update_Existing_Claim()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            CustomClaims = new Dictionary<string, string> { { "department", "sales" } }
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, System.Threading.CancellationToken _) => u);

        // Act
        var result = await _profileService.AddClaimAsync("user-123", "department", "engineering");

        // Assert
        Assert.True(result);
        Assert.Single(user.CustomClaims);
        Assert.Equal("engineering", user.CustomClaims["department"]);
    }

    [Fact]
    public async Task RemoveClaimAsync_Should_Remove_Claim()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            CustomClaims = new Dictionary<string, string> { { "department", "engineering" } }
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, System.Threading.CancellationToken _) => u);

        // Act
        var result = await _profileService.RemoveClaimAsync("user-123", "department");

        // Assert
        Assert.True(result);
        Assert.Empty(user.CustomClaims);
        
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u => 
            !u.CustomClaims.ContainsKey("department")), default), Times.Once);
    }

    [Fact]
    public async Task AddRoleAsync_Should_Add_New_Role()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            Roles = new List<string> { "user" }
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, System.Threading.CancellationToken _) => u);

        // Act
        var result = await _profileService.AddRoleAsync("user-123", "admin");

        // Assert
        Assert.True(result);
        Assert.Equal(2, user.Roles.Count);
        Assert.Contains("admin", user.Roles);
    }

    [Fact]
    public async Task AddRoleAsync_Should_Return_False_For_Existing_Role()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            Roles = new List<string> { "user", "admin" }
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        // Act
        var result = await _profileService.AddRoleAsync("user-123", "admin");

        // Assert
        Assert.False(result);
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public void ValidateProfile_Should_Return_Success_For_Valid_Profile()
    {
        // Arrange
        var profile = new UserProfile
        {
            UserId = "user-123",
            Email = "test@example.com",
            Username = "testuser",
            DisplayName = "Test User",
            Bio = "A valid bio",
            Website = "https://example.com"
        };

        // Act
        var result = _profileService.ValidateProfile(profile);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateProfile_Should_Return_Errors_For_Invalid_Profile()
    {
        // Arrange
        var profile = new UserProfile
        {
            UserId = "user-123",
            Email = "invalid-email",
            Username = "ab",
            Bio = new string('a', 501),
            Website = "not-a-url"
        };

        // Act
        var result = _profileService.ValidateProfile(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Email format is invalid", result.Errors);
        Assert.Contains("Username must be at least 3 characters long", result.Errors);
        Assert.Contains("Bio must be 500 characters or less", result.Errors);
        Assert.Contains("Website URL is invalid", result.Errors);
    }

    [Fact]
    public async Task GetAuditTrailAsync_Should_Return_User_Audit_Entries()
    {
        // Arrange
        var user = new User
        {
            Id = "user-123",
            Email = "test@example.com",
            DisplayName = "Old Name"
        };

        _userRepositoryMock.Setup(x => x.GetByIdAsync("user-123", default))
            .ReturnsAsync(user);

        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, System.Threading.CancellationToken _) => u);

        // Create some audit entries
        await _profileService.UpdateProfileAsync("user-123", new ProfileUpdateRequest { DisplayName = "New Name" });
        await _profileService.AddClaimAsync("user-123", "test", "value");

        // Act
        var entries = await _profileService.GetAuditTrailAsync("user-123", 10);

        // Assert
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal("user-123", e.UserId));
        Assert.Contains(entries, e => e.Action == "UpdateProfile" && e.Field == "DisplayName");
        Assert.Contains(entries, e => e.Action == "AddClaim" && e.Field == "test");
    }
}