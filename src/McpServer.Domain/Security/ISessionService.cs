namespace McpServer.Domain.Security;

/// <summary>
/// Service interface for session management.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new session for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="authMethod">The authentication method.</param>
    /// <param name="authProvider">The authentication provider (for OAuth).</param>
    /// <param name="metadata">Additional session metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created session.</returns>
    Task<Session> CreateSessionAsync(
        string userId, 
        string authMethod, 
        string? authProvider = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a session token.
    /// </summary>
    /// <param name="token">The session token.</param>
    /// <param name="updateActivity">Whether to update last activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session if valid, null otherwise.</returns>
    Task<Session?> ValidateSessionAsync(
        string token, 
        bool updateActivity = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes a session using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The refreshed session if successful, null otherwise.</returns>
    Task<Session?> RefreshSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="reason">The reason for revocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if revoked, false otherwise.</returns>
    Task<bool> RevokeSessionAsync(
        string sessionId, 
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="reason">The reason for revocation.</param>
    /// <param name="exceptSessionId">Optional session ID to exclude.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Number of sessions revoked.</returns>
    Task<int> RevokeAllUserSessionsAsync(
        string userId, 
        string reason,
        string? exceptSessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of active sessions.</returns>
    Task<IReadOnlyList<Session>> GetActiveSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired sessions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Number of sessions cleaned up.</returns>
    Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enforces session limits for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="maxSessions">Maximum allowed sessions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Number of sessions revoked.</returns>
    Task<int> EnforceSessionLimitAsync(
        string userId, 
        int maxSessions,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Session configuration options.
/// </summary>
public class SessionOptions
{
    /// <summary>
    /// Gets or sets the session timeout.
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the refresh token timeout.
    /// </summary>
    public TimeSpan RefreshTokenTimeout { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the sliding expiration window.
    /// </summary>
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the maximum sessions per user.
    /// </summary>
    public int MaxSessionsPerUser { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to enforce session limits.
    /// </summary>
    public bool EnforceSessionLimits { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to track activity.
    /// </summary>
    public bool TrackActivity { get; set; } = true;

    /// <summary>
    /// Gets or sets the token secret for session tokens.
    /// </summary>
    public string TokenSecret { get; set; } = "change-this-secret-in-production";
}