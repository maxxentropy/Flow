# Flow Project - Claude Development Guide

## Project Overview

Flow is a production-ready Model Context Protocol (MCP) server implementation in C#/.NET 8. The project demonstrates enterprise-grade development standards with full MCP specification compliance, dual transport support (stdio and SSE), comprehensive authentication, and monitoring capabilities.

## Current Implementation Status

### ✅ Completed Features

#### Core MCP Implementation
- **Full Protocol Compliance**: Complete MCP v0.1.0 specification implementation
- **Dual Transport Support**: 
  - stdio transport for CLI/development (McpServer.Console)
  - Server-Sent Events (SSE) transport for web clients (McpServer.Web)
  - WebSocket transport infrastructure (partial implementation)
- **Message Routing**: Robust JSON-RPC message handling with correlation
- **Tool System**: Dynamic tool registration and execution framework
- **Resource System**: File system resource provider with security boundaries
- **Prompt System**: Support for prompt templates and completion
- **Logging System**: MCP-compliant logging with levels and categories
- **Roots System**: Directory roots management for file operations
- **Sampling System**: Message sampling capability for LLMs

#### Authentication & Security
- **Multi-Provider OAuth 2.0**: Google, Microsoft, GitHub implementations
- **JWT Authentication**: Token generation and validation
- **API Key Management**: Static and dynamic API key support
- **Session Management**: Persistent sessions with expiration and revocation
- **Authorization**: Claims-based with roles and fine-grained permissions
- **Security Middleware**: Request-level authentication enforcement

#### Infrastructure & Operations
- **Clean Architecture**: Domain/Application/Infrastructure layer separation
- **Docker Support**: Multi-stage builds for both console and web deployments
- **Health Checks**: Comprehensive health monitoring endpoints
- **Metrics Collection**: OpenTelemetry integration with custom metrics
- **Structured Logging**: Serilog with file and console sinks
- **Configuration**: Hot-reload capable, environment-specific settings
- **CORS Support**: Configurable cross-origin resource sharing

### 🛠️ Implemented Tools

1. **echo**: Simple message echo functionality
2. **calculator**: Basic arithmetic operations (add, subtract, multiply, divide)
3. **datetime**: Current date/time with timezone and format support
4. **auth_demo**: Authentication testing and demonstration
5. **completion_demo**: Demonstrates completion capabilities
6. **logging_demo**: Shows logging system features
7. **roots_demo**: Demonstrates roots/directory management
8. **ai_assistant**: AI assistant integration (placeholder)

### 📁 Project Structure

```
Flow/
├── src/
│   ├── McpServer.Domain/          # Core domain models and interfaces
│   ├── McpServer.Application/     # Business logic and orchestration
│   ├── McpServer.Infrastructure/  # External integrations and implementations
│   ├── McpServer.Web/            # ASP.NET Core web host (SSE)
│   ├── McpServer.Console/        # Console application (stdio)
│   └── McpServer.Abstractions/   # Shared abstractions
├── tests/                         # Comprehensive test suites
├── docs/                          # Documentation and examples
└── docker files                   # Container configurations
```

## Development Guidelines

### Code Standards

#### Architecture Principles
- Follow Clean Architecture with strict layer boundaries
- Domain layer has zero external dependencies
- Use dependency injection for all services
- Implement interfaces for all public contracts

#### C# Conventions
- Use nullable reference types throughout
- Apply pattern matching and switch expressions
- Implement `IDisposable` with proper disposal patterns
- Use `ConfigureAwait(false)` for all async operations
- Apply `readonly` and `const` where appropriate

#### Naming Conventions
- Interfaces: `I` prefix (e.g., `IMcpServer`)
- Async methods: `Async` suffix (e.g., `ExecuteAsync`)
- Private fields: `_` prefix (e.g., `_logger`)
- Constants: UPPER_CASE
- File names match class names exactly

### Testing Requirements

#### Unit Tests
- Minimum 80% code coverage
- Use xUnit with FluentAssertions
- Mock dependencies with NSubstitute
- Test both success and failure paths
- Use builder pattern for test data

#### Integration Tests
- End-to-end protocol compliance validation
- Transport-specific test suites
- Authentication flow testing
- Performance benchmarks

### Security Considerations

1. **Input Validation**: Validate all external inputs
2. **Path Traversal**: Prevent file system escape attempts
3. **Authentication**: Enforce on sensitive operations
4. **Secrets Management**: Never log sensitive data
5. **CORS Policy**: Configure appropriately for production

