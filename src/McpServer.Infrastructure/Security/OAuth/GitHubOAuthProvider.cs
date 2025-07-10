using System.Net.Http.Headers;
using System.Text.Json;
using McpServer.Domain.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Security.OAuth;

/// <summary>
/// GitHub OAuth provider implementation.
/// </summary>
public class GitHubOAuthProvider : BaseOAuthProvider
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubOAuthProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public GitHubOAuthProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubOAuthProvider> logger,
        IConfiguration configuration)
        : base(httpClientFactory, logger)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public override string Name => "GitHub";

    /// <inheritdoc/>
    protected override string ClientId => _configuration["McpServer:OAuth:GitHub:ClientId"] 
        ?? throw new InvalidOperationException("GitHub OAuth ClientId not configured");

    /// <inheritdoc/>
    protected override string ClientSecret => _configuration["McpServer:OAuth:GitHub:ClientSecret"] 
        ?? throw new InvalidOperationException("GitHub OAuth ClientSecret not configured");

    /// <inheritdoc/>
    protected override string AuthorizationEndpoint => "https://github.com/login/oauth/authorize";

    /// <inheritdoc/>
    protected override string TokenEndpoint => "https://github.com/login/oauth/access_token";

    /// <inheritdoc/>
    protected override string UserInfoEndpoint => "https://api.github.com/user";

    /// <inheritdoc/>
    protected override string[] DefaultScopes => new[] { "read:user", "user:email" };

    /// <inheritdoc/>
    public override async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var httpClient = HttpClientFactory.CreateClient();
        
        // GitHub requires Accept header for JSON response
        var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("client_secret", ClientSecret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri)
        });

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenData = JsonDocument.Parse(json);

        return new OAuthTokenResponse
        {
            AccessToken = tokenData.RootElement.GetProperty("access_token").GetString()!,
            TokenType = tokenData.RootElement.TryGetProperty("token_type", out var tokenType) ? tokenType.GetString() ?? "bearer" : "bearer",
            Scope = tokenData.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString() : null
        };
    }

    /// <inheritdoc/>
    public override async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var httpClient = HttpClientFactory.CreateClient();
        
        // Set User-Agent header as required by GitHub API
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("McpServer/1.0");
        
        var userResponse = await GetJsonAsync(httpClient, UserInfoEndpoint, accessToken, cancellationToken);
        var root = userResponse.RootElement;

        // Get primary email if not public
        string? email = root.TryGetProperty("email", out var emailProp) && emailProp.ValueKind != JsonValueKind.Null 
            ? emailProp.GetString() 
            : null;
        
        bool emailVerified = false;
        
        if (string.IsNullOrEmpty(email))
        {
            // Fetch email from emails endpoint
            try
            {
                var emailsResponse = await GetJsonAsync(httpClient, "https://api.github.com/user/emails", accessToken, cancellationToken);
                if (emailsResponse.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var emailObj in emailsResponse.RootElement.EnumerateArray())
                    {
                        if (emailObj.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                        {
                            email = emailObj.GetProperty("email").GetString();
                            emailVerified = emailObj.TryGetProperty("verified", out var verified) && verified.GetBoolean();
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Email endpoint might not be accessible
            }
        }

        return new OAuthUserInfo
        {
            Id = root.GetProperty("id").GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
            Email = email,
            EmailVerified = emailVerified,
            Name = root.TryGetProperty("name", out var name) && name.ValueKind != JsonValueKind.Null ? name.GetString() : null,
            Picture = root.TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() : null,
            AdditionalData = new Dictionary<string, object>
            {
                ["login"] = root.GetProperty("login").GetString()!,
                ["html_url"] = root.GetProperty("html_url").GetString()!,
                ["type"] = root.GetProperty("type").GetString()!
            }
        };
    }

    /// <inheritdoc/>
    public override Task<OAuthTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // GitHub doesn't support refresh tokens
        throw new NotSupportedException("GitHub OAuth does not support refresh tokens");
    }
}