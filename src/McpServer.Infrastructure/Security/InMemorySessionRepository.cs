using System.Collections.Concurrent;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Security;

/// <summary>
/// In-memory implementation of session repository.
/// </summary>
public class InMemorySessionRepository : ISessionRepository
{
    private readonly ILogger<InMemorySessionRepository> _logger;
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _tokenToId = new();
    private readonly ConcurrentDictionary<string, string> _refreshTokenToId = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _userSessions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySessionRepository"/> class.
    /// </summary>
    public InMemorySessionRepository(ILogger<InMemorySessionRepository> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Session> CreateAsync(Session session, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryAdd(session.Id, session))
        {
            throw new InvalidOperationException($"Session with ID {session.Id} already exists");
        }

        _tokenToId.TryAdd(session.Token, session.Id);
        
        if (!string.IsNullOrEmpty(session.RefreshToken))
        {
            _refreshTokenToId.TryAdd(session.RefreshToken, session.Id);
        }

        // Add to user sessions
        _userSessions.AddOrUpdate(session.UserId,
            new HashSet<string> { session.Id },
            (_, existing) =>
            {
                existing.Add(session.Id);
                return existing;
            });

        _logger.LogInformation("Created session {SessionId} for user {UserId}", session.Id, session.UserId);
        return Task.FromResult(CloneSession(session)!);
    }

    /// <inheritdoc/>
    public Task<Session?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(CloneSession(session));
    }

    /// <inheritdoc/>
    public Task<Session?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (_tokenToId.TryGetValue(token, out var sessionId))
        {
            _sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult(CloneSession(session));
        }
        return Task.FromResult<Session?>(null);
    }

    /// <inheritdoc/>
    public Task<Session?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (_refreshTokenToId.TryGetValue(refreshToken, out var sessionId))
        {
            _sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult(CloneSession(session));
        }
        return Task.FromResult<Session?>(null);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Session>> GetActiveSessionsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var sessions = new List<Session>();
        
        if (_userSessions.TryGetValue(userId, out var sessionIds))
        {
            foreach (var sessionId in sessionIds)
            {
                if (_sessions.TryGetValue(sessionId, out var session) && session.IsActive)
                {
                    sessions.Add(CloneSession(session)!);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<Session>>(sessions);
    }

    /// <inheritdoc/>
    public Task<Session> UpdateAsync(Session session, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(session.Id, out var existingSession))
        {
            throw new InvalidOperationException($"Session with ID {session.Id} not found");
        }

        // Update token index if changed
        if (existingSession.Token != session.Token)
        {
            _tokenToId.TryRemove(existingSession.Token, out _);
            _tokenToId.TryAdd(session.Token, session.Id);
        }

        // Update refresh token index if changed
        if (existingSession.RefreshToken != session.RefreshToken)
        {
            if (!string.IsNullOrEmpty(existingSession.RefreshToken))
            {
                _refreshTokenToId.TryRemove(existingSession.RefreshToken, out _);
            }
            if (!string.IsNullOrEmpty(session.RefreshToken))
            {
                _refreshTokenToId.TryAdd(session.RefreshToken, session.Id);
            }
        }

        _sessions[session.Id] = session;
        _logger.LogDebug("Updated session {SessionId}", session.Id);
        return Task.FromResult(CloneSession(session)!);
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateActivityAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActivityAt = DateTime.UtcNow;
            await UpdateAsync(session, cancellationToken);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Revoke(reason);
            await UpdateAsync(session, cancellationToken);
            _logger.LogInformation("Revoked session {SessionId} for reason: {Reason}", sessionId, reason);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public async Task<int> RevokeAllForUserAsync(string userId, string reason, string? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        var count = 0;
        
        if (_userSessions.TryGetValue(userId, out var sessionIds))
        {
            foreach (var sessionId in sessionIds)
            {
                if (sessionId != exceptSessionId && _sessions.TryGetValue(sessionId, out var session) && session.IsActive)
                {
                    await RevokeAsync(sessionId, reason, cancellationToken);
                    count++;
                }
            }
        }

        _logger.LogInformation("Revoked {Count} sessions for user {UserId}", count, userId);
        return count;
    }

    /// <inheritdoc/>
    public Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        var now = DateTime.UtcNow;
        var toRemove = new List<string>();

        foreach (var kvp in _sessions)
        {
            var session = kvp.Value;
            // Delete if expired for more than 30 days or revoked for more than 7 days
            if ((session.ExpiresAt < now.AddDays(-30)) ||
                (session.RevokedAt.HasValue && session.RevokedAt.Value < now.AddDays(-7)))
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var sessionId in toRemove)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                _tokenToId.TryRemove(session.Token, out _);
                if (!string.IsNullOrEmpty(session.RefreshToken))
                {
                    _refreshTokenToId.TryRemove(session.RefreshToken, out _);
                }
                
                if (_userSessions.TryGetValue(session.UserId, out var userSessionIds))
                {
                    userSessionIds.Remove(sessionId);
                    if (userSessionIds.Count == 0)
                    {
                        _userSessions.TryRemove(session.UserId, out _);
                    }
                }
                
                count++;
            }
        }

        if (count > 0)
        {
            _logger.LogInformation("Deleted {Count} expired sessions", count);
        }

        return Task.FromResult(count);
    }

    /// <inheritdoc/>
    public Task<SessionStatistics> GetStatisticsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var stats = new SessionStatistics();
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var devices = new HashSet<string>();
        var locations = new HashSet<string>();

        if (_userSessions.TryGetValue(userId, out var sessionIds))
        {
            foreach (var sessionId in sessionIds)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    if (session.IsActive)
                    {
                        stats.ActiveSessions++;
                    }

                    if (session.CreatedAt >= thirtyDaysAgo)
                    {
                        stats.SessionsLast30Days++;
                    }

                    if (!string.IsNullOrEmpty(session.UserAgent))
                    {
                        devices.Add(session.UserAgent);
                    }

                    if (!string.IsNullOrEmpty(session.IpAddress))
                    {
                        locations.Add(session.IpAddress);
                    }
                }
            }
        }

        stats.Devices = devices.ToList();
        stats.Locations = locations.ToList();

        return Task.FromResult(stats);
    }

    private static Session? CloneSession(Session? session)
    {
        if (session == null) return null;

        return new Session
        {
            Id = session.Id,
            UserId = session.UserId,
            Token = session.Token,
            RefreshToken = session.RefreshToken,
            AuthenticationMethod = session.AuthenticationMethod,
            AuthenticationProvider = session.AuthenticationProvider,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
            RefreshTokenExpiresAt = session.RefreshTokenExpiresAt,
            LastActivityAt = session.LastActivityAt,
            IsActive = session.IsActive,
            RevocationReason = session.RevocationReason,
            RevokedAt = session.RevokedAt,
            Metadata = new Dictionary<string, string>(session.Metadata)
        };
    }
}