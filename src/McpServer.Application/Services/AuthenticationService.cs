using System.Security.Claims;
using McpServer.Domain.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Default implementation of the authentication service.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, IAuthenticationProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="providers">The authentication providers.</param>
    public AuthenticationService(
        ILogger<AuthenticationService> logger,
        IConfiguration configuration,
        IEnumerable<IAuthenticationProvider> providers)
    {
        _logger = logger;
        _configuration = configuration;
        _providers = providers
            .GroupBy(p => p.Scheme, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<AuthenticationResult> AuthenticateAsync(
        string scheme,
        string credentials,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scheme))
        {
            _logger.LogWarning("Authentication attempted with empty scheme");
            return AuthenticationResult.Failure("Authentication scheme is required");
        }

        if (string.IsNullOrEmpty(credentials))
        {
            _logger.LogWarning("Authentication attempted with empty credentials");
            return AuthenticationResult.Failure("Credentials are required");
        }

        if (!_providers.TryGetValue(scheme, out var provider))
        {
            _logger.LogWarning("Unsupported authentication scheme: {Scheme}", scheme);
            return AuthenticationResult.Failure($"Unsupported authentication scheme: {scheme}");
        }

        try
        {
            _logger.LogDebug("Authenticating with scheme: {Scheme}", scheme);
            var result = await provider.AuthenticateAsync(credentials, cancellationToken);
            
            if (result.IsAuthenticated)
            {
                _logger.LogInformation("Authentication successful for scheme: {Scheme}, ClientId: {ClientId}",
                    scheme, result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            }
            else
            {
                _logger.LogWarning("Authentication failed for scheme: {Scheme}, Reason: {Reason}",
                    scheme, result.FailureReason);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication with scheme: {Scheme}", scheme);
            return AuthenticationResult.Failure("Authentication error occurred");
        }
    }

    /// <inheritdoc/>
    public Task<bool> AuthorizeAsync(
        ClaimsPrincipal principal,
        string resource,
        string action)
    {
        if (principal == null || principal.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Authorization attempted with unauthenticated principal");
            return Task.FromResult(false);
        }

        // Check for admin role
        if (principal.IsInRole("admin"))
        {
            _logger.LogDebug("Authorization granted to admin for {Resource}:{Action}", resource, action);
            return Task.FromResult(true);
        }

        // Check for specific permission claim
        var permissionClaim = $"{resource}:{action}";
        if (principal.HasClaim("permission", permissionClaim))
        {
            _logger.LogDebug("Authorization granted via permission claim for {Resource}:{Action}", resource, action);
            return Task.FromResult(true);
        }

        // Check for wildcard permissions
        if (principal.HasClaim("permission", $"{resource}:*") ||
            principal.HasClaim("permission", $"*:{action}") ||
            principal.HasClaim("permission", "*:*"))
        {
            _logger.LogDebug("Authorization granted via wildcard permission for {Resource}:{Action}", resource, action);
            return Task.FromResult(true);
        }

        _logger.LogWarning("Authorization denied for {Resource}:{Action}, Principal: {Principal}",
            resource, action, principal.Identity?.Name);
        return Task.FromResult(false);
    }
}

/// <summary>
/// Interface for authentication providers.
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Gets the authentication scheme this provider handles.
    /// </summary>
    string Scheme { get; }
    
    /// <summary>
    /// Authenticates using the provided credentials.
    /// </summary>
    /// <param name="credentials">The credentials to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication result.</returns>
    Task<AuthenticationResult> AuthenticateAsync(string credentials, CancellationToken cancellationToken = default);
}