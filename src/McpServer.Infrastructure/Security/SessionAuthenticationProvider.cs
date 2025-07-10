using System.Security.Claims;
using McpServer.Application.Services;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Security;

/// <summary>
/// Authentication provider for session tokens.
/// </summary>
public class SessionAuthenticationProvider : IAuthenticationProvider
{
    private readonly ILogger<SessionAuthenticationProvider> _logger;
    private readonly ISessionService _sessionService;
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionAuthenticationProvider"/> class.
    /// </summary>
    public SessionAuthenticationProvider(
        ILogger<SessionAuthenticationProvider> logger,
        ISessionService sessionService,
        IUserRepository userRepository)
    {
        _logger = logger;
        _sessionService = sessionService;
        _userRepository = userRepository;
    }

    /// <inheritdoc/>
    public string Scheme => "Session";

    /// <inheritdoc/>
    public async Task<AuthenticationResult> AuthenticateAsync(string credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate session token
            var session = await _sessionService.ValidateSessionAsync(credentials, true, cancellationToken);
            
            if (session == null)
            {
                return AuthenticationResult.Failure("Invalid or expired session");
            }

            // Get user
            var user = await _userRepository.GetByIdAsync(session.UserId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Session {SessionId} for non-existent user: {UserId}", session.Id, session.UserId);
                await _sessionService.RevokeSessionAsync(session.Id, "User not found", cancellationToken);
                return AuthenticationResult.Failure("Invalid session");
            }

            // Create principal
            var principal = user.ToClaimsPrincipal();
            var identity = (ClaimsIdentity)principal.Identity!;
            identity.AddClaim(new Claim("auth_method", "session"));
            identity.AddClaim(new Claim("session_id", session.Id));
            
            if (!string.IsNullOrEmpty(session.AuthenticationProvider))
            {
                identity.AddClaim(new Claim("auth_provider", session.AuthenticationProvider));
            }

            _logger.LogDebug("Session authentication successful for user: {UserId}, session: {SessionId}", 
                user.Id, session.Id);
            
            return AuthenticationResult.Success(principal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session authentication failed");
            return AuthenticationResult.Failure("Session authentication failed");
        }
    }
}