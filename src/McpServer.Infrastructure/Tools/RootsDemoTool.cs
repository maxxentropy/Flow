using System.ComponentModel;
using McpServer.Application.Services;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Tools;

/// <summary>
/// A tool that demonstrates MCP roots functionality.
/// </summary>
[Description("Demonstrates MCP roots functionality for filesystem boundaries")]
public class RootsDemoTool : ITool
{
    private readonly ILogger<RootsDemoTool> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IRootRegistry? _rootRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="RootsDemoTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public RootsDemoTool(ILogger<RootsDemoTool> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public string Name => "roots_demo";

    /// <inheritdoc/>
    public string Description => "Demonstrates MCP roots functionality by showing current roots and testing URI access";

    /// <inheritdoc/>
    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["action"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "The action to perform",
                ["enum"] = new[] { "list", "check", "add", "remove", "clear" }
            },
            ["uri"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "URI to check or root to add/remove (required for check, add, remove actions)"
            },
            ["name"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Name for the root (optional, used with add action)"
            }
        },
        Required = new List<string> { "action" }
    };

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing roots demo tool");

        // Get root registry service lazily
        _rootRegistry ??= _serviceProvider.GetService<IRootRegistry>();

        if (_rootRegistry == null)
        {
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = "Error: Root registry service is not available" }
                },
                IsError = true
            };
        }

        // Parse arguments
        if (request.Arguments == null || !request.Arguments.TryGetValue("action", out var actionObj))
        {
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = "Error: 'action' parameter is required" }
                },
                IsError = true
            };
        }

        var action = actionObj?.ToString() ?? "list";
        var uri = request.Arguments.TryGetValue("uri", out var uriObj) ? uriObj?.ToString() : null;
        var name = request.Arguments.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : null;

        try
        {
            return action switch
            {
                "list" => await HandleListAction(cancellationToken),
                "check" => await HandleCheckAction(uri, cancellationToken),
                "add" => await HandleAddAction(uri, name, cancellationToken),
                "remove" => await HandleRemoveAction(uri, cancellationToken),
                "clear" => await HandleClearAction(cancellationToken),
                _ => new ToolResult
                {
                    Content = new List<ToolContent>
                    {
                        new McpServer.Domain.Tools.TextContent 
                        { 
                            Text = $"Error: Unknown action '{action}'. Valid actions are: list, check, add, remove, clear" 
                        }
                    },
                    IsError = true
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing roots demo action: {Action}", action);
            return new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = $"Error executing action '{action}': {ex.Message}" }
                },
                IsError = true
            };
        }
    }

    private Task<ToolResult> HandleListAction(CancellationToken cancellationToken)
    {
        var roots = _rootRegistry!.Roots;
        var response = $"Current roots ({roots.Count} total):\n";

        if (roots.Count == 0)
        {
            response += "No roots configured (all access allowed)";
        }
        else
        {
            for (int i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                response += $"{i + 1}. {root.Uri}";
                if (!string.IsNullOrEmpty(root.Name))
                {
                    response += $" ({root.Name})";
                }
                response += "\n";
            }
        }

        return Task.FromResult(new ToolResult
        {
            Content = new List<ToolContent>
            {
                new McpServer.Domain.Tools.TextContent { Text = response.TrimEnd() }
            }
        });
    }

    private Task<ToolResult> HandleCheckAction(string? uri, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return Task.FromResult(new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = "Error: 'uri' parameter is required for check action" }
                },
                IsError = true
            });
        }

        var isAllowed = _rootRegistry!.IsWithinRootBoundaries(uri);
        var containingRoot = _rootRegistry.GetContainingRoot(uri);

        var response = $"URI: {uri}\n";
        response += $"Access allowed: {(isAllowed ? "Yes" : "No")}\n";

        if (containingRoot != null)
        {
            response += $"Containing root: {containingRoot.Uri}";
            if (!string.IsNullOrEmpty(containingRoot.Name))
            {
                response += $" ({containingRoot.Name})";
            }
        }
        else if (_rootRegistry.HasRoots)
        {
            response += "Containing root: None (outside all configured roots)";
        }
        else
        {
            response += "Containing root: N/A (no roots configured, all access allowed)";
        }

        return Task.FromResult(new ToolResult
        {
            Content = new List<ToolContent>
            {
                new McpServer.Domain.Tools.TextContent { Text = response }
            }
        });
    }

    private Task<ToolResult> HandleAddAction(string? uri, string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return Task.FromResult(new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = "Error: 'uri' parameter is required for add action" }
                },
                IsError = true
            });
        }

        var root = new Root { Uri = uri, Name = name };
        _rootRegistry!.AddRoot(root);

        var response = $"Added root: {uri}";
        if (!string.IsNullOrEmpty(name))
        {
            response += $" ({name})";
        }

        return Task.FromResult(new ToolResult
        {
            Content = new List<ToolContent>
            {
                new McpServer.Domain.Tools.TextContent { Text = response }
            }
        });
    }

    private Task<ToolResult> HandleRemoveAction(string? uri, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return Task.FromResult(new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new McpServer.Domain.Tools.TextContent { Text = "Error: 'uri' parameter is required for remove action" }
                },
                IsError = true
            });
        }

        var removed = _rootRegistry!.RemoveRoot(uri);
        var response = removed 
            ? $"Removed root: {uri}" 
            : $"Root not found: {uri}";

        return Task.FromResult(new ToolResult
        {
            Content = new List<ToolContent>
            {
                new McpServer.Domain.Tools.TextContent { Text = response }
            }
        });
    }

    private Task<ToolResult> HandleClearAction(CancellationToken cancellationToken)
    {
        var rootCount = _rootRegistry!.Roots.Count;
        _rootRegistry.ClearRoots();

        var response = $"Cleared all roots ({rootCount} removed). All access is now allowed.";

        return Task.FromResult(new ToolResult
        {
            Content = new List<ToolContent>
            {
                new McpServer.Domain.Tools.TextContent { Text = response }
            }
        });
    }
}