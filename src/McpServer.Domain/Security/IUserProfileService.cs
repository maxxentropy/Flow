namespace McpServer.Domain.Security;

/// <summary>
/// Service for managing user profiles and claims.
/// </summary>
public interface IUserProfileService
{
    /// <summary>
    /// Gets a user profile by ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user profile.</returns>
    Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user's profile.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="updates">The profile updates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated profile.</returns>
    Task<UserProfile> UpdateProfileAsync(string userId, ProfileUpdateRequest updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user's avatar.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="avatarUrl">The avatar URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if updated.</returns>
    Task<bool> UpdateAvatarAsync(string userId, string avatarUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a custom claim to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="claimType">The claim type.</param>
    /// <param name="claimValue">The claim value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if added.</returns>
    Task<bool> AddClaimAsync(string userId, string claimType, string claimValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a custom claim from a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="claimType">The claim type.</param>
    /// <param name="claimValue">The claim value (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if removed.</returns>
    Task<bool> RemoveClaimAsync(string userId, string claimType, string? claimValue = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all custom claims for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user's custom claims.</returns>
    Task<IReadOnlyDictionary<string, string>> GetClaimsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a role to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="role">The role name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if added.</returns>
    Task<bool> AddRoleAsync(string userId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="role">The role name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if removed.</returns>
    Task<bool> RemoveRoleAsync(string userId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a permission to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if added.</returns>
    Task<bool> AddPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a permission from a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if removed.</returns>
    Task<bool> RemovePermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates profile data.
    /// </summary>
    /// <param name="profile">The profile to validate.</param>
    /// <returns>Validation result.</returns>
    ProfileValidationResult ValidateProfile(UserProfile profile);

    /// <summary>
    /// Gets the audit trail for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="limit">Maximum number of entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Audit trail entries.</returns>
    Task<IReadOnlyList<ProfileAuditEntry>> GetAuditTrailAsync(string userId, int limit = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// User profile information.
/// </summary>
public class UserProfile
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the bio.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Gets or sets the location.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the website.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the roles.
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Gets or sets the permissions.
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Gets or sets custom claims.
    /// </summary>
    public Dictionary<string, string> CustomClaims { get; set; } = new();

    /// <summary>
    /// Gets or sets preferences.
    /// </summary>
    public UserPreferences Preferences { get; set; } = new();

    /// <summary>
    /// Gets or sets when the profile was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the profile was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// User preferences.
/// </summary>
public class UserPreferences
{
    /// <summary>
    /// Gets or sets the preferred language.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Gets or sets the timezone.
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Gets or sets the theme.
    /// </summary>
    public string Theme { get; set; } = "light";

    /// <summary>
    /// Gets or sets notification preferences.
    /// </summary>
    public NotificationPreferences Notifications { get; set; } = new();

    /// <summary>
    /// Gets or sets custom preferences.
    /// </summary>
    public Dictionary<string, object> Custom { get; set; } = new();
}

/// <summary>
/// Notification preferences.
/// </summary>
public class NotificationPreferences
{
    /// <summary>
    /// Gets or sets whether email notifications are enabled.
    /// </summary>
    public bool Email { get; set; } = true;

    /// <summary>
    /// Gets or sets whether push notifications are enabled.
    /// </summary>
    public bool Push { get; set; } = true;

    /// <summary>
    /// Gets or sets whether security alerts are enabled.
    /// </summary>
    public bool SecurityAlerts { get; set; } = true;
}

/// <summary>
/// Profile update request.
/// </summary>
public class ProfileUpdateRequest
{
    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the bio.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Gets or sets the location.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the website.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets preference updates.
    /// </summary>
    public UserPreferences? Preferences { get; set; }
}

/// <summary>
/// Profile validation result.
/// </summary>
public class ProfileValidationResult
{
    /// <summary>
    /// Gets or sets whether the profile is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Creates a success result.
    /// </summary>
    public static ProfileValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static ProfileValidationResult Failure(params string[] errors) => new() 
    { 
        IsValid = false, 
        Errors = errors.ToList() 
    };
}

/// <summary>
/// Profile audit entry.
/// </summary>
public class ProfileAuditEntry
{
    /// <summary>
    /// Gets or sets the entry ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Gets or sets the action performed.
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Gets or sets the field changed.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Gets or sets the old value.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Gets or sets the new value.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Gets or sets who performed the action.
    /// </summary>
    public string? PerformedBy { get; set; }

    /// <summary>
    /// Gets or sets when the action was performed.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}