using System.Text.RegularExpressions;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Service for managing user profiles and claims.
/// </summary>
public class UserProfileService : IUserProfileService
{
    private readonly ILogger<UserProfileService> _logger;
    private readonly IUserRepository _userRepository;
    private readonly List<ProfileAuditEntry> _auditTrail = new();
    private readonly object _auditLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="UserProfileService"/> class.
    /// </summary>
    public UserProfileService(
        ILogger<UserProfileService> logger,
        IUserRepository userRepository)
    {
        _logger = logger;
        _userRepository = userRepository;
    }

    /// <inheritdoc/>
    public async Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return null;
        }

        return new UserProfile
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Bio = user.Bio,
            Location = user.Location,
            Website = user.Website,
            Roles = new List<string>(user.Roles),
            Permissions = new List<string>(user.Permissions),
            CustomClaims = new Dictionary<string, string>(user.CustomClaims),
            Preferences = user.Preferences ?? new UserPreferences(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    /// <inheritdoc/>
    public async Task<UserProfile> UpdateProfileAsync(string userId, ProfileUpdateRequest updates, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }

        var oldProfile = await GetProfileAsync(userId, cancellationToken);

        if (updates.DisplayName != null && updates.DisplayName != user.DisplayName)
        {
            AddAuditEntry(userId, "UpdateProfile", "DisplayName", user.DisplayName, updates.DisplayName, userId);
            user.DisplayName = updates.DisplayName;
        }

        if (updates.Bio != null && updates.Bio != user.Bio)
        {
            AddAuditEntry(userId, "UpdateProfile", "Bio", user.Bio, updates.Bio, userId);
            user.Bio = updates.Bio;
        }

        if (updates.Location != null && updates.Location != user.Location)
        {
            AddAuditEntry(userId, "UpdateProfile", "Location", user.Location, updates.Location, userId);
            user.Location = updates.Location;
        }

        if (updates.Website != null && updates.Website != user.Website)
        {
            AddAuditEntry(userId, "UpdateProfile", "Website", user.Website, updates.Website, userId);
            user.Website = updates.Website;
        }

        if (updates.Preferences != null)
        {
            AddAuditEntry(userId, "UpdateProfile", "Preferences", null, null, userId);
            user.Preferences = updates.Preferences;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Updated profile for user {UserId}", userId);

        return (await GetProfileAsync(userId, cancellationToken))!;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAvatarAsync(string userId, string avatarUrl, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        AddAuditEntry(userId, "UpdateAvatar", "AvatarUrl", user.AvatarUrl, avatarUrl, userId);
        
        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Updated avatar for user {UserId}", userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> AddClaimAsync(string userId, string claimType, string claimValue, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        if (user.CustomClaims.TryGetValue(claimType, out var existingValue) && existingValue == claimValue)
        {
            return false;
        }

        var oldValue = user.CustomClaims.TryGetValue(claimType, out var existing) ? existing : null;
        user.CustomClaims[claimType] = claimValue;
        user.UpdatedAt = DateTime.UtcNow;

        AddAuditEntry(userId, "AddClaim", claimType, oldValue, claimValue, userId);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Added claim {ClaimType}={ClaimValue} for user {UserId}", claimType, claimValue, userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveClaimAsync(string userId, string claimType, string? claimValue = null, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        if (!user.CustomClaims.TryGetValue(claimType, out var oldValue))
        {
            return false;
        }

        if (claimValue != null && oldValue != claimValue)
        {
            return false;
        }
        user.CustomClaims.Remove(claimType);
        user.UpdatedAt = DateTime.UtcNow;

        AddAuditEntry(userId, "RemoveClaim", claimType, oldValue, null, userId);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Removed claim {ClaimType} for user {UserId}", claimType, userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetClaimsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return new Dictionary<string, string>();
        }

        return new Dictionary<string, string>(user.CustomClaims);
    }

    /// <inheritdoc/>
    public async Task<bool> AddRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        if (user.Roles.Contains(role))
        {
            return false;
        }

        user.Roles.Add(role);
        user.UpdatedAt = DateTime.UtcNow;

        AddAuditEntry(userId, "AddRole", "Roles", null, role, userId);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Added role {Role} to user {UserId}", role, userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        if (!user.Roles.Contains(role))
        {
            return false;
        }

        user.Roles.Remove(role);
        user.UpdatedAt = DateTime.UtcNow;

        AddAuditEntry(userId, "RemoveRole", "Roles", role, null, userId);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Removed role {Role} from user {UserId}", role, userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> AddPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        if (user.Permissions.Contains(permission))
        {
            return false;
        }

        user.Permissions.Add(permission);
        user.UpdatedAt = DateTime.UtcNow;

        AddAuditEntry(userId, "AddPermission", "Permissions", null, permission, userId);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Added permission {Permission} to user {UserId}", permission, userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RemovePermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        if (!user.Permissions.Contains(permission))
        {
            return false;
        }

        user.Permissions.Remove(permission);
        user.UpdatedAt = DateTime.UtcNow;

        AddAuditEntry(userId, "RemovePermission", "Permissions", permission, null, userId);

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Removed permission {Permission} from user {UserId}", permission, userId);
        return true;
    }

    /// <inheritdoc/>
    public ProfileValidationResult ValidateProfile(UserProfile profile)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.Email))
        {
            errors.Add("Email is required");
        }
        else if (!IsValidEmail(profile.Email))
        {
            errors.Add("Email format is invalid");
        }

        if (!string.IsNullOrWhiteSpace(profile.Username))
        {
            if (profile.Username.Length < 3)
            {
                errors.Add("Username must be at least 3 characters long");
            }
            else if (profile.Username.Length > 50)
            {
                errors.Add("Username must be 50 characters or less");
            }
            else if (!IsValidUsername(profile.Username))
            {
                errors.Add("Username can only contain letters, numbers, hyphens, and underscores");
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.Bio) && profile.Bio.Length > 500)
        {
            errors.Add("Bio must be 500 characters or less");
        }

        if (!string.IsNullOrWhiteSpace(profile.Website) && !IsValidUrl(profile.Website))
        {
            errors.Add("Website URL is invalid");
        }

        if (!string.IsNullOrWhiteSpace(profile.DisplayName) && profile.DisplayName.Length > 100)
        {
            errors.Add("Display name must be 100 characters or less");
        }

        return errors.Count == 0 
            ? ProfileValidationResult.Success() 
            : ProfileValidationResult.Failure(errors.ToArray());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ProfileAuditEntry>> GetAuditTrailAsync(string userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        lock (_auditLock)
        {
            var entries = _auditTrail
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.Timestamp)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<ProfileAuditEntry>>(entries);
        }
    }

    private void AddAuditEntry(string userId, string action, string? field, string? oldValue, string? newValue, string? performedBy)
    {
        lock (_auditLock)
        {
            var entry = new ProfileAuditEntry
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Action = action,
                Field = field,
                OldValue = oldValue,
                NewValue = newValue,
                PerformedBy = performedBy,
                Timestamp = DateTime.UtcNow
            };

            _auditTrail.Add(entry);

            // Keep only last 1000 entries per user
            var userEntries = _auditTrail.Where(e => e.UserId == userId).OrderByDescending(e => e.Timestamp).ToList();
            if (userEntries.Count > 1000)
            {
                foreach (var oldEntry in userEntries.Skip(1000))
                {
                    _auditTrail.Remove(oldEntry);
                }
            }
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidUsername(string username)
    {
        return Regex.IsMatch(username, @"^[a-zA-Z0-9_-]+$");
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}