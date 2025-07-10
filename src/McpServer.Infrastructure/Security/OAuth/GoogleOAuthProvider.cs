using System.Text.Json;
using McpServer.Domain.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Security.OAuth;

/// <summary>
/// Google OAuth provider implementation.
/// </summary>
public class GoogleOAuthProvider : BaseOAuthProvider
{
    private static readonly string[] ExcludedProperties = { "id", "email", "verified_email", "name", "given_name", "family_name", "picture", "locale" };
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleOAuthProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public GoogleOAuthProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleOAuthProvider> logger,
        IConfiguration configuration)
        : base(httpClientFactory, logger)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public override string Name => "Google";

    /// <inheritdoc/>
    protected override string ClientId => _configuration["McpServer:OAuth:Google:ClientId"] 
        ?? throw new InvalidOperationException("Google OAuth ClientId not configured");

    /// <inheritdoc/>
    protected override string ClientSecret => _configuration["McpServer:OAuth:Google:ClientSecret"] 
        ?? throw new InvalidOperationException("Google OAuth ClientSecret not configured");

    /// <inheritdoc/>
    protected override string AuthorizationEndpoint => "https://accounts.google.com/o/oauth2/v2/auth";

    /// <inheritdoc/>
    protected override string TokenEndpoint => "https://oauth2.googleapis.com/token";

    /// <inheritdoc/>
    protected override string UserInfoEndpoint => "https://www.googleapis.com/oauth2/v2/userinfo";

    /// <inheritdoc/>
    protected override string[] DefaultScopes => new[] { "openid", "email", "profile" };

    /// <inheritdoc/>
    public override async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var httpClient = HttpClientFactory.CreateClient();
        var response = await GetJsonAsync(httpClient, UserInfoEndpoint, accessToken, cancellationToken);
        var root = response.RootElement;

        return new OAuthUserInfo
        {
            Id = root.GetProperty("id").GetString()!,
            Email = root.TryGetProperty("email", out var email) ? email.GetString() : null,
            EmailVerified = root.TryGetProperty("verified_email", out var verified) && verified.GetBoolean(),
            Name = root.TryGetProperty("name", out var name) ? name.GetString() : null,
            GivenName = root.TryGetProperty("given_name", out var givenName) ? givenName.GetString() : null,
            FamilyName = root.TryGetProperty("family_name", out var familyName) ? familyName.GetString() : null,
            Picture = root.TryGetProperty("picture", out var picture) ? picture.GetString() : null,
            Locale = root.TryGetProperty("locale", out var locale) ? locale.GetString() : null,
            AdditionalData = root.EnumerateObject()
                .Where(p => !ExcludedProperties.Contains(p.Name))
                .ToDictionary(p => p.Name, p => (object)p.Value.ToString())
        };
    }
}