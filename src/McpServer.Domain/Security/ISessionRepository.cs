namespace McpServer.Domain.Security;

/// <summary>
/// Repository interface for session management.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Creates a new session.
    /// </summary>
    /// <param name="session">The session to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created session.</returns>
    Task<Session> CreateAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session if found, null otherwise.</returns>
    Task<Session?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by token.
    /// </summary>
    /// <param name="token">The session token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session if found, null otherwise.</returns>
    Task<Session?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session if found, null otherwise.</returns>
    Task<Session?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of active sessions.</returns>
    Task<IReadOnlyList<Session>> GetActiveSessionsByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a session.
    /// </summary>
    /// <param name="session">The session to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated session.</returns>
    Task<Session> UpdateAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last activity time for a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if updated, false otherwise.</returns>
    Task<bool> UpdateActivityAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="reason">The reason for revocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if revoked, false otherwise.</returns>
    Task<bool> RevokeAsync(string sessionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all sessions for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="reason">The reason for revocation.</param>
    /// <param name="exceptSessionId">Optional session ID to exclude from revocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Number of sessions revoked.</returns>
    Task<int> RevokeAllForUserAsync(string userId, string reason, string? exceptSessionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes expired sessions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Number of sessions deleted.</returns>
    Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets session statistics for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Session statistics.</returns>
    Task<SessionStatistics> GetStatisticsForUserAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Session statistics for a user.
/// </summary>
public class SessionStatistics
{
    /// <summary>
    /// Gets or sets the total number of active sessions.
    /// </summary>
    public int ActiveSessions { get; set; }

    /// <summary>
    /// Gets or sets the total number of sessions created in the last 30 days.
    /// </summary>
    public int SessionsLast30Days { get; set; }

    /// <summary>
    /// Gets or sets the devices/user agents used.
    /// </summary>
    public List<string> Devices { get; set; } = new();

    /// <summary>
    /// Gets or sets the locations (IP addresses) used.
    /// </summary>
    public List<string> Locations { get; set; } = new();
}