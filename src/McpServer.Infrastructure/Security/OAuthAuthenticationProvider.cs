using System.Security.Claims;
using System.Text.Json;
using McpServer.Application.Services;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Security;

/// <summary>
/// Authentication provider for OAuth tokens.
/// </summary>
public class OAuthAuthenticationProvider : IAuthenticationProvider
{
    private readonly ILogger<OAuthAuthenticationProvider> _logger;
    private readonly IUserRepository _userRepository;
    private readonly Dictionary<string, IOAuthProvider> _oauthProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userRepository">The user repository.</param>
    /// <param name="oauthProviders">The OAuth providers.</param>
    public OAuthAuthenticationProvider(
        ILogger<OAuthAuthenticationProvider> logger,
        IUserRepository userRepository,
        IEnumerable<IOAuthProvider> oauthProviders)
    {
        _logger = logger;
        _userRepository = userRepository;
        _oauthProviders = oauthProviders.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public string Scheme => "OAuth";

    /// <inheritdoc/>
    public async Task<AuthenticationResult> AuthenticateAsync(string credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse OAuth token format: "provider:access_token"
            var parts = credentials.Split(':', 2);
            if (parts.Length != 2)
            {
                return AuthenticationResult.Failure("Invalid OAuth token format. Expected 'provider:access_token'");
            }

            var providerName = parts[0];
            var accessToken = parts[1];

            if (!_oauthProviders.TryGetValue(providerName, out var provider))
            {
                return AuthenticationResult.Failure($"Unknown OAuth provider: {providerName}");
            }

            // Get user info from OAuth provider
            var userInfo = await provider.GetUserInfoAsync(accessToken, cancellationToken);

            // Try to find existing user by external login
            var user = await _userRepository.GetByExternalLoginAsync(providerName, userInfo.Id, cancellationToken);

            if (user == null && !string.IsNullOrEmpty(userInfo.Email))
            {
                // Try to find by email
                user = await _userRepository.GetByEmailAsync(userInfo.Email, cancellationToken);

                if (user != null)
                {
                    // Link the external login to existing user
                    var externalLogin = new ExternalLogin
                    {
                        Provider = providerName,
                        ProviderUserId = userInfo.Id,
                        ProviderDisplayName = userInfo.Name,
                        ProviderData = userInfo.AdditionalData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? string.Empty)
                    };

                    await _userRepository.LinkExternalLoginAsync(user.Id, externalLogin, cancellationToken);
                    _logger.LogInformation("Linked {Provider} login to existing user {UserId}", providerName, user.Id);
                }
            }

            if (user == null)
            {
                // Create new user from OAuth info
                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = userInfo.Email ?? $"{userInfo.Id}@{providerName.ToLowerInvariant()}.oauth",
                    EmailVerified = userInfo.EmailVerified ?? false,
                    DisplayName = userInfo.Name ?? userInfo.Id,
                    AvatarUrl = userInfo.Picture,
                    Roles = new List<string> { "user" },
                    Permissions = new List<string> { "tools:execute", "resources:read" },
                    ExternalLogins = new List<ExternalLogin>
                    {
                        new ExternalLogin
                        {
                            Provider = providerName,
                            ProviderUserId = userInfo.Id,
                            ProviderDisplayName = userInfo.Name,
                            ProviderData = userInfo.AdditionalData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString() ?? string.Empty)
                        }
                    }
                };

                // Set username from provider data
                if (providerName.Equals("GitHub", StringComparison.OrdinalIgnoreCase) && 
                    userInfo.AdditionalData.TryGetValue("login", out var githubLogin))
                {
                    user.Username = githubLogin.ToString();
                }
                else if (!string.IsNullOrEmpty(userInfo.Email))
                {
                    user.Username = userInfo.Email.Split('@')[0];
                }

                user = await _userRepository.CreateAsync(user, cancellationToken);
                _logger.LogInformation("Created new user {UserId} from {Provider} OAuth", user.Id, providerName);
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);

            // Create claims principal
            var principal = user.ToClaimsPrincipal();
            
            // Add OAuth-specific claims
            var identity = (ClaimsIdentity)principal.Identity!;
            identity.AddClaim(new Claim("oauth_provider", providerName));
            identity.AddClaim(new Claim("oauth_access_token", accessToken));

            return AuthenticationResult.Success(principal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth authentication failed");
            return AuthenticationResult.Failure("OAuth authentication failed");
        }
    }
}