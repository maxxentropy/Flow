using System.Text.Json;
using System.Text.Json.Nodes;
using McpServer.Domain.Protocol;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Middleware;

/// <summary>
/// Configuration for authentication middleware.
/// </summary>
public class AuthenticationMiddlewareOptions
{
    /// <summary>
    /// Gets or sets whether authentication is required for all requests.
    /// </summary>
    public bool RequireAuthentication { get; set; }
    
    /// <summary>
    /// Gets or sets the methods that do not require authentication.
    /// </summary>
    public List<string> AnonymousMethods { get; set; } = new() { "initialize" };
    
    /// <summary>
    /// Gets or sets whether to include authentication errors in responses.
    /// </summary>
    public bool IncludeErrorDetails { get; set; }
}

/// <summary>
/// Middleware that handles authentication for MCP messages.
/// </summary>
public class AuthenticationMiddleware : IMessageMiddleware
{
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly IAuthenticationService _authenticationService;
    private readonly IConfiguration _configuration;
    private readonly AuthenticationMiddlewareOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationMiddleware"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="authenticationService">The authentication service.</param>
    /// <param name="configuration">The configuration.</param>
    public AuthenticationMiddleware(
        ILogger<AuthenticationMiddleware> logger,
        IAuthenticationService authenticationService,
        IConfiguration configuration)
    {
        _logger = logger;
        _authenticationService = authenticationService;
        _configuration = configuration;
        _options = new AuthenticationMiddlewareOptions();
        configuration.GetSection("McpServer:Authentication").Bind(_options);
    }

    /// <summary>
    /// Processes a request through the authentication middleware.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="context">The authentication context.</param>
    /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
    /// <returns>The response.</returns>
    public async Task<JsonRpcResponse?> ProcessAsync(
        JsonRpcRequest request,
        AuthenticationContext context,
        Func<JsonRpcRequest, AuthenticationContext, Task<JsonRpcResponse?>> nextMiddleware)
    {
        // Check if authentication is required
        if (!_options.RequireAuthentication || 
            (request.Method != null && _options.AnonymousMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase)))
        {
            _logger.LogDebug("Skipping authentication for method: {Method}", request.Method);
            return await nextMiddleware(request, context);
        }

        // Extract authentication information from request
        var authInfo = ExtractAuthenticationInfo(request);
        if (authInfo == null)
        {
            _logger.LogWarning("No authentication information provided for method: {Method}", request.Method);
            return CreateAuthenticationError(request, "Authentication required");
        }

        // Authenticate the request
        var result = await _authenticationService.AuthenticateAsync(
            authInfo.Value.Scheme,
            authInfo.Value.Credentials);

        if (!result.IsAuthenticated)
        {
            _logger.LogWarning("Authentication failed for method: {Method}, Reason: {Reason}",
                request.Method, result.FailureReason);
            return CreateAuthenticationError(request, 
                _options.IncludeErrorDetails ? result.FailureReason! : "Authentication failed");
        }

        // Update the context with authentication information
        context.IsAuthenticated = true;
        context.Principal = result.Principal;
        context.AuthenticationScheme = authInfo.Value.Scheme;
        context.ClientId = result.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        _logger.LogInformation("Authenticated request for method: {Method}, ClientId: {ClientId}",
            request.Method, context.ClientId);

        // Continue to next middleware
        return await nextMiddleware(request, context);
    }

    private (string Scheme, string Credentials)? ExtractAuthenticationInfo(JsonRpcRequest request)
    {
        try
        {
            // Look for auth header in params
            if (request.Params != null)
            {
                var paramsJson = JsonSerializer.Serialize(request.Params);
                var doc = JsonDocument.Parse(paramsJson);
                
                if (doc.RootElement.TryGetProperty("_auth", out var authElement))
                {
                    var authValue = authElement.GetString();
                    if (!string.IsNullOrEmpty(authValue))
                    {
                        var parts = authValue.Split(' ', 2);
                        if (parts.Length == 2)
                        {
                            return (parts[0], parts[1]);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting authentication information");
        }
        
        return null;
    }

    private static JsonRpcResponse CreateAuthenticationError(JsonRpcRequest request, string message)
    {
        return new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Error = new JsonRpcError
            {
                Code = -32001, // Custom error code for authentication
                Message = message
            },
            Id = request.Id
        };
    }
}

/// <summary>
/// Interface for message processing middleware.
/// </summary>
public interface IMessageMiddleware
{
    /// <summary>
    /// Processes a request through the middleware.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="context">The authentication context.</param>
    /// <param name="nextMiddleware">The next middleware in the pipeline.</param>
    /// <returns>The response.</returns>
    Task<JsonRpcResponse?> ProcessAsync(
        JsonRpcRequest request,
        AuthenticationContext context,
        Func<JsonRpcRequest, AuthenticationContext, Task<JsonRpcResponse?>> nextMiddleware);
}