using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using McpServer.Application.Services;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace McpServer.Infrastructure.Security;

/// <summary>
/// Configuration options for JWT authentication.
/// </summary>
public class JwtAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the JWT issuer.
    /// </summary>
    public string Issuer { get; set; } = "McpServer";
    
    /// <summary>
    /// Gets or sets the JWT audience.
    /// </summary>
    public string Audience { get; set; } = "McpServer";
    
    /// <summary>
    /// Gets or sets the signing key.
    /// </summary>
    public string? SigningKey { get; set; }
    
    /// <summary>
    /// Gets or sets whether to validate the issuer.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to validate the audience.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to validate the lifetime.
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the clock skew for token validation.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Provides JWT bearer token authentication.
/// </summary>
public class JwtAuthenticationProvider : IAuthenticationProvider
{
    private readonly ILogger<JwtAuthenticationProvider> _logger;
    private readonly IOptions<JwtAuthenticationOptions> _options;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly TokenValidationParameters _validationParameters;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The authentication options.</param>
    public JwtAuthenticationProvider(
        ILogger<JwtAuthenticationProvider> logger,
        IOptions<JwtAuthenticationOptions> options)
    {
        _logger = logger;
        _options = options;
        _tokenHandler = new JwtSecurityTokenHandler();
        
        if (string.IsNullOrEmpty(_options.Value.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is required");
        }
        
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = _options.Value.ValidateIssuer,
            ValidateAudience = _options.Value.ValidateAudience,
            ValidateLifetime = _options.Value.ValidateLifetime,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _options.Value.Issuer,
            ValidAudience = _options.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_options.Value.SigningKey)),
            ClockSkew = _options.Value.ClockSkew
        };
    }

    /// <inheritdoc/>
    public string Scheme => "Bearer";

    /// <inheritdoc/>
    public Task<AuthenticationResult> AuthenticateAsync(string credentials, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials))
        {
            return Task.FromResult(AuthenticationResult.Failure("Token is required"));
        }

        try
        {
            // Validate the token
            var principal = _tokenHandler.ValidateToken(
                credentials,
                _validationParameters,
                out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return Task.FromResult(AuthenticationResult.Failure("Invalid token format"));
            }

            // Extract client ID from token
            var clientId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value
                ?? principal.FindFirst("client_id")?.Value;

            _logger.LogInformation("JWT authentication successful for client: {ClientId}", clientId);
            return Task.FromResult(AuthenticationResult.Success(principal));
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogWarning("Expired JWT token presented");
            return Task.FromResult(AuthenticationResult.Failure("Token has expired"));
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogWarning("JWT token with invalid signature presented");
            return Task.FromResult(AuthenticationResult.Failure("Invalid token signature"));
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed");
            return Task.FromResult(AuthenticationResult.Failure("Token validation failed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JWT authentication");
            return Task.FromResult(AuthenticationResult.Failure("Authentication error occurred"));
        }
    }
    
    /// <summary>
    /// Generates a JWT token for the specified claims.
    /// </summary>
    /// <param name="claims">The claims to include in the token.</param>
    /// <param name="expiration">The token expiration time.</param>
    /// <returns>The generated JWT token.</returns>
    public string GenerateToken(IEnumerable<Claim> claims, TimeSpan expiration)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Value.SigningKey!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _options.Value.Issuer,
            audience: _options.Value.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiration),
            signingCredentials: credentials);
        
        return _tokenHandler.WriteToken(token);
    }
}