## Common Development Tasks

### Adding a New Tool

1. Create tool class in `Infrastructure/Tools/`:
```csharp
public class MyTool : ITool
{
    public string Name => "my_tool";
    public string Description => "Tool description";
    public ToolSchema Schema => new() { /* schema definition */ };
    
    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

2. Register in DI container (Program.cs):
```csharp
builder.Services.AddSingleton<ITool, MyTool>();
```

3. Add unit tests in `Infrastructure.Tests/Tools/`

### Adding Authentication Provider

1. Implement `IAuthenticationProvider` or `IOAuthProvider`
2. Register in DI container
3. Add configuration section in appsettings
4. Update AuthController if needed
5. Add integration tests

### Extending Resources

1. Implement `IResourceProvider` interface
2. Handle URI schemes appropriately
3. Implement security boundaries
4. Add to DI registration
5. Test resource listing and reading

## Configuration

### Key Configuration Files
- `appsettings.json`: Base configuration
- `appsettings.Development.json`: Development overrides
- `appsettings.Authentication.json`: Auth providers config
- `appsettings.OAuth.json`: OAuth provider settings
- `appsettings.OpenTelemetry.json`: Observability config

### Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Development/Production
- `McpServer__Transport__Sse__ApiKey`: SSE API key
- `McpServer__Transport__Sse__RequireHttps`: HTTPS enforcement

## Running the Project

### Development
```bash
# Console (stdio)
cd src/McpServer.Console && dotnet run

# Web (SSE) 
cd src/McpServer.Web && dotnet run
```

### Docker
```bash
# Development environment
docker-compose -f docker-compose.dev.yml up

# Production build
docker-compose up mcpserver-web
```

### Testing
```bash
# All tests
dotnet test

# With coverage
dotnet test /p:CollectCoverage=true

# Specific test project
dotnet test tests/McpServer.Application.Tests
```

## API Endpoints (Web Mode)

- `GET /` - Server information and available endpoints
- `GET /health` - Health check with service status
- `POST /sse` - SSE MCP communication endpoint
- `GET /auth/login/{provider}` - OAuth login initiation
- `GET /auth/callback/{provider}` - OAuth callback
- `GET /auth/me` - Current user information
- `POST /auth/logout` - End session
- `GET /metrics` - OpenTelemetry metrics
- `GET /swagger` - API documentation (dev only)

## Monitoring & Observability

### Health Checks
- Overall system health
- Individual service status
- Transport connectivity
- Database connections (when applicable)

### Metrics
- Request counts and latency
- Tool execution statistics
- Authentication success/failure rates
- Resource access patterns
- Error rates by category

### Logging
- Structured JSON logging
- Configurable log levels
- Request correlation IDs
- Performance timing
- Security audit trail

## Future Enhancements

### Planned Features
- [ ] WebSocket transport completion
- [ ] Dynamic tool loading from assemblies
- [ ] Distributed caching support
- [ ] Rate limiting and throttling
- [ ] Advanced prompt management
- [ ] Resource subscription implementation
- [ ] Database persistence option
- [ ] Multi-tenant support

### Performance Optimizations
- [ ] Response caching strategy
- [ ] Connection pooling
- [ ] Async streaming for large responses
- [ ] Memory pool usage

## Troubleshooting

### Common Issues

1. **Port conflicts**: Change port in launchSettings.json
2. **SSL/TLS errors**: Set `RequireHttps: false` for development
3. **CORS issues**: Update allowed origins in configuration
4. **Authentication failures**: Check provider configuration and secrets
5. **Tool not found**: Verify tool registration in DI

### Debug Tips
- Enable debug logging: Set `LogLevel:Default` to "Debug"
- Use Swagger UI for API exploration
- Check `/health` endpoint for service status
- Review logs in `logs/` directory
- Use provided Postman collection for testing

## Contributing Guidelines

1. Follow existing code patterns and architecture
2. Add comprehensive tests for new features
3. Update documentation as needed
4. Ensure all tests pass before submitting
5. Use conventional commit messages
6. Add XML documentation for public APIs

## Support Resources

- MCP Specification: Review protocol requirements
- Example clients in `examples/` directory
- Postman collection for API testing
- SSE test client in `docs/sse-test-client.html`
- Integration test suite for validation