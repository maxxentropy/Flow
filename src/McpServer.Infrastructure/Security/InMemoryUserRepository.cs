using System.Collections.Concurrent;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Security;

/// <summary>
/// In-memory implementation of user repository for development/testing.
/// </summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly ILogger<InMemoryUserRepository> _logger;
    private readonly ConcurrentDictionary<string, User> _users = new();
    private readonly ConcurrentDictionary<string, string> _emailToId = new();
    private readonly ConcurrentDictionary<string, string> _externalLoginToId = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryUserRepository"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public InMemoryUserRepository(ILogger<InMemoryUserRepository> logger)
    {
        _logger = logger;
        SeedDefaultUsers();
    }

    private void SeedDefaultUsers()
    {
        // Add a default admin user
        var adminUser = new User
        {
            Id = "admin",
            Username = "admin",
            Email = "admin@localhost",
            DisplayName = "Administrator",
            EmailVerified = true,
            Roles = new List<string> { "admin", "user" },
            Permissions = new List<string> { "*:*" } // All permissions
        };
        CreateAsync(adminUser).Wait();

        // Add a default test user
        var testUser = new User
        {
            Id = "test-user",
            Username = "testuser",
            Email = "test@localhost",
            DisplayName = "Test User",
            EmailVerified = true,
            Roles = new List<string> { "user" },
            Permissions = new List<string> { "tools:execute", "resources:read" }
        };
        CreateAsync(testUser).Wait();
    }

    /// <inheritdoc/>
    public Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        _users.TryGetValue(userId, out var user);
        return Task.FromResult(CloneUser(user));
    }

    /// <inheritdoc/>
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (_emailToId.TryGetValue(email.ToLowerInvariant(), out var userId))
        {
            _users.TryGetValue(userId, out var user);
            return Task.FromResult(CloneUser(user));
        }
        return Task.FromResult<User?>(null);
    }

    /// <inheritdoc/>
    public Task<User?> GetByExternalLoginAsync(string provider, string providerUserId, CancellationToken cancellationToken = default)
    {
        var key = $"{provider}:{providerUserId}";
        if (_externalLoginToId.TryGetValue(key, out var userId))
        {
            _users.TryGetValue(userId, out var user);
            return Task.FromResult(CloneUser(user));
        }
        return Task.FromResult<User?>(null);
    }

    /// <inheritdoc/>
    public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(user.Id))
        {
            user.Id = Guid.NewGuid().ToString();
        }

        user.CreatedAt = DateTime.UtcNow;
        
        if (!_users.TryAdd(user.Id, user))
        {
            throw new InvalidOperationException($"User with ID {user.Id} already exists");
        }

        _emailToId.TryAdd(user.Email.ToLowerInvariant(), user.Id);

        foreach (var login in user.ExternalLogins)
        {
            var key = $"{login.Provider}:{login.ProviderUserId}";
            _externalLoginToId.TryAdd(key, user.Id);
        }

        _logger.LogInformation("Created user {UserId} with email {Email}", user.Id, user.Email);
        return Task.FromResult(CloneUser(user)!);
    }

    /// <inheritdoc/>
    public Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(user.Id, out var existingUser))
        {
            throw new InvalidOperationException($"User with ID {user.Id} not found");
        }

        // Update email index if email changed
        if (existingUser.Email != user.Email)
        {
            _emailToId.TryRemove(existingUser.Email.ToLowerInvariant(), out _);
            _emailToId.TryAdd(user.Email.ToLowerInvariant(), user.Id);
        }

        // Update external login indices
        var existingLogins = existingUser.ExternalLogins.Select(l => $"{l.Provider}:{l.ProviderUserId}").ToHashSet();
        var newLogins = user.ExternalLogins.Select(l => $"{l.Provider}:{l.ProviderUserId}").ToHashSet();

        foreach (var removed in existingLogins.Except(newLogins))
        {
            _externalLoginToId.TryRemove(removed, out _);
        }

        foreach (var added in newLogins.Except(existingLogins))
        {
            _externalLoginToId.TryAdd(added, user.Id);
        }

        user.UpdatedAt = DateTime.UtcNow;
        _users[user.Id] = user;

        _logger.LogInformation("Updated user {UserId}", user.Id);
        return Task.FromResult(CloneUser(user)!);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_users.TryRemove(userId, out var user))
        {
            _emailToId.TryRemove(user.Email.ToLowerInvariant(), out _);
            
            foreach (var login in user.ExternalLogins)
            {
                var key = $"{login.Provider}:{login.ProviderUserId}";
                _externalLoginToId.TryRemove(key, out _);
            }

            _logger.LogInformation("Deleted user {UserId}", userId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public async Task<bool> LinkExternalLoginAsync(string userId, ExternalLogin externalLogin, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        // Check if this external login is already linked to another user
        var existingUser = await GetByExternalLoginAsync(externalLogin.Provider, externalLogin.ProviderUserId, cancellationToken);
        if (existingUser != null && existingUser.Id != userId)
        {
            _logger.LogWarning("External login {Provider}:{ProviderId} is already linked to user {ExistingUserId}", 
                externalLogin.Provider, externalLogin.ProviderUserId, existingUser.Id);
            return false;
        }

        // Add the external login
        user.ExternalLogins.Add(externalLogin);
        await UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Linked external login {Provider}:{ProviderId} to user {UserId}", 
            externalLogin.Provider, externalLogin.ProviderUserId, userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> UnlinkExternalLoginAsync(string userId, string provider, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        var loginToRemove = user.ExternalLogins.FirstOrDefault(l => l.Provider == provider);
        if (loginToRemove == null)
        {
            return false;
        }

        user.ExternalLogins.Remove(loginToRemove);
        await UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Unlinked external login {Provider} from user {UserId}", provider, userId);
        return true;
    }

    private static User? CloneUser(User? user)
    {
        if (user == null) return null;
        
        return new User
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Bio = user.Bio,
            Location = user.Location,
            Website = user.Website,
            Preferences = user.Preferences,
            EmailVerified = user.EmailVerified,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginAt = user.LastLoginAt,
            Roles = new List<string>(user.Roles),
            Permissions = new List<string>(user.Permissions),
            ExternalLogins = user.ExternalLogins.Select(el => new ExternalLogin
            {
                Provider = el.Provider,
                ProviderUserId = el.ProviderUserId,
                ProviderDisplayName = el.ProviderDisplayName,
                LinkedAt = el.LinkedAt,
                ProviderData = new Dictionary<string, string>(el.ProviderData)
            }).ToList(),
            CustomClaims = new Dictionary<string, string>(user.CustomClaims)
        };
    }
}