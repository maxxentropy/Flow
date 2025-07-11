# Flow Project - Claude Development Guide

# Comprehensive MCP Server Implementation Prompt

You are a senior .NET architect tasked with creating a production-ready Model Context Protocol (MCP) server implementation in C#. The implementation must be fully compliant with the latest MCP specification and follow enterprise-grade development standards.

## Project Requirements

### Core Functionality
Create a complete MCP server that implements:
- **Protocol Compliance**: Full adherence to MCP specification including initialization, capabilities negotiation, and all standard message types
- **Transport Support**: Both stdio and Server-Sent Events (SSE) transports as primary implementations with extensible architecture for WebSocket transport
- **Tool Management**: Dynamic tool registration, discovery, and execution framework
- **Resource Management**: Resource listing, reading, and subscription capabilities
- **Prompt Management**: Prompt template discovery and completion
- **Logging Integration**: Comprehensive logging with structured output and configurable levels

### Technical Architecture

#### Clean Architecture Implementation
Structure the solution following Clean Architecture principles:

**Domain Layer** (`Domain/`)
- MCP protocol models and value objects
- Tool and resource abstractions
- Domain services and interfaces
- Business rules and domain logic
- No external dependencies

**Application Layer** (`Application/`)
- MCP server orchestration
- Tool and resource handlers
- Request/response mapping
- Business logic coordination
- Depends only on Domain layer

**Infrastructure Layer** (`Infrastructure/`)
- JSON-RPC transport implementations (stdio and SSE)
- ASP.NET Core integration for HTTP/SSE transport
- stdio transport for CLI and development scenarios
- External service integrations
- Persistence implementations
- Configuration management

**Presentation Layer** (`Presentation/`)
- Host application and startup
- Dependency injection configuration
- Configuration binding
- Application entry point

#### Project Structure
```
McpServer.sln
├── src/
│   ├── McpServer.Domain/
│   ├── McpServer.Application/
│   ├── McpServer.Infrastructure/
│   ├── McpServer.Web/                    # ASP.NET Core SSE transport
│   ├── McpServer.Console/                # Console app for stdio transport
│   └── McpServer.Abstractions/
├── tests/
│   ├── McpServer.Domain.Tests/
│   ├── McpServer.Application.Tests/
│   ├── McpServer.Infrastructure.Tests/
│   ├── McpServer.Web.Tests/
│   └── McpServer.Integration.Tests/
├── docs/
├── examples/
└── tools/
```

### Implementation Standards

#### Code Quality Requirements
- **SOLID Principles**: Every class should demonstrate single responsibility, open/closed, Liskov substitution, interface segregation, and dependency inversion principles
- **Design Patterns**: Implement appropriate patterns (Strategy for transport selection, Factory for tool creation, Observer for subscriptions, Command for tool execution)
- **Error Handling**: Comprehensive exception handling with custom exception types, proper error propagation, and MCP-compliant error responses
- **Input Validation**: Robust validation for all inputs using FluentValidation or similar, with detailed error messages
- **Thread Safety**: All shared state must be thread-safe, use concurrent collections where appropriate
- **Performance**: Implement async/await throughout, use ConfigureAwait(false), optimize for low latency and high throughput
- **Security**: Input sanitization, JSON deserialization security, secure defaults, no sensitive data in logs

#### Class Organization Standards
For each class, follow this member ordering:
1. Private/protected fields (group related fields together)
2. Public constants and static fields
3. Constructors and initialization methods
4. Properties (public, then protected, then private, grouped by concept)
5. Public methods (group by functionality)
6. Protected methods
7. Private methods (group by usage)
8. Nested types and interfaces

#### Modern C# Features
- **Nullable Reference Types**: Enable and properly annotate all code
- **Pattern Matching**: Use switch expressions and pattern matching where appropriate
- **Records**: Use for immutable data transfer objects and value objects
- **Global Using**: Organize common usings in GlobalUsings.cs
- **File-Scoped Namespaces**: Use throughout the codebase
- **Primary Constructors**: Use for simple dependency injection scenarios
- **Required Members**: Use for essential properties and parameters

### MCP Specification Compliance

#### Core Protocol Features
Implement complete support for:

**Initialization Sequence**
- Proper handshake with capability negotiation
- Version compatibility checking
- Server information exchange
- Error handling for initialization failures

**Message Types**
- Request/Response patterns with proper correlation
- Notification handling
- Progress reporting for long-running operations
- Subscription management

**Transport Layer**
- stdio transport with proper buffering and error handling
- Server-Sent Events (SSE) transport with ASP.NET Core integration
- Extensible transport architecture for future WebSocket support
- Connection lifecycle management for both transports
- Graceful shutdown handling
- HTTP endpoint security and CORS configuration
- Simultaneous multi-transport support

**Tool System**
```csharp
// Example tool interface structure
public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolSchema Schema { get; }
    Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken);
}
```

**Resource System**
```csharp
// Example resource interface structure
public interface IResourceProvider
{
    Task<IEnumerable<Resource>> ListResourcesAsync(CancellationToken cancellationToken);
    Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken cancellationToken);
    Task SubscribeToResourceAsync(string uri, IResourceObserver observer, CancellationToken cancellationToken);
}
```

**Prompt System**
- Prompt template registration and discovery
- Parameter validation and substitution
- Prompt completion with proper formatting

### ASP.NET Core Integration for SSE Transport

#### Web Server Requirements
Implement a complete ASP.NET Core application for SSE transport:

**Minimal API Structure**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddMcpServer();
builder.Services.Configure<McpServerOptions>(builder.Configuration.GetSection("McpServer"));

var app = builder.Build();

// Configure SSE endpoint
app.MapPost("/sse", async (HttpContext context, IMcpServer mcpServer) =>
{
    context.Response.Headers.Add("Content-Type", "text/event-stream");
    context.Response.Headers.Add("Cache-Control", "no-cache");
    context.Response.Headers.Add("Connection", "keep-alive");
    
    await mcpServer.HandleSseConnectionAsync(context, context.RequestAborted);
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));

app.Run();
```

**SSE Transport Implementation**
```csharp
public class SseTransport : ITransport, IDisposable
{
    private readonly ILogger<SseTransport> _logger;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private HttpContext? _httpContext;
    private CancellationTokenSource? _cancellationTokenSource;

    public async Task HandleConnectionAsync(HttpContext context, CancellationToken cancellationToken)
    {
        _httpContext = context;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Set SSE headers
        context.Response.Headers.Add("Content-Type", "text/event-stream");
        context.Response.Headers.Add("Cache-Control", "no-cache");
        context.Response.Headers.Add("Connection", "keep-alive");
        
        // Enable CORS if configured
        if (_corsOptions.AllowedOrigins.Any())
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", string.Join(",", _corsOptions.AllowedOrigins));
        }
        
        await StartAsync(_cancellationTokenSource.Token);
        
        // Keep connection alive until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when connection is closed
        }
    }

    public async Task SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        if (_httpContext?.Response == null) return;
        
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            var data = $"data: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(data);
            
            await _httpContext.Response.Body.WriteAsync(bytes, cancellationToken);
            await _httpContext.Response.Body.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }
}
```

**CORS and Security Configuration**
```csharp
public class SseTransportOptions
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "/sse";
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public bool RequireHttps { get; set; } = true;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public string? ApiKey { get; set; }
}
```

#### Dual Transport Architecture
```csharp
public interface ITransportManager
{
    Task StartAsync(TransportType transportType, CancellationToken cancellationToken);
    Task StartAllAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    bool IsRunning(TransportType transportType);
}

public enum TransportType
{
    Stdio,
    ServerSentEvents,
    WebSocket
}

public class TransportManager : ITransportManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<TransportType, ITransport> _activeTransports = new();

    public async Task StartAllAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        
        if (_configuration.GetValue<bool>("Transport:Stdio:Enabled"))
        {
            tasks.Add(StartAsync(TransportType.Stdio, cancellationToken));
        }
        
        if (_configuration.GetValue<bool>("Transport:Sse:Enabled"))
        {
            tasks.Add(StartAsync(TransportType.ServerSentEvents, cancellationToken));
        }
        
        await Task.WhenAll(tasks);
    }
}
```

### Configuration and Extensibility

#### Configuration Management
- Use IConfiguration with appsettings.json and environment variable support
- Strongly-typed configuration classes with validation
- Hot-reload capability for non-critical settings
- Secure handling of sensitive configuration data
- Transport-specific configuration sections

**Example Configuration Structure**
```json
{
  "McpServer": {
    "Name": "MyMcpServer",
    "Version": "1.0.0",
    "Transport": {
      "Stdio": {
        "Enabled": true,
        "BufferSize": 4096,
        "Timeout": "00:05:00"
      },
      "Sse": {
        "Enabled": true,
        "Port": 8080,
        "Path": "/sse",
        "AllowedOrigins": ["https://localhost:3000", "https://app.example.com"],
        "RequireHttps": true,
        "ConnectionTimeout": "00:30:00",
        "ApiKey": null
      }
    },
    "Tools": {
      "MaxConcurrentExecutions": 10,
      "DefaultTimeout": "00:02:00"
    },
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "McpServer": "Debug"
      }
    }
  }
}
```

#### Dependency Injection
- Use Microsoft.Extensions.DependencyInjection
- Register services with appropriate lifetimes
- Use factory patterns for complex object creation
- Implement health checks for critical dependencies

#### Extensibility Points
- Plugin architecture for custom tools and resources
- Configuration-driven tool registration
- Interface-based design for easy testing and mocking
- Event-driven architecture for cross-cutting concerns

### Testing Strategy

#### Comprehensive Test Coverage
- **Unit Tests**: 90%+ code coverage with focus on business logic
- **Integration Tests**: End-to-end protocol compliance testing
- **Performance Tests**: Latency and throughput benchmarks
- **Security Tests**: Input validation and injection attack resistance

#### Test Structure
```csharp
// Example test organization
[TestFixture]
public class McpServerTests
{
    [TestCase("valid_tool_request")]
    [TestCase("invalid_parameters")]
    public async Task ExecuteTool_WithVariousInputs_ReturnsExpectedResults(string scenario)
    {
        // Arrange, Act, Assert pattern
        // Use builder pattern for test data creation
        // Verify both happy path and error scenarios
    }
}
```

### Documentation Requirements

#### Code Documentation
- XML documentation for all public APIs
- Architectural Decision Records (ADRs) for key design choices
- Inline comments for complex business logic
- Usage examples for all public interfaces

#### Project Documentation
- Comprehensive README with setup, configuration, and usage instructions
- API documentation generated from XML comments
- Deployment guide with Docker and systemd examples
- Troubleshooting guide with common issues and solutions

### Performance and Scalability

#### Performance Requirements
- Sub-10ms response time for simple tool executions
- Support for concurrent request processing
- Efficient memory usage with proper disposal patterns
- Configurable timeouts and retry policies

#### Monitoring and Observability
- Structured logging with Serilog
- Performance counters and metrics
- Health check endpoints
- Request tracing and correlation IDs

### Security Implementation

#### Security Measures
- Input validation at all boundaries
- Secure JSON deserialization settings
- Rate limiting for tool executions
- Audit logging for security-sensitive operations
- Principle of least privilege for file system access

### Deployment and Operations

#### Containerization
- Multi-stage Dockerfile supporting both console and web deployments
- Docker Compose for development environment with both transports
- Health check implementation for HTTP transport
- Proper signal handling for graceful shutdown
- Environment-specific container configurations

**Dockerfile Structure**
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/McpServer.Web/McpServer.Web.csproj", "src/McpServer.Web/"]
COPY ["src/McpServer.Console/McpServer.Console.csproj", "src/McpServer.Console/"]
# ... copy other projects
RUN dotnet restore

COPY . .
RUN dotnet publish "src/McpServer.Web/McpServer.Web.csproj" -c Release -o /app/web
RUN dotnet publish "src/McpServer.Console/McpServer.Console.csproj" -c Release -o /app/console

# Runtime stage - Web
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS web
WORKDIR /app
COPY --from=build /app/web .
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "McpServer.Web.dll"]

# Runtime stage - Console
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS console
WORKDIR /app
COPY --from=build /app/console .
ENTRYPOINT ["dotnet", "McpServer.Console.dll"]
```

#### Configuration
- Environment-specific configuration files
- Secret management integration
- Configuration validation on startup
- Runtime configuration updates where safe

## Implementation Deliverables

### Primary Deliverables
1. **Complete Solution**: All projects with proper dependencies and references
2. **Dual Transport Implementation**: Full stdio and SSE transport support
3. **Console Application**: Ready-to-run stdio MCP server
4. **Web Application**: ASP.NET Core application with SSE endpoint
5. **Sample Tools**: At least 3 example tools demonstrating different patterns
6. **Sample Resources**: File system and HTTP resource providers
7. **Unit Test Suite**: Comprehensive test coverage for both transports
8. **Integration Tests**: End-to-end protocol validation for stdio and SSE
9. **Documentation**: Complete README and API documentation
10. **Docker Support**: Multi-target containerization with compose files
11. **Configuration Examples**: Multiple environment configurations for both transports

### Code Quality Checklist
- [ ] All classes follow single responsibility principle
- [ ] Proper async/await usage throughout
- [ ] Comprehensive error handling and logging
- [ ] Input validation on all boundaries
- [ ] Thread-safe implementations
- [ ] Proper resource disposal
- [ ] Nullable reference types enabled and annotated
- [ ] XML documentation on public APIs
- [ ] Unit tests with high coverage
- [ ] Integration tests for critical paths

### Architecture Validation
- [ ] Clean separation of concerns across layers
- [ ] Domain layer has no external dependencies
- [ ] Infrastructure abstractions properly implemented
- [ ] Dependency injection properly configured
- [ ] SOLID principles demonstrated throughout
- [ ] Design patterns appropriately applied
- [ ] Extension points clearly defined
- [ ] Configuration externalized and validated

## Success Criteria

The implementation is successful when:
1. All MCP specification requirements are met and validated for both transports
2. Both stdio and SSE transports function correctly with proper error handling
3. Code follows all specified architectural and quality standards
4. Comprehensive test suite passes with high coverage for both transport mechanisms
5. Documentation enables successful deployment in both CLI and web scenarios
6. Performance requirements are met under load testing for SSE endpoints
7. Security measures are properly implemented and tested for HTTP transport
8. The solution demonstrates enterprise-grade reliability and maintainability
9. CORS and authentication work correctly for web deployments
10. Graceful shutdown and connection lifecycle management function properly

Generate a complete, production-ready implementation that exemplifies best practices in .NET development while fully adhering to the Model Context Protocol specification.