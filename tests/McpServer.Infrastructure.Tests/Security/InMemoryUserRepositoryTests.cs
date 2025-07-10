using System;
using System.Linq;
using System.Threading.Tasks;
using McpServer.Domain.Security;
using McpServer.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace McpServer.Infrastructure.Tests.Security;

public class InMemoryUserRepositoryTests
{
    private readonly InMemoryUserRepository _repository;

    public InMemoryUserRepositoryTests()
    {
        var logger = Mock.Of<ILogger<InMemoryUserRepository>>();
        _repository = new InMemoryUserRepository(logger);
    }

    [Fact]
    public async Task Should_Have_Default_Users()
    {
        // Act
        var admin = await _repository.GetByIdAsync("admin");
        var testUser = await _repository.GetByIdAsync("test-user");

        // Assert
        Assert.NotNull(admin);
        Assert.Equal("admin", admin.Username);
        Assert.Equal("admin@localhost", admin.Email);
        Assert.Contains("admin", admin.Roles);

        Assert.NotNull(testUser);
        Assert.Equal("testuser", testUser.Username);
        Assert.Equal("test@localhost", testUser.Email);
        Assert.Contains("user", testUser.Roles);
    }

    [Fact]
    public async Task CreateAsync_Should_Create_User()
    {
        // Arrange
        var user = new User
        {
            Id = "new-user",
            Email = "new@example.com",
            Username = "newuser",
            DisplayName = "New User"
        };

        // Act
        var created = await _repository.CreateAsync(user);
        var retrieved = await _repository.GetByIdAsync("new-user");

        // Assert
        Assert.NotNull(created);
        Assert.Equal("new-user", created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(user.Email, retrieved.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_Should_Find_User()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "test@example.com",
            Username = "emailtest"
        };
        await _repository.CreateAsync(user);

        // Act
        var found = await _repository.GetByEmailAsync("test@example.com");
        var notFound = await _repository.GetByEmailAsync("nonexistent@example.com");

        // Assert
        Assert.NotNull(found);
        Assert.Equal("test@example.com", found.Email);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task GetByExternalLoginAsync_Should_Find_User()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "oauth@example.com",
            ExternalLogins = new List<ExternalLogin>
            {
                new ExternalLogin
                {
                    Provider = "Google",
                    ProviderUserId = "google-123"
                }
            }
        };
        await _repository.CreateAsync(user);

        // Act
        var found = await _repository.GetByExternalLoginAsync("Google", "google-123");
        var notFound = await _repository.GetByExternalLoginAsync("GitHub", "github-456");

        // Assert
        Assert.NotNull(found);
        Assert.Equal("oauth@example.com", found.Email);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task UpdateAsync_Should_Update_User()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "update@example.com",
            Username = "updateuser"
        };
        await _repository.CreateAsync(user);

        // Act
        user.DisplayName = "Updated User";
        user.Email = "updated@example.com";
        var updated = await _repository.UpdateAsync(user);
        var retrieved = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Updated User", updated.DisplayName);
        Assert.NotNull(updated.UpdatedAt);
        Assert.NotNull(retrieved);
        Assert.Equal("updated@example.com", retrieved.Email);
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_User()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "delete@example.com",
            Username = "deleteuser"
        };
        await _repository.CreateAsync(user);

        // Act
        var deleted = await _repository.DeleteAsync(user.Id);
        var retrieved = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.True(deleted);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task LinkExternalLoginAsync_Should_Add_Login()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "link@example.com",
            Username = "linkuser"
        };
        await _repository.CreateAsync(user);

        var externalLogin = new ExternalLogin
        {
            Provider = "GitHub",
            ProviderUserId = "github-789"
        };

        // Act
        var linked = await _repository.LinkExternalLoginAsync(user.Id, externalLogin);
        var retrieved = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.True(linked);
        Assert.NotNull(retrieved);
        Assert.Single(retrieved.ExternalLogins);
        Assert.Equal("GitHub", retrieved.ExternalLogins[0].Provider);
        
        // Verify we can find by external login
        var foundByLogin = await _repository.GetByExternalLoginAsync("GitHub", "github-789");
        Assert.NotNull(foundByLogin);
        Assert.Equal(user.Id, foundByLogin.Id);
    }

    [Fact]
    public async Task UnlinkExternalLoginAsync_Should_Remove_Login()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "unlink@example.com",
            Username = "unlinkuser",
            ExternalLogins = new List<ExternalLogin>
            {
                new ExternalLogin
                {
                    Provider = "Microsoft",
                    ProviderUserId = "ms-123"
                }
            }
        };
        await _repository.CreateAsync(user);

        // Act
        var unlinked = await _repository.UnlinkExternalLoginAsync(user.Id, "Microsoft");
        var retrieved = await _repository.GetByIdAsync(user.Id);
        var foundByLogin = await _repository.GetByExternalLoginAsync("Microsoft", "ms-123");

        // Assert
        Assert.True(unlinked);
        Assert.NotNull(retrieved);
        Assert.Empty(retrieved.ExternalLogins);
        Assert.Null(foundByLogin);
    }
}