using System.Text.Json;
using McpServer.Domain.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Security.OAuth;

/// <summary>
/// Microsoft OAuth provider implementation.
/// </summary>
public class MicrosoftOAuthProvider : BaseOAuthProvider
{
    private static readonly string[] ExcludedProperties = { "id", "mail", "userPrincipalName", "displayName", "givenName", "surname", "preferredLanguage" };
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="MicrosoftOAuthProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public MicrosoftOAuthProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<MicrosoftOAuthProvider> logger,
        IConfiguration configuration)
        : base(httpClientFactory, logger)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public override string Name => "Microsoft";

    /// <inheritdoc/>
    protected override string ClientId => _configuration["McpServer:OAuth:Microsoft:ClientId"] 
        ?? throw new InvalidOperationException("Microsoft OAuth ClientId not configured");

    /// <inheritdoc/>
    protected override string ClientSecret => _configuration["McpServer:OAuth:Microsoft:ClientSecret"] 
        ?? throw new InvalidOperationException("Microsoft OAuth ClientSecret not configured");

    /// <inheritdoc/>
    protected override string AuthorizationEndpoint => "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";

    /// <inheritdoc/>
    protected override string TokenEndpoint => "https://login.microsoftonline.com/common/oauth2/v2.0/token";

    /// <inheritdoc/>
    protected override string UserInfoEndpoint => "https://graph.microsoft.com/v1.0/me";

    /// <inheritdoc/>
    protected override string[] DefaultScopes => new[] { "openid", "email", "profile", "User.Read" };

    /// <inheritdoc/>
    public override async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var httpClient = HttpClientFactory.CreateClient();
        var response = await GetJsonAsync(httpClient, UserInfoEndpoint, accessToken, cancellationToken);
        var root = response.RootElement;

        // Get profile photo URL if available
        string? photoUrl = null;
        try
        {
            var photoResponse = await GetJsonAsync(httpClient, "https://graph.microsoft.com/v1.0/me/photo", accessToken, cancellationToken);
            if (photoResponse.RootElement.TryGetProperty("@odata.mediaContentType", out _))
            {
                photoUrl = $"https://graph.microsoft.com/v1.0/me/photo/$value";
            }
        }
        catch
        {
            // Photo might not be available
        }

        return new OAuthUserInfo
        {
            Id = root.GetProperty("id").GetString()!,
            Email = root.TryGetProperty("mail", out var mail) ? mail.GetString() : 
                    root.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() : null,
            EmailVerified = true, // Microsoft accounts are always verified
            Name = root.TryGetProperty("displayName", out var displayName) ? displayName.GetString() : null,
            GivenName = root.TryGetProperty("givenName", out var givenName) ? givenName.GetString() : null,
            FamilyName = root.TryGetProperty("surname", out var surname) ? surname.GetString() : null,
            Picture = photoUrl,
            Locale = root.TryGetProperty("preferredLanguage", out var lang) ? lang.GetString() : null,
            AdditionalData = root.EnumerateObject()
                .Where(p => !ExcludedProperties.Contains(p.Name))
                .ToDictionary(p => p.Name, p => (object)p.Value.ToString())
        };
    }
}