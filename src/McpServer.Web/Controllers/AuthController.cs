using System.Security.Cryptography;
using System.Text;
using System.Web;
using McpServer.Domain.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace McpServer.Web.Controllers;

/// <summary>
/// Controller for OAuth authentication flow.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, IOAuthProvider> _oauthProviders;
    private readonly IUserRepository _userRepository;
    private readonly ISessionService _sessionService;
    private readonly Dictionary<string, OAuthState> _stateStore = new(); // In production, use distributed cache

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    public AuthController(
        ILogger<AuthController> logger,
        IConfiguration configuration,
        IEnumerable<IOAuthProvider> oauthProviders,
        IUserRepository userRepository,
        ISessionService sessionService)
    {
        _logger = logger;
        _configuration = configuration;
        _oauthProviders = oauthProviders.ToDictionary(p => p.Name.ToLowerInvariant());
        _userRepository = userRepository;
        _sessionService = sessionService;
    }

    /// <summary>
    /// Initiates OAuth login flow.
    /// </summary>
    /// <param name="provider">The OAuth provider name.</param>
    /// <param name="redirect_uri">The client's redirect URI.</param>
    /// <returns>Redirect to OAuth provider.</returns>
    [HttpGet("login/{provider}")]
    public IActionResult Login(string provider, [FromQuery] string? redirect_uri)
    {
        if (!_oauthProviders.TryGetValue(provider.ToLowerInvariant(), out var oauthProvider))
        {
            return BadRequest(new { error = "Unknown OAuth provider" });
        }

        // Generate state for CSRF protection
        var state = GenerateState();
        var oauthState = new OAuthState
        {
            Provider = provider,
            ClientRedirectUri = redirect_uri ?? _configuration["McpServer:OAuth:DefaultRedirectUri"] ?? "/",
            CreatedAt = DateTime.UtcNow
        };
        _stateStore[state] = oauthState;

        // Get callback URL
        var callbackUrl = $"{Request.Scheme}://{Request.Host}/auth/callback/{provider}";
        
        // Get authorization URL
        var authUrl = oauthProvider.GetAuthorizationUrl(state, callbackUrl);

        _logger.LogInformation("Initiating OAuth login for provider {Provider}", provider);
        return Redirect(authUrl);
    }

    /// <summary>
    /// Handles OAuth callback.
    /// </summary>
    /// <param name="provider">The OAuth provider name.</param>
    /// <param name="code">The authorization code.</param>
    /// <param name="state">The state parameter.</param>
    /// <param name="error">Error from provider.</param>
    /// <returns>Redirect to client with token or error.</returns>
    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(
        string provider, 
        [FromQuery] string? code, 
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        // Handle OAuth errors
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("OAuth error from {Provider}: {Error}", provider, error);
            return RedirectToClient("/", $"OAuth error: {error}");
        }

        // Validate state
        if (string.IsNullOrEmpty(state) || !_stateStore.TryGetValue(state, out var oauthState))
        {
            return RedirectToClient("/", "Invalid or expired state");
        }

        // Remove state to prevent reuse
        _stateStore.Remove(state);

        // Check state expiration (5 minutes)
        if (DateTime.UtcNow - oauthState.CreatedAt > TimeSpan.FromMinutes(5))
        {
            return RedirectToClient(oauthState.ClientRedirectUri, "State expired");
        }

        if (!_oauthProviders.TryGetValue(provider.ToLowerInvariant(), out var oauthProvider))
        {
            return RedirectToClient(oauthState.ClientRedirectUri, "Unknown provider");
        }

        if (string.IsNullOrEmpty(code))
        {
            return RedirectToClient(oauthState.ClientRedirectUri, "No authorization code received");
        }

        try
        {
            // Exchange code for token
            var callbackUrl = $"{Request.Scheme}://{Request.Host}/auth/callback/{provider}";
            var tokenResponse = await oauthProvider.ExchangeCodeForTokenAsync(code, callbackUrl);

            // Get user info
            var userInfo = await oauthProvider.GetUserInfoAsync(tokenResponse.AccessToken);

            // Create or update user
            var user = await CreateOrUpdateUserAsync(provider, userInfo, tokenResponse);

            // Create session
            var metadata = new Dictionary<string, string>
            {
                ["oauth_access_token"] = tokenResponse.AccessToken,
                ["ip_address"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                ["user_agent"] = Request.Headers["User-Agent"].FirstOrDefault() ?? "unknown"
            };
            
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                metadata["oauth_refresh_token"] = tokenResponse.RefreshToken;
            }

            var session = await _sessionService.CreateSessionAsync(
                user.Id, 
                "oauth", 
                provider,
                metadata);

            _logger.LogInformation("OAuth login successful for user {UserId} via {Provider}", user.Id, provider);

            // Redirect to client with token
            return RedirectToClient(oauthState.ClientRedirectUri, null, session.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth callback error for {Provider}", provider);
            return RedirectToClient(oauthState.ClientRedirectUri, "Authentication failed");
        }
    }

    /// <summary>
    /// Gets current user info.
    /// </summary>
    /// <returns>User information.</returns>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        // Extract token from Authorization header
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "No authorization header" });
        }

        var token = authHeader.Substring("Bearer ".Length);
        
        // Validate session
        var session = await _sessionService.ValidateSessionAsync(token);
        if (session == null)
        {
            return Unauthorized(new { error = "Invalid or expired session" });
        }

        var user = await _userRepository.GetByIdAsync(session.UserId);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            name = user.DisplayName,
            avatar = user.AvatarUrl,
            roles = user.Roles,
            externalLogins = user.ExternalLogins.Select(el => el.Provider)
        });
    }

    /// <summary>
    /// Logs out the user.
    /// </summary>
    /// <returns>Success response.</returns>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Extract session from Authorization header
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            var token = authHeader.Substring("Bearer ".Length);
            var session = await _sessionService.ValidateSessionAsync(token, false);
            
            if (session != null)
            {
                await _sessionService.RevokeSessionAsync(session.Id, "User logout");
            }
        }
        
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Gets active sessions for the current user.
    /// </summary>
    /// <returns>List of active sessions.</returns>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        // Extract session from Authorization header
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "No authorization header" });
        }

        var token = authHeader.Substring("Bearer ".Length);
        var session = await _sessionService.ValidateSessionAsync(token);
        
        if (session == null)
        {
            return Unauthorized(new { error = "Invalid or expired session" });
        }

        var sessions = await _sessionService.GetActiveSessionsAsync(session.UserId);
        
        return Ok(new
        {
            sessions = sessions.Select(s => new
            {
                id = s.Id,
                current = s.Token == token,
                createdAt = s.CreatedAt,
                lastActivityAt = s.LastActivityAt,
                expiresAt = s.ExpiresAt,
                authMethod = s.AuthenticationMethod,
                authProvider = s.AuthenticationProvider,
                ipAddress = s.IpAddress,
                userAgent = s.UserAgent
            })
        });
    }

    /// <summary>
    /// Revokes a specific session.
    /// </summary>
    /// <param name="sessionId">The session ID to revoke.</param>
    /// <returns>Success response.</returns>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        // Extract session from Authorization header
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "No authorization header" });
        }

        var token = authHeader.Substring("Bearer ".Length);
        var currentSession = await _sessionService.ValidateSessionAsync(token);
        
        if (currentSession == null)
        {
            return Unauthorized(new { error = "Invalid or expired session" });
        }

        // Verify the session belongs to the same user
        var targetSession = await _sessionService.GetActiveSessionsAsync(currentSession.UserId);
        if (!targetSession.Any(s => s.Id == sessionId))
        {
            return NotFound(new { error = "Session not found" });
        }

        var revoked = await _sessionService.RevokeSessionAsync(sessionId, "User requested");
        
        return Ok(new { revoked });
    }

    /// <summary>
    /// Revokes all other sessions.
    /// </summary>
    /// <returns>Number of sessions revoked.</returns>
    [HttpPost("sessions/revoke-others")]
    public async Task<IActionResult> RevokeOtherSessions()
    {
        // Extract session from Authorization header
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "No authorization header" });
        }

        var token = authHeader.Substring("Bearer ".Length);
        var session = await _sessionService.ValidateSessionAsync(token);
        
        if (session == null)
        {
            return Unauthorized(new { error = "Invalid or expired session" });
        }

        var count = await _sessionService.RevokeAllUserSessionsAsync(
            session.UserId, 
            "User requested logout from other devices",
            session.Id);
        
        return Ok(new { revokedCount = count });
    }

    private async Task<User> CreateOrUpdateUserAsync(string provider, OAuthUserInfo userInfo, OAuthTokenResponse tokenResponse)
    {
        // Try to find existing user
        var user = await _userRepository.GetByExternalLoginAsync(provider, userInfo.Id);

        if (user == null && !string.IsNullOrEmpty(userInfo.Email))
        {
            user = await _userRepository.GetByEmailAsync(userInfo.Email);
            
            if (user != null)
            {
                // Link external login
                var externalLogin = new ExternalLogin
                {
                    Provider = provider,
                    ProviderUserId = userInfo.Id,
                    ProviderDisplayName = userInfo.Name,
                    ProviderData = userInfo.AdditionalData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? string.Empty)
                };
                
                if (tokenResponse.RefreshToken != null)
                {
                    externalLogin.ProviderData["refresh_token"] = tokenResponse.RefreshToken;
                }

                await _userRepository.LinkExternalLoginAsync(user.Id, externalLogin);
            }
        }

        if (user == null)
        {
            // Create new user
            user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = userInfo.Email ?? $"{userInfo.Id}@{provider.ToLowerInvariant()}.oauth",
                EmailVerified = userInfo.EmailVerified ?? false,
                DisplayName = userInfo.Name ?? userInfo.Id,
                AvatarUrl = userInfo.Picture,
                Username = GenerateUsername(provider, userInfo),
                Roles = new List<string> { "user" },
                Permissions = new List<string> { "tools:execute", "resources:read" },
                ExternalLogins = new List<ExternalLogin>
                {
                    new ExternalLogin
                    {
                        Provider = provider,
                        ProviderUserId = userInfo.Id,
                        ProviderDisplayName = userInfo.Name,
                        ProviderData = userInfo.AdditionalData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? string.Empty)
                    }
                }
            };

            if (tokenResponse.RefreshToken != null)
            {
                user.ExternalLogins[0].ProviderData["refresh_token"] = tokenResponse.RefreshToken;
            }

            user = await _userRepository.CreateAsync(user);
        }
        else
        {
            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }

        return user;
    }

    private static string GenerateUsername(string provider, OAuthUserInfo userInfo)
    {
        if (provider.Equals("GitHub", StringComparison.OrdinalIgnoreCase) && 
            userInfo.AdditionalData.TryGetValue("login", out var githubLogin))
        {
            return githubLogin.ToString()!;
        }
        
        if (!string.IsNullOrEmpty(userInfo.Email))
        {
            return userInfo.Email.Split('@')[0];
        }

        return $"{provider.ToLowerInvariant()}_{userInfo.Id}";
    }

    private static string GenerateState()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }


    private RedirectResult RedirectToClient(string redirectUri, string? error = null, string? token = null)
    {
        var uri = new UriBuilder(redirectUri);
        var query = HttpUtility.ParseQueryString(uri.Query);

        if (!string.IsNullOrEmpty(error))
        {
            query["error"] = error;
        }
        else if (!string.IsNullOrEmpty(token))
        {
            query["token"] = token;
        }

        uri.Query = query.ToString();
        return Redirect(uri.ToString());
    }

    private class OAuthState
    {
        public required string Provider { get; set; }
        public required string ClientRedirectUri { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}