using System.Security.Claims;

namespace McpServer.Domain.Security;

/// <summary>
/// Represents a user in the system.
/// </summary>
public class User
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user's avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the user's bio.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Gets or sets the user's location.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the user's website.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the user's preferences.
    /// </summary>
    public UserPreferences? Preferences { get; set; }

    /// <summary>
    /// Gets or sets whether the email is verified.
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// Gets or sets whether the user is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the date the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date the user was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the date the user last logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Gets or sets the user's roles.
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Gets or sets the user's permissions.
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Gets or sets the user's external login providers.
    /// </summary>
    public List<ExternalLogin> ExternalLogins { get; set; } = new();

    /// <summary>
    /// Gets or sets custom claims for the user.
    /// </summary>
    public Dictionary<string, string> CustomClaims { get; set; } = new();

    /// <summary>
    /// Converts the user to a ClaimsPrincipal.
    /// </summary>
    /// <returns>A ClaimsPrincipal representing the user.</returns>
    public ClaimsPrincipal ToClaimsPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Id),
            new(ClaimTypes.Email, Email),
            new("email_verified", EmailVerified.ToString())
        };

        if (!string.IsNullOrEmpty(Username))
            claims.Add(new Claim(ClaimTypes.Name, Username));

        if (!string.IsNullOrEmpty(DisplayName))
            claims.Add(new Claim("display_name", DisplayName));

        if (!string.IsNullOrEmpty(AvatarUrl))
            claims.Add(new Claim("avatar_url", AvatarUrl));

        foreach (var role in Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var permission in Permissions)
            claims.Add(new Claim("permission", permission));

        foreach (var (key, value) in CustomClaims)
            claims.Add(new Claim(key, value));

        var identity = new ClaimsIdentity(claims, "User");
        return new ClaimsPrincipal(identity);
    }
}

/// <summary>
/// Represents an external login provider association.
/// </summary>
public class ExternalLogin
{
    /// <summary>
    /// Gets or sets the provider name (e.g., "Google", "Microsoft", "GitHub").
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// Gets or sets the provider user ID.
    /// </summary>
    public required string ProviderUserId { get; set; }

    /// <summary>
    /// Gets or sets the provider display name.
    /// </summary>
    public string? ProviderDisplayName { get; set; }

    /// <summary>
    /// Gets or sets when this login was linked.
    /// </summary>
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets additional provider data.
    /// </summary>
    public Dictionary<string, string> ProviderData { get; set; } = new();
}