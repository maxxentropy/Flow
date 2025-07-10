using System.Security.Claims;
using McpServer.Application.Services;
using McpServer.Domain.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Infrastructure.Security;

/// <summary>
/// Configuration options for API key authentication.
/// </summary>
public class ApiKeyAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the API keys configuration.
    /// </summary>
    public Dictionary<string, ApiKeyConfiguration> ApiKeys { get; set; } = new();
}

/// <summary>
/// Configuration for an individual API key.
/// </summary>
public class ApiKeyConfiguration
{
    /// <summary>
    /// Gets or sets the API key value.
    /// </summary>
    public required string Key { get; set; }
    
    /// <summary>
    /// Gets or sets the client name.
    /// </summary>
    public required string ClientName { get; set; }
    
    /// <summary>
    /// Gets or sets the associated user ID.
    /// </summary>
    public string? UserId { get; set; }
    
    /// <summary>
    /// Gets or sets the roles assigned to this API key.
    /// </summary>
    public List<string> Roles { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the permissions assigned to this API key.
    /// </summary>
    public List<string> Permissions { get; set; } = new();
    
    /// <summary>
    /// Gets or sets whether this API key is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the expiration date for this API key.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Provides API key based authentication.
/// </summary>
public class ApiKeyAuthenticationProvider : IAuthenticationProvider
{
    private readonly ILogger<ApiKeyAuthenticationProvider> _logger;
    private readonly IOptions<ApiKeyAuthenticationOptions> _options;
    private readonly IUserRepository? _userRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The authentication options.</param>
    /// <param name="userRepository">The user repository (optional).</param>
    public ApiKeyAuthenticationProvider(
        ILogger<ApiKeyAuthenticationProvider> logger,
        IOptions<ApiKeyAuthenticationOptions> options,
        IUserRepository? userRepository = null)
    {
        _logger = logger;
        _options = options;
        _userRepository = userRepository;
    }

    /// <inheritdoc/>
    public string Scheme => "ApiKey";

    /// <inheritdoc/>
    public async Task<AuthenticationResult> AuthenticateAsync(string credentials, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials))
        {
            return AuthenticationResult.Failure("API key is required");
        }

        // Find matching API key
        var matchingKey = _options.Value.ApiKeys.Values
            .FirstOrDefault(k => k.Key == credentials && k.Enabled);

        if (matchingKey == null)
        {
            _logger.LogWarning("Invalid API key attempted");
            return AuthenticationResult.Failure("Invalid API key");
        }

        // Check expiration
        if (matchingKey.ExpiresAt.HasValue && matchingKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired API key used: {ClientName}", matchingKey.ClientName);
            return AuthenticationResult.Failure("API key has expired");
        }

        // If user ID is specified and repository is available, load user
        User? user = null;
        if (!string.IsNullOrEmpty(matchingKey.UserId) && _userRepository != null)
        {
            user = await _userRepository.GetByIdAsync(matchingKey.UserId, cancellationToken);
            if (user != null && !user.IsActive)
            {
                _logger.LogWarning("API key used for inactive user: {UserId}", user.Id);
                return AuthenticationResult.Failure("User account is inactive");
            }
        }

        ClaimsPrincipal principal;
        if (user != null)
        {
            // Use user's claims
            principal = user.ToClaimsPrincipal();
            var identity = (ClaimsIdentity)principal.Identity!;
            
            // Add API key specific claims
            identity.AddClaim(new Claim("auth_method", "apikey"));
            identity.AddClaim(new Claim("api_key_name", matchingKey.ClientName));
            
            // Add any additional roles/permissions from API key config
            foreach (var role in matchingKey.Roles.Where(r => !user.Roles.Contains(r)))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
            
            foreach (var permission in matchingKey.Permissions.Where(p => !user.Permissions.Contains(p)))
            {
                identity.AddClaim(new Claim("permission", permission));
            }
        }
        else
        {
            // Create claims from API key config
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, matchingKey.ClientName),
                new Claim(ClaimTypes.Name, matchingKey.ClientName),
                new Claim("auth_method", "apikey")
            };

            // Add roles
            foreach (var role in matchingKey.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add permissions
            foreach (var permission in matchingKey.Permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var identity = new ClaimsIdentity(claims, "ApiKey");
            principal = new ClaimsPrincipal(identity);
        }

        _logger.LogInformation("API key authentication successful for client: {ClientName}", matchingKey.ClientName);
        return AuthenticationResult.Success(principal);
    }
}