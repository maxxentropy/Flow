using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Security.OAuth;

/// <summary>
/// Base class for OAuth providers.
/// </summary>
public abstract class BaseOAuthProvider : IOAuthProvider
{
    private protected readonly IHttpClientFactory HttpClientFactory;
    private protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseOAuthProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    protected BaseOAuthProvider(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        HttpClientFactory = httpClientFactory;
        Logger = logger;
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    protected abstract string ClientId { get; }
    protected abstract string ClientSecret { get; }
    protected abstract string AuthorizationEndpoint { get; }
    protected abstract string TokenEndpoint { get; }
    protected abstract string UserInfoEndpoint { get; }
    protected abstract string[] DefaultScopes { get; }

    /// <inheritdoc/>
    public virtual string GetAuthorizationUrl(string state, string redirectUri, string[]? scopes = null)
    {
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["client_id"] = ClientId;
        queryParams["redirect_uri"] = redirectUri;
        queryParams["response_type"] = "code";
        queryParams["state"] = state;
        queryParams["scope"] = string.Join(" ", scopes ?? DefaultScopes);

        return $"{AuthorizationEndpoint}?{queryParams}";
    }

    /// <inheritdoc/>
    public virtual async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var httpClient = HttpClientFactory.CreateClient();
        
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("client_secret", ClientSecret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri)
        });

        var response = await httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenData = JsonDocument.Parse(json);

        return new OAuthTokenResponse
        {
            AccessToken = tokenData.RootElement.GetProperty("access_token").GetString()!,
            TokenType = tokenData.RootElement.TryGetProperty("token_type", out var tokenType) ? tokenType.GetString() ?? "Bearer" : "Bearer",
            ExpiresIn = tokenData.RootElement.TryGetProperty("expires_in", out var expiresIn) ? expiresIn.GetInt32() : null,
            RefreshToken = tokenData.RootElement.TryGetProperty("refresh_token", out var refreshToken) ? refreshToken.GetString() : null,
            Scope = tokenData.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
            IdToken = tokenData.RootElement.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null
        };
    }

    /// <inheritdoc/>
    public abstract Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public virtual async Task<OAuthTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var httpClient = HttpClientFactory.CreateClient();
        
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("client_secret", ClientSecret),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        });

        var response = await httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenData = JsonDocument.Parse(json);

        return new OAuthTokenResponse
        {
            AccessToken = tokenData.RootElement.GetProperty("access_token").GetString()!,
            TokenType = tokenData.RootElement.TryGetProperty("token_type", out var tokenType) ? tokenType.GetString() ?? "Bearer" : "Bearer",
            ExpiresIn = tokenData.RootElement.TryGetProperty("expires_in", out var expiresIn) ? expiresIn.GetInt32() : null,
            RefreshToken = tokenData.RootElement.TryGetProperty("refresh_token", out var newRefreshToken) ? newRefreshToken.GetString() : refreshToken,
            Scope = tokenData.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString() : null
        };
    }

    protected static async Task<JsonDocument> GetJsonAsync(HttpClient httpClient, string url, string accessToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json);
    }
}