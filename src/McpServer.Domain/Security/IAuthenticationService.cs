using System.Security.Claims;

namespace McpServer.Domain.Security;

/// <summary>
/// Represents the result of an authentication attempt.
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// Gets whether the authentication was successful.
    /// </summary>
    public bool IsAuthenticated { get; init; }
    
    /// <summary>
    /// Gets the authenticated principal.
    /// </summary>
    public ClaimsPrincipal? Principal { get; init; }
    
    /// <summary>
    /// Gets the reason for authentication failure.
    /// </summary>
    public string? FailureReason { get; init; }
    
    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <returns>A successful authentication result.</returns>
    public static AuthenticationResult Success(ClaimsPrincipal principal)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = true,
            Principal = principal
        };
    }
    
    /// <summary>
    /// Creates a failed authentication result.
    /// </summary>
    /// <param name="reason">The reason for failure.</param>
    /// <returns>A failed authentication result.</returns>
    public static AuthenticationResult Failure(string reason)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = false,
            FailureReason = reason
        };
    }
}

/// <summary>
/// Provides authentication services for the MCP server.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a request using the provided authentication scheme and credentials.
    /// </summary>
    /// <param name="scheme">The authentication scheme (e.g., "Bearer", "ApiKey").</param>
    /// <param name="credentials">The credentials to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication result.</returns>
    Task<AuthenticationResult> AuthenticateAsync(
        string scheme,
        string credentials,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates whether the authenticated principal has access to the specified resource.
    /// </summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <param name="resource">The resource being accessed.</param>
    /// <param name="action">The action being performed.</param>
    /// <returns>True if access is allowed; otherwise, false.</returns>
    Task<bool> AuthorizeAsync(
        ClaimsPrincipal principal,
        string resource,
        string action);
}