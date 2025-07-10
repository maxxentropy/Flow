using System.Security.Cryptography;
using System.Text;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Application.Services;

/// <summary>
/// Service for managing user sessions.
/// </summary>
public class SessionService : ISessionService
{
    private readonly ILogger<SessionService> _logger;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOptions<SessionOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionService"/> class.
    /// </summary>
    public SessionService(
        ILogger<SessionService> logger,
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IOptions<SessionOptions> options)
    {
        _logger = logger;
        _sessionRepository = sessionRepository;
        _userRepository = userRepository;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<Session> CreateSessionAsync(
        string userId, 
        string authMethod, 
        string? authProvider = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        // Verify user exists and is active
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            throw new InvalidOperationException("User not found or inactive");
        }

        // Enforce session limits
        if (_options.Value.EnforceSessionLimits)
        {
            await EnforceSessionLimitAsync(userId, _options.Value.MaxSessionsPerUser - 1, cancellationToken);
        }

        // Create session
        var session = new Session
        {
            Id = GenerateSessionId(),
            UserId = userId,
            Token = GenerateToken(),
            RefreshToken = GenerateToken(),
            AuthenticationMethod = authMethod,
            AuthenticationProvider = authProvider,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_options.Value.SessionTimeout),
            RefreshTokenExpiresAt = DateTime.UtcNow.Add(_options.Value.RefreshTokenTimeout),
            LastActivityAt = DateTime.UtcNow,
            IsActive = true,
            Metadata = metadata ?? new Dictionary<string, string>()
        };

        var created = await _sessionRepository.CreateAsync(session, cancellationToken);
        
        _logger.LogInformation("Created session {SessionId} for user {UserId} using {AuthMethod}", 
            created.Id, userId, authMethod);

        return created;
    }

    /// <inheritdoc/>
    public async Task<Session?> ValidateSessionAsync(
        string token, 
        bool updateActivity = true,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByTokenAsync(token, cancellationToken);
        
        if (session == null)
        {
            _logger.LogDebug("Session not found for token");
            return null;
        }

        if (!session.IsValid())
        {
            _logger.LogDebug("Session {SessionId} is invalid or expired", session.Id);
            return null;
        }

        // Verify user is still active
        var user = await _userRepository.GetByIdAsync(session.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Session {SessionId} belongs to inactive user {UserId}", session.Id, session.UserId);
            await RevokeSessionAsync(session.Id, "User inactive", cancellationToken);
            return null;
        }

        // Update activity
        if (updateActivity && _options.Value.TrackActivity)
        {
            await _sessionRepository.UpdateActivityAsync(session.Id, cancellationToken);
            
            // Extend expiration if using sliding expiration
            if (_options.Value.SlidingExpiration > TimeSpan.Zero)
            {
                var newExpiry = DateTime.UtcNow.Add(_options.Value.SlidingExpiration);
                if (newExpiry > session.ExpiresAt)
                {
                    session.ExpiresAt = newExpiry;
                    await _sessionRepository.UpdateAsync(session, cancellationToken);
                }
            }
        }

        return session;
    }

    /// <inheritdoc/>
    public async Task<Session?> RefreshSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByRefreshTokenAsync(refreshToken, cancellationToken);
        
        if (session == null)
        {
            _logger.LogDebug("Session not found for refresh token");
            return null;
        }

        if (!session.CanRefresh())
        {
            _logger.LogDebug("Session {SessionId} cannot be refreshed", session.Id);
            return null;
        }

        // Verify user is still active
        var user = await _userRepository.GetByIdAsync(session.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Cannot refresh session {SessionId} for inactive user {UserId}", session.Id, session.UserId);
            await RevokeSessionAsync(session.Id, "User inactive", cancellationToken);
            return null;
        }

        // Generate new tokens
        session.Token = GenerateToken();
        session.RefreshToken = GenerateToken();
        session.ExpiresAt = DateTime.UtcNow.Add(_options.Value.SessionTimeout);
        session.RefreshTokenExpiresAt = DateTime.UtcNow.Add(_options.Value.RefreshTokenTimeout);
        session.LastActivityAt = DateTime.UtcNow;

        var updated = await _sessionRepository.UpdateAsync(session, cancellationToken);
        
        _logger.LogInformation("Refreshed session {SessionId} for user {UserId}", session.Id, session.UserId);

        return updated;
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeSessionAsync(
        string sessionId, 
        string reason,
        CancellationToken cancellationToken = default)
    {
        var result = await _sessionRepository.RevokeAsync(sessionId, reason, cancellationToken);
        
        if (result)
        {
            _logger.LogInformation("Revoked session {SessionId}: {Reason}", sessionId, reason);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<int> RevokeAllUserSessionsAsync(
        string userId, 
        string reason,
        string? exceptSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var count = await _sessionRepository.RevokeAllForUserAsync(userId, reason, exceptSessionId, cancellationToken);
        
        _logger.LogInformation("Revoked {Count} sessions for user {UserId}: {Reason}", count, userId, reason);

        return count;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Session>> GetActiveSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _sessionRepository.GetActiveSessionsByUserIdAsync(userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var count = await _sessionRepository.DeleteExpiredAsync(cancellationToken);
        
        if (count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired sessions", count);
        }

        return count;
    }

    /// <inheritdoc/>
    public async Task<int> EnforceSessionLimitAsync(
        string userId, 
        int maxSessions,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.GetActiveSessionsByUserIdAsync(userId, cancellationToken);
        
        if (sessions.Count <= maxSessions)
        {
            return 0;
        }

        // Revoke oldest sessions
        var toRevoke = sessions
            .OrderBy(s => s.LastActivityAt)
            .Take(sessions.Count - maxSessions)
            .ToList();

        var count = 0;
        foreach (var session in toRevoke)
        {
            if (await RevokeSessionAsync(session.Id, "Session limit exceeded", cancellationToken))
            {
                count++;
            }
        }

        _logger.LogInformation("Enforced session limit for user {UserId}, revoked {Count} sessions", userId, count);

        return count;
    }

    private static string GenerateSessionId()
    {
        return $"sess_{Guid.NewGuid():N}";
    }

    private string GenerateToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        
        // Add signature for integrity
        var token = Convert.ToBase64String(bytes);
        var signature = ComputeSignature(token);
        
        return $"{token}.{signature}";
    }

    private string ComputeSignature(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.Value.TokenSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
}