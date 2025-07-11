using System.Text.Json;
using McpServer.Application.Resources;
using McpServer.Domain.Resources;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Resources;

/// <summary>
/// Provides REST API endpoint documentation as resources using templates.
/// </summary>
public class RestApiResourceProvider : TemplateResourceProvider
{
    private readonly ILogger<RestApiResourceProvider> _logger;
    
    // Mock API endpoints for demonstration
    private readonly List<ApiEndpoint> _endpoints = new()
    {
        new ApiEndpoint
        {
            Method = "GET",
            Path = "/users",
            Description = "List all users",
            Parameters = new[] { "page", "limit", "sort" },
            ResponseExample = """
            {
              "users": [
                {"id": 1, "name": "John Doe", "email": "john@example.com"}
              ],
              "total": 100,
              "page": 1
            }
            """
        },
        new ApiEndpoint
        {
            Method = "GET",
            Path = "/users/{id}",
            Description = "Get user by ID",
            Parameters = Array.Empty<string>(),
            ResponseExample = """
            {
              "id": 1,
              "name": "John Doe",
              "email": "john@example.com",
              "created_at": "2024-01-01T00:00:00Z"
            }
            """
        },
        new ApiEndpoint
        {
            Method = "POST",
            Path = "/users",
            Description = "Create a new user",
            Parameters = Array.Empty<string>(),
            RequestExample = """
            {
              "name": "Jane Doe",
              "email": "jane@example.com",
              "password": "secure123"
            }
            """,
            ResponseExample = """
            {
              "id": 2,
              "name": "Jane Doe",
              "email": "jane@example.com"
            }
            """
        },
        new ApiEndpoint
        {
            Method = "GET",
            Path = "/products/{id}",
            Description = "Get product details",
            Parameters = new[] { "include_reviews" },
            ResponseExample = """
            {
              "id": 1,
              "name": "Widget",
              "price": 19.99,
              "stock": 100
            }
            """
        }
    };
    
    /// <summary>
    /// Initializes a new instance of the <see cref="RestApiResourceProvider"/> class.
    /// </summary>
    public RestApiResourceProvider(ILogger<RestApiResourceProvider> logger) : base(logger)
    {
        _logger = logger;
        
        // Register templates
        RegisterTemplate(new ResourceTemplate(
            "api://endpoints",
            "API Endpoints List",
            "List of all available API endpoints",
            "application/json"
        ));
        
        RegisterTemplate(new ResourceTemplate(
            "api://endpoints/{method}/{path}",
            "API Endpoint Details",
            "Detailed documentation for a specific endpoint",
            "application/json"
        ));
        
        RegisterTemplate(new ResourceTemplate(
            "api://openapi",
            "OpenAPI Specification",
            "OpenAPI 3.0 specification for the API",
            "application/json"
        ));
    }
    
    /// <inheritdoc/>
    public override async Task<IEnumerable<TemplateResourceInstance>> ListTemplateInstancesAsync(CancellationToken cancellationToken = default)
    {
        var instances = new List<TemplateResourceInstance>();
        
        // Add endpoints list
        instances.Add(new TemplateResourceInstance
        {
            Template = Templates.First(t => t.Name == "API Endpoints List"),
            Parameters = new Dictionary<string, string>(),
            DisplayName = "All API Endpoints"
        });
        
        // Add OpenAPI spec
        instances.Add(new TemplateResourceInstance
        {
            Template = Templates.First(t => t.Name == "OpenAPI Specification"),
            Parameters = new Dictionary<string, string>(),
            DisplayName = "OpenAPI 3.0 Specification"
        });
        
        // Add individual endpoint instances
        foreach (var endpoint in _endpoints)
        {
            // Encode the path to make it URL-safe
            var encodedPath = endpoint.Path.Replace("/", "_").TrimStart('_');
            
            instances.Add(new TemplateResourceInstance
            {
                Template = Templates.First(t => t.Name == "API Endpoint Details"),
                Parameters = new Dictionary<string, string>
                {
                    ["method"] = endpoint.Method.ToLower(),
                    ["path"] = encodedPath
                },
                DisplayName = $"{endpoint.Method} {endpoint.Path}",
                Metadata = new Dictionary<string, object>
                {
                    ["actualPath"] = endpoint.Path,
                    ["description"] = endpoint.Description
                }
            });
        }
        
        return await Task.FromResult(instances);
    }
    
    /// <inheritdoc/>
    public override async Task<ResourceContent> ReadTemplateResourceAsync(
        IResourceTemplate template,
        IDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        switch (template.Name)
        {
            case "API Endpoints List":
                return await ReadEndpointsListAsync(cancellationToken);
                
            case "API Endpoint Details":
                return await ReadEndpointDetailsAsync(
                    parameters["method"].ToUpper(),
                    parameters["path"],
                    cancellationToken);
                    
            case "OpenAPI Specification":
                return await GenerateOpenApiSpecAsync(cancellationToken);
                
            default:
                throw new NotSupportedException($"Template {template.Name} is not supported");
        }
    }
    
    private async Task<ResourceContent> ReadEndpointsListAsync(CancellationToken cancellationToken)
    {
        var endpointsList = _endpoints.Select(e => new
        {
            method = e.Method,
            path = e.Path,
            description = e.Description,
            parameters = e.Parameters,
            hasRequestBody = !string.IsNullOrEmpty(e.RequestExample)
        });
        
        var json = JsonSerializer.Serialize(new { endpoints = endpointsList }, 
            new JsonSerializerOptions { WriteIndented = true });
        
        return await Task.FromResult(new ResourceContent
        {
            Uri = "api://endpoints",
            MimeType = "application/json",
            Text = json
        });
    }
    
    private async Task<ResourceContent> ReadEndpointDetailsAsync(
        string method,
        string encodedPath,
        CancellationToken cancellationToken)
    {
        // Decode the path
        var actualPath = "/" + encodedPath.Replace("_", "/");
        
        var endpoint = _endpoints.FirstOrDefault(e => 
            e.Method.Equals(method, StringComparison.OrdinalIgnoreCase) &&
            e.Path.Equals(actualPath, StringComparison.OrdinalIgnoreCase));
            
        if (endpoint == null)
        {
            throw new ResourceNotFoundException($"Endpoint {method} {actualPath} not found");
        }
        
        var details = new
        {
            method = endpoint.Method,
            path = endpoint.Path,
            description = endpoint.Description,
            parameters = endpoint.Parameters.Select(p => new
            {
                name = p,
                in_ = "query",
                required = false,
                type = "string"
            }),
            requestBody = string.IsNullOrEmpty(endpoint.RequestExample) ? null : new
            {
                contentType = "application/json",
                example = JsonSerializer.Deserialize<JsonElement>(endpoint.RequestExample)
            },
            responses = new
            {
                success = new
                {
                    status = endpoint.Method == "POST" ? 201 : 200,
                    contentType = "application/json",
                    example = JsonSerializer.Deserialize<JsonElement>(endpoint.ResponseExample)
                }
            }
        };
        
        var json = JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true });
        
        return await Task.FromResult(new ResourceContent
        {
            Uri = $"api://endpoints/{method.ToLower()}/{encodedPath}",
            MimeType = "application/json",
            Text = json
        });
    }
    
    private async Task<ResourceContent> GenerateOpenApiSpecAsync(CancellationToken cancellationToken)
    {
        var spec = new
        {
            openapi = "3.0.0",
            info = new
            {
                title = "Example API",
                version = "1.0.0",
                description = "Example API with dynamic resource templates"
            },
            servers = new[]
            {
                new { url = "https://api.example.com", description = "Production server" }
            },
            paths = _endpoints.GroupBy(e => e.Path).ToDictionary(
                g => g.Key,
                g => g.ToDictionary(
                    e => e.Method.ToLower(),
                    e => new
                    {
                        summary = e.Description,
                        parameters = e.Parameters.Select(p => new
                        {
                            name = p,
                            in_ = "query",
                            required = false,
                            schema = new { type = "string" }
                        }),
                        requestBody = string.IsNullOrEmpty(e.RequestExample) ? null : new
                        {
                            content = new
                            {
                                applicationJson = new
                                {
                                    schema = new { type = "object" },
                                    example = JsonSerializer.Deserialize<JsonElement>(e.RequestExample)
                                }
                            }
                        },
                        responses = new
                        {
                            success = new
                            {
                                description = "Successful response",
                                content = new
                                {
                                    applicationJson = new
                                    {
                                        schema = new { type = "object" },
                                        example = JsonSerializer.Deserialize<JsonElement>(e.ResponseExample)
                                    }
                                }
                            }
                        }
                    }
                )
            )
        };
        
        var json = JsonSerializer.Serialize(spec, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        return await Task.FromResult(new ResourceContent
        {
            Uri = "api://openapi",
            MimeType = "application/json",
            Text = json
        });
    }
    
    private class ApiEndpoint
    {
        public required string Method { get; init; }
        public required string Path { get; init; }
        public required string Description { get; init; }
        public required string[] Parameters { get; init; }
        public string? RequestExample { get; init; }
        public required string ResponseExample { get; init; }
    }
}