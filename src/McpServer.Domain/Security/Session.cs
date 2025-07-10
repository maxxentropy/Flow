using System.Security.Claims;

namespace McpServer.Domain.Security;

/// <summary>
/// Represents a user session.
/// </summary>
public class Session
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Gets or sets the session token.
    /// </summary>
    public required string Token { get; set; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the authentication method used.
    /// </summary>
    public required string AuthenticationMethod { get; set; }

    /// <summary>
    /// Gets or sets the authentication provider (for OAuth).
    /// </summary>
    public string? AuthenticationProvider { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the user agent.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets when the session was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the session expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when the refresh token expires.
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the last activity time.
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether the session is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the reason for revocation.
    /// </summary>
    public string? RevocationReason { get; set; }

    /// <summary>
    /// Gets or sets when the session was revoked.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Gets or sets additional session data.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Checks if the session is valid.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
    {
        return IsActive && DateTime.UtcNow < ExpiresAt;
    }

    /// <summary>
    /// Checks if the session can be refreshed.
    /// </summary>
    /// <returns>True if refreshable, false otherwise.</returns>
    public bool CanRefresh()
    {
        return !string.IsNullOrEmpty(RefreshToken) && 
               RefreshTokenExpiresAt.HasValue && 
               DateTime.UtcNow < RefreshTokenExpiresAt.Value;
    }

    /// <summary>
    /// Revokes the session.
    /// </summary>
    /// <param name="reason">The reason for revocation.</param>
    public void Revoke(string reason)
    {
        IsActive = false;
        RevocationReason = reason;
        RevokedAt = DateTime.UtcNow;
    }
}