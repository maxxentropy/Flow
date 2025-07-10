# MCP Server - Model Context Protocol Implementation

A production-ready implementation of the Model Context Protocol (MCP) server in C#/.NET 8, featuring both stdio and Server-Sent Events (SSE) transports.

## Features

- **Full MCP Protocol Compliance**: Implements the complete MCP specification v0.1.0
- **Dual Transport Support**: 
  - stdio transport for CLI and development scenarios
  - Server-Sent Events (SSE) transport for web-based clients
- **Clean Architecture**: Organized in layers following Domain-Driven Design principles
- **Extensible Design**: Easy to add custom tools, resources, and prompts
- **Production Ready**: Includes logging, error handling, and Docker support
- **Sample Implementations**: Built-in example tools and file system resource provider

## Quick Start

### Running with .NET CLI

#### Console Mode (stdio transport)
```bash
cd src/McpServer.Console
dotnet run
```

#### Web Mode (SSE transport)
```bash
cd src/McpServer.Web
dotnet run
```

The SSE endpoint will be available at `http://localhost:8080/sse`

### Running with Docker

#### Web Server (SSE)
```bash
docker-compose up mcpserver-web
```

#### Console Mode
```bash
docker-compose --profile console up mcpserver-console
```

## Architecture

The solution follows Clean Architecture principles:

```
McpServer/
├── Domain/           # Core business logic and protocol models
├── Application/      # Use cases and orchestration
├── Infrastructure/   # Transport implementations and external services
├── Web/              # ASP.NET Core host for SSE transport
├── Console/          # Console host for stdio transport
└── Abstractions/     # Shared abstractions and DI configuration
```

## Configuration

### Console Application (appsettings.json)
```json
{
  "McpServer": {
    "Name": "MCP Server Console",
    "Version": "1.0.0",
    "Transport": {
      "Stdio": {
        "Enabled": true,
        "BufferSize": 4096,
        "Timeout": "00:05:00"
      }
    }
  }
}
```

### Web Application (appsettings.json)
```json
{
  "McpServer": {
    "Transport": {
      "Sse": {
        "Enabled": true,
        "Path": "/sse",
        "AllowedOrigins": ["http://localhost:3000"],
        "RequireHttps": false,
        "ApiKey": null
      }
    }
  }
}
```

## Extending the Server

### Adding a Custom Tool

```csharp
public class MyCustomTool : ITool
{
    public string Name => "myTool";
    public string Description => "My custom tool";
    
    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["input"] = new { type = "string", description = "Input parameter" }
        },
        Required = new List<string> { "input" }
    };

    public Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
    {
        // Implementation
        return Task.FromResult(new ToolResult
        {
            Content = new List<ToolContent>
            {
                new TextContent { Text = "Result" }
            }
        });
    }
}
```

Register in DI:
```csharp
services.AddSingleton<ITool, MyCustomTool>();
```

### Adding a Custom Resource Provider

```csharp
public class MyResourceProvider : IResourceProvider
{
    public Task<IEnumerable<Resource>> ListResourcesAsync(CancellationToken cancellationToken)
    {
        // Implementation
    }

    public Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

## API Endpoints (Web Mode)

- `GET /` - Server information
- `GET /health` - Health check endpoint
- `POST /sse` - SSE connection endpoint for MCP communication
- `GET /swagger` - API documentation (development only)

## Built-in Tools

### Echo Tool
Echoes back the provided message.
```json
{
  "name": "echo",
  "arguments": {
    "message": "Hello, World!"
  }
}
```

### Calculator Tool
Performs basic arithmetic operations.
```json
{
  "name": "calculator",
  "arguments": {
    "operation": "add",
    "a": 10,
    "b": 5
  }
}
```

### DateTime Tool
Provides current date/time information.
```json
{
  "name": "datetime",
  "arguments": {
    "format": "yyyy-MM-dd HH:mm:ss",
    "timezone": "UTC"
  }
}
```

## Development

### Prerequisites
- .NET 8 SDK
- Docker (optional)

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Development with Docker
```bash
docker-compose -f docker-compose.dev.yml up
```

## Deployment

### Docker Deployment
```bash
# Build image
docker build -t mcpserver:latest .

# Run web server
docker run -p 8080:8080 mcpserver:latest

# Run console mode
docker run -it mcpserver:console
```

### Production Configuration

1. Set appropriate environment variables:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `McpServer__Transport__Sse__ApiKey=your-api-key`
   - `McpServer__Transport__Sse__RequireHttps=true`

2. Configure CORS for your client domains
3. Set up proper logging and monitoring
4. Configure SSL/TLS for HTTPS

## Security Considerations

- Enable API key authentication for SSE transport in production
- Use HTTPS for SSE connections
- Configure CORS appropriately
- Restrict file system access paths
- Implement rate limiting for tool executions
- Validate all inputs thoroughly

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please follow the existing code style and architecture patterns.