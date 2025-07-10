using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpServer.Domain.Tools;
using McpServer.Domain.Security;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Tools;

/// <summary>
/// A demo tool that shows authentication information.
/// </summary>
public class AuthenticationDemoTool : ITool
{
    private readonly ILogger<AuthenticationDemoTool> _logger;
    private readonly IAuthenticationService _authenticationService;
    private readonly IUserRepository? _userRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationDemoTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="authenticationService">The authentication service.</param>
    /// <param name="userRepository">The user repository (optional).</param>
    public AuthenticationDemoTool(
        ILogger<AuthenticationDemoTool> logger,
        IAuthenticationService authenticationService,
        IUserRepository? userRepository = null)
    {
        _logger = logger;
        _authenticationService = authenticationService;
        _userRepository = userRepository;
    }

    /// <inheritdoc/>
    public string Name => "auth_demo";

    /// <inheritdoc/>
    public string Description => "Demonstrates authentication features";

    /// <inheritdoc/>
    public ToolSchema Schema => new ToolSchema
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["action"] = new
            {
                type = "string",
                description = "The action to perform",
                @enum = new[] { "whoami", "check_permission", "validate_token", "list_users" }
            },
            ["resource"] = new
            {
                type = "string",
                description = "The resource to check permission for (for check_permission action)"
            },
            ["permission_action"] = new
            {
                type = "string",
                description = "The permission action to check (for check_permission action)"
            },
            ["token_type"] = new
            {
                type = "string",
                description = "The token type (for validate_token action)",
                @enum = new[] { "apikey", "jwt" }
            },
            ["token"] = new
            {
                type = "string",
                description = "The token to validate (for validate_token action)"
            }
        },
        Required = new List<string> { "action" }
    };

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        var action = request.Arguments?.GetValueOrDefault("action")?.ToString();
        
        if (string.IsNullOrEmpty(action))
        {
            return CreateErrorResult("Action is required");
        }

        var result = action switch
        {
            "whoami" => await GetCurrentUserInfoAsync(cancellationToken),
            "check_permission" => await CheckPermissionAsync(request, cancellationToken),
            "validate_token" => await ValidateTokenAsync(request, cancellationToken),
            "list_users" => await ListUsersAsync(cancellationToken),
            _ => CreateErrorResult($"Unknown action: {action}")
        };

        return result;
    }

    private static Task<ToolResult> GetCurrentUserInfoAsync(CancellationToken cancellationToken)
    {
        // In a real scenario, this would get the current authentication context
        // For demo purposes, we'll return mock data
        var content = JsonSerializer.Serialize(new
        {
            authenticated = false,
            message = "No authentication context available in demo mode"
        });

        return Task.FromResult(new ToolResult
        {
            Content = new List<ToolContent> { new TextContent { Text = content } }
        });
    }

    private async Task<ToolResult> CheckPermissionAsync(ToolRequest request, CancellationToken cancellationToken)
    {
        var resource = request.Arguments?.GetValueOrDefault("resource")?.ToString();
        var permissionAction = request.Arguments?.GetValueOrDefault("permission_action")?.ToString();

        if (string.IsNullOrEmpty(resource) || string.IsNullOrEmpty(permissionAction))
        {
            return CreateErrorResult("Resource and permission_action are required for check_permission");
        }

        // Create a mock principal for demo
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "demo_user"),
            new Claim(ClaimTypes.Role, "user"),
            new Claim("permission", "tools:execute"),
            new Claim("permission", "resources:read")
        };
        
        var identity = new ClaimsIdentity(claims, "Demo");
        var principal = new ClaimsPrincipal(identity);

        var hasPermission = await _authenticationService.AuthorizeAsync(
            principal, resource, permissionAction);

        var content = JsonSerializer.Serialize(new
        {
            resource = resource,
            action = permissionAction,
            authorized = hasPermission,
            principal = principal.Identity?.Name
        });

        return new ToolResult
        {
            Content = new List<ToolContent> { new TextContent { Text = content } }
        };
    }

    private async Task<ToolResult> ValidateTokenAsync(ToolRequest request, CancellationToken cancellationToken)
    {
        var tokenType = request.Arguments?.GetValueOrDefault("token_type")?.ToString();
        var token = request.Arguments?.GetValueOrDefault("token")?.ToString();

        if (string.IsNullOrEmpty(tokenType) || string.IsNullOrEmpty(token))
        {
            return CreateErrorResult("Token type and token are required for validate_token");
        }

        var scheme = tokenType == "jwt" ? "Bearer" : "ApiKey";
        var result = await _authenticationService.AuthenticateAsync(
            scheme, token, cancellationToken);

        object responseData;
        if (result.IsAuthenticated)
        {
            responseData = new
            {
                valid = true,
                scheme = scheme,
                principal = new
                {
                    name = result.Principal?.Identity?.Name,
                    authenticated = result.Principal?.Identity?.IsAuthenticated,
                    claims = result.Principal?.Claims.Select(c => new
                    {
                        type = c.Type,
                        value = c.Value
                    }).ToList()
                }
            };
        }
        else
        {
            responseData = new
            {
                valid = false,
                scheme = scheme,
                reason = result.FailureReason
            };
        }

        var content = JsonSerializer.Serialize(responseData);
        return new ToolResult
        {
            Content = new List<ToolContent> { new TextContent { Text = content } }
        };
    }

    private async Task<ToolResult> ListUsersAsync(CancellationToken cancellationToken)
    {
        if (_userRepository == null)
        {
            return CreateErrorResult("User repository not available");
        }

        // In a real implementation, this would have pagination
        // For demo purposes, we'll just list the first few users
        var users = new List<object>();
        
        // Get some known users (in real app, would have a ListAsync method)
        var knownUserIds = new[] { "admin", "test-user" };
        foreach (var userId in knownUserIds)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user != null)
            {
                users.Add(new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    displayName = user.DisplayName,
                    roles = user.Roles,
                    isActive = user.IsActive,
                    createdAt = user.CreatedAt,
                    lastLoginAt = user.LastLoginAt,
                    externalLogins = user.ExternalLogins.Select(el => new
                    {
                        provider = el.Provider,
                        linkedAt = el.LinkedAt
                    })
                });
            }
        }

        var content = JsonSerializer.Serialize(new
        {
            users = users,
            count = users.Count
        });

        return new ToolResult
        {
            Content = new List<ToolContent> { new TextContent { Text = content } }
        };
    }

    private static ToolResult CreateErrorResult(string error)
    {
        return new ToolResult
        {
            Content = new List<ToolContent> 
            { 
                new TextContent 
                { 
                    Text = JsonSerializer.Serialize(new { error }) 
                } 
            },
            IsError = true
        };
    }
}