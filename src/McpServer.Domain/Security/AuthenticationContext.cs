using System.Security.Claims;

namespace McpServer.Domain.Security;

/// <summary>
/// Represents the authentication context for a request.
/// </summary>
public class AuthenticationContext
{
    /// <summary>
    /// Gets or sets whether the request is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; }
    
    /// <summary>
    /// Gets or sets the authenticated principal.
    /// </summary>
    public ClaimsPrincipal? Principal { get; set; }
    
    /// <summary>
    /// Gets or sets the authentication scheme used.
    /// </summary>
    public string? AuthenticationScheme { get; set; }
    
    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    public string? ClientId { get; set; }
    
    /// <summary>
    /// Gets or sets additional authentication properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
    
    /// <summary>
    /// Creates an unauthenticated context.
    /// </summary>
    /// <returns>An unauthenticated context.</returns>
    public static AuthenticationContext Unauthenticated()
    {
        return new AuthenticationContext
        {
            IsAuthenticated = false
        };
    }
    
    /// <summary>
    /// Creates an authenticated context.
    /// </summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <param name="scheme">The authentication scheme.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <returns>An authenticated context.</returns>
    public static AuthenticationContext Authenticated(
        ClaimsPrincipal principal,
        string scheme,
        string? clientId = null)
    {
        return new AuthenticationContext
        {
            IsAuthenticated = true,
            Principal = principal,
            AuthenticationScheme = scheme,
            ClientId = clientId
        };
    }
}