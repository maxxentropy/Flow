namespace McpServer.Domain.Security;

/// <summary>
/// Interface for OAuth authentication providers.
/// </summary>
public interface IOAuthProvider
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the authorization URL for the provider.
    /// </summary>
    /// <param name="state">The state parameter for CSRF protection.</param>
    /// <param name="redirectUri">The redirect URI after authorization.</param>
    /// <param name="scopes">The requested scopes.</param>
    /// <returns>The authorization URL.</returns>
    string GetAuthorizationUrl(string state, string redirectUri, string[]? scopes = null);

    /// <summary>
    /// Exchanges an authorization code for an access token.
    /// </summary>
    /// <param name="code">The authorization code.</param>
    /// <param name="redirectUri">The redirect URI used in authorization.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The OAuth token response.</returns>
    Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user information using an access token.
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user information.</returns>
    Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new token response.</returns>
    Task<OAuthTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an OAuth token response.
/// </summary>
public class OAuthTokenResponse
{
    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the token type (usually "Bearer").
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets the expiration time in seconds.
    /// </summary>
    public int? ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the granted scopes.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the ID token (for OpenID Connect).
    /// </summary>
    public string? IdToken { get; set; }
}

/// <summary>
/// Represents user information from an OAuth provider.
/// </summary>
public class OAuthUserInfo
{
    /// <summary>
    /// Gets or sets the provider user ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets whether the email is verified.
    /// </summary>
    public bool? EmailVerified { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the given name.
    /// </summary>
    public string? GivenName { get; set; }

    /// <summary>
    /// Gets or sets the family name.
    /// </summary>
    public string? FamilyName { get; set; }

    /// <summary>
    /// Gets or sets the profile picture URL.
    /// </summary>
    public string? Picture { get; set; }

    /// <summary>
    /// Gets or sets the locale.
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific data.
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}