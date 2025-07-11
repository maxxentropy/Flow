# MCP Server Codebase Index

## Quick Navigation Guide

This index provides fast access to key components of the MCP Server implementation. Use Ctrl+F to quickly find what you need.

## 🏗️ Project Structure Overview

```
McpServer.sln
├── src/
│   ├── McpServer.Domain/          # Core domain models and interfaces
│   ├── McpServer.Application/     # Business logic and orchestration
│   ├── McpServer.Infrastructure/  # External integrations and implementations
│   ├── McpServer.Web/            # ASP.NET Core web host (SSE transport)
│   ├── McpServer.Console/        # Console host (stdio transport)
│   └── McpServer.Abstractions/   # Shared abstractions and extensions
├── tests/
│   ├── McpServer.Domain.Tests/
│   ├── McpServer.Application.Tests/
│   ├── McpServer.Infrastructure.Tests/
│   ├── McpServer.Integration.Tests/
│   └── McpServer.Web.Tests/
├── docs/                         # Documentation
├── examples/                     # Example clients and usage
└── postman/                     # API testing collections
```

## 🔑 Key Classes and Locations

### Core Server Components

| Component | Location | Description |
|-----------|----------|-------------|
| `IMcpServer` | `src/McpServer.Application/Server/IMcpServer.cs` | Main server interface |
| `MultiplexingMcpServer` | `src/McpServer.Application/Server/MultiplexingMcpServer.cs` | Connection multiplexing implementation |
| `MessageRouter` | `src/McpServer.Application/Server/MessageRouter.cs` | Routes messages to handlers |
| `ConnectionAwareMessageRouter` | `src/McpServer.Application/Server/ConnectionAwareMessageRouter.cs` | Connection-aware routing |
| `ConnectionManager` | `src/McpServer.Application/Connection/ConnectionManager.cs` | Manages client connections |

### Transport Layer

| Transport Type | Location | Description |
|----------------|----------|-------------|
| `ITransport` | `src/McpServer.Domain/Transport/ITransport.cs` | Transport interface |
| `StdioTransport` | `src/McpServer.Infrastructure/Transport/StdioTransport.cs` | Console I/O transport |
| `SseTransport` | `src/McpServer.Infrastructure/Transport/SseTransport.cs` | Server-Sent Events transport |
| `WebSocketTransport` | `src/McpServer.Infrastructure/Transport/WebSocketTransport.cs` | WebSocket transport |
| `TransportManager` | `src/McpServer.Infrastructure/Transport/TransportManager.cs` | Manages multiple transports |

### Message Handlers

| Handler | Location | Purpose |
|---------|----------|---------|
| `InitializeHandler` | `src/McpServer.Application/Handlers/InitializeHandler.cs` | Protocol initialization |
| `ToolsHandler` | `src/McpServer.Application/Handlers/ToolsHandler.cs` | Tool discovery and execution |
| `ResourcesHandler` | `src/McpServer.Application/Handlers/ResourcesHandler.cs` | Resource management |
| `PromptsHandler` | `src/McpServer.Application/Handlers/PromptsHandler.cs` | Prompt handling |
| `CompletionHandler` | `src/McpServer.Application/Handlers/CompletionHandler.cs` | Completion requests |
| `LoggingHandler` | `src/McpServer.Application/Handlers/LoggingHandler.cs` | Logging configuration |
| `SamplingHandler` | `src/McpServer.Application/Handlers/SamplingHandler.cs` | Sampling operations |
| `RootsHandler` | `src/McpServer.Application/Handlers/RootsHandler.cs` | Root directory management |

### Tools System

| Component | Location | Description |
|-----------|----------|-------------|
| `ITool` | `src/McpServer.Domain/Tools/ITool.cs` | Tool interface |
| `IToolRegistry` | `src/McpServer.Application/Services/IToolRegistry.cs` | Tool registration |
| `ToolRegistry` | `src/McpServer.Application/Services/ToolRegistry.cs` | Tool registry implementation |
| **Sample Tools** | | |
| `CalculatorTool` | `src/McpServer.Infrastructure/Tools/CalculatorTool.cs` | Basic calculator |
| `DateTimeTool` | `src/McpServer.Infrastructure/Tools/DateTimeTool.cs` | Date/time operations |
| `EchoTool` | `src/McpServer.Infrastructure/Tools/EchoTool.cs` | Echo demonstration |
| `DataProcessingTool` | `src/McpServer.Infrastructure/Tools/DataProcessingTool.cs` | Data processing example |
| `AiAssistantTool` | `src/McpServer.Infrastructure/Tools/AiAssistantTool.cs` | AI assistant integration |

### Resources System

| Component | Location | Description |
|-----------|----------|-------------|
| `IResource` | `src/McpServer.Domain/Resources/IResource.cs` | Resource interface |
| `IResourceRegistry` | `src/McpServer.Application/Services/IResourceRegistry.cs` | Resource registration |
| `ResourceRegistry` | `src/McpServer.Application/Services/ResourceRegistry.cs` | Resource registry implementation |
| **Resource Providers** | | |
| `FileSystemResourceProvider` | `src/McpServer.Infrastructure/Resources/FileSystemResourceProvider.cs` | File system resources |
| `DatabaseSchemaResourceProvider` | `src/McpServer.Infrastructure/Resources/DatabaseSchemaResourceProvider.cs` | Database schema resources |
| `RestApiResourceProvider` | `src/McpServer.Infrastructure/Resources/RestApiResourceProvider.cs` | REST API resources |
| `TemplateResourceProvider` | `src/McpServer.Application/Resources/TemplateResourceProvider.cs` | Template-based resources |

### Authentication & Security

| Component | Location | Description |
|-----------|----------|-------------|
| `IAuthenticationService` | `src/McpServer.Domain/Security/IAuthenticationService.cs` | Auth service interface |
| `AuthenticationService` | `src/McpServer.Application/Services/AuthenticationService.cs` | Auth service implementation |
| `AuthenticationMiddleware` | `src/McpServer.Application/Middleware/AuthenticationMiddleware.cs` | Auth middleware |
| **Auth Providers** | | |
| `ApiKeyAuthenticationProvider` | `src/McpServer.Infrastructure/Security/ApiKeyAuthenticationProvider.cs` | API key auth |
| `JwtAuthenticationProvider` | `src/McpServer.Infrastructure/Security/JwtAuthenticationProvider.cs` | JWT auth |
| `OAuthAuthenticationProvider` | `src/McpServer.Infrastructure/Security/OAuthAuthenticationProvider.cs` | OAuth wrapper |
| **OAuth Providers** | | |
| `GitHubOAuthProvider` | `src/McpServer.Infrastructure/Security/OAuth/GitHubOAuthProvider.cs` | GitHub OAuth |
| `GoogleOAuthProvider` | `src/McpServer.Infrastructure/Security/OAuth/GoogleOAuthProvider.cs` | Google OAuth |
| `MicrosoftOAuthProvider` | `src/McpServer.Infrastructure/Security/OAuth/MicrosoftOAuthProvider.cs` | Microsoft OAuth |

### Caching System

| Component | Location | Description |
|-----------|----------|-------------|
| `ICacheService` | `src/McpServer.Application/Caching/ICacheService.cs` | Cache service interface |
| `MemoryCacheService` | `src/McpServer.Application/Caching/MemoryCacheService.cs` | In-memory cache |
| `DistributedCacheService` | `src/McpServer.Application/Caching/DistributedCacheService.cs` | Distributed cache |
| `ToolResultCache` | `src/McpServer.Application/Caching/ToolResultCache.cs` | Tool result caching |
| `ResourceContentCache` | `src/McpServer.Application/Caching/ResourceContentCache.cs` | Resource content caching |

### High Availability & Resilience

| Component | Location | Description |
|-----------|----------|-------------|
| `ICircuitBreaker` | `src/McpServer.Application/HighAvailability/ICircuitBreaker.cs` | Circuit breaker interface |
| `CircuitBreaker` | `src/McpServer.Application/HighAvailability/CircuitBreaker.cs` | Circuit breaker implementation |
| `IRetryPolicy` | `src/McpServer.Application/HighAvailability/IRetryPolicy.cs` | Retry policy interface |
| `RetryPolicy` | `src/McpServer.Application/HighAvailability/RetryPolicy.cs` | Retry policy implementation |
| `IFailoverManager` | `src/McpServer.Application/HighAvailability/IFailoverManager.cs` | Failover management |
| `FailoverManager` | `src/McpServer.Application/HighAvailability/FailoverManager.cs` | Failover implementation |

### Protocol & Messages

| Component | Location | Description |
|-----------|----------|-------------|
| **JSON-RPC** | | |
| `JsonRpcRequest` | `src/McpServer.Domain/Protocol/JsonRpc/JsonRpcRequest.cs` | JSON-RPC request |
| `JsonRpcResponse` | `src/McpServer.Domain/Protocol/JsonRpc/JsonRpcResponse.cs` | JSON-RPC response |
| `JsonRpcError` | `src/McpServer.Domain/Protocol/JsonRpc/JsonRpcError.cs` | JSON-RPC error |
| `McpErrorCodes` | `src/McpServer.Domain/Protocol/JsonRpc/McpErrorCodes.cs` | MCP error codes |
| **Protocol Messages** | | |
| `InitializeRequest` | `src/McpServer.Domain/Protocol/Messages/InitializeRequest.cs` | Init request |
| `InitializeResponse` | `src/McpServer.Domain/Protocol/Messages/InitializeResponse.cs` | Init response |
| `ToolsListResponse` | `src/McpServer.Domain/Protocol/Messages/ToolsListResponse.cs` | Tools list |
| `CompletionMessages` | `src/McpServer.Domain/Protocol/Messages/CompletionMessages.cs` | Completion messages |
| `LoggingMessages` | `src/McpServer.Domain/Protocol/Messages/LoggingMessages.cs` | Logging messages |
| `ProgressMessages` | `src/McpServer.Domain/Protocol/Messages/ProgressMessages.cs` | Progress messages |

### Validation

| Component | Location | Description |
|-----------|----------|-------------|
| `IValidationService` | `src/McpServer.Domain/Validation/IValidationService.cs` | Validation interface |
| `ValidationService` | `src/McpServer.Application/Services/ValidationService.cs` | Validation service |
| `McpMessageValidatorFactory` | `src/McpServer.Domain/Validation/FluentValidators/McpMessageValidatorFactory.cs` | Validator factory |
| **Validators** | | |
| `JsonRpcRequestValidator` | `src/McpServer.Domain/Validation/FluentValidators/JsonRpcRequestValidator.cs` | JSON-RPC validation |
| `InitializeRequestValidator` | `src/McpServer.Domain/Validation/FluentValidators/InitializeRequestValidator.cs` | Init validation |
| `ToolsCallRequestValidator` | `src/McpServer.Domain/Validation/FluentValidators/ToolsCallRequestValidator.cs` | Tool call validation |

### Web API Controllers

| Controller | Location | Purpose |
|------------|----------|---------|
| `AuthController` | `src/McpServer.Web/Controllers/AuthController.cs` | Authentication endpoints |
| `HealthController` | `src/McpServer.Web/Controllers/HealthController.cs` | Health checks |
| `MetricsController` | `src/McpServer.Web/Controllers/MetricsController.cs` | Metrics endpoints |
| `MonitoringController` | `src/McpServer.Web/Controllers/MonitoringController.cs` | Monitoring endpoints |
| `ProfileController` | `src/McpServer.Web/Controllers/ProfileController.cs` | User profile management |

## 📁 Important Configuration Files

### Application Settings

| File | Location | Purpose |
|------|----------|---------|
| **Main Configuration** | | |
| `appsettings.json` | `src/McpServer.Web/` | Web host configuration |
| `appsettings.json` | `src/McpServer.Console/` | Console host configuration |
| **Feature-Specific** | | |
| `appsettings.Authentication.json` | Root directory | Authentication settings |
| `appsettings.OAuth.json` | Root directory | OAuth provider settings |
| `appsettings.ConnectionMultiplexing.json` | Multiple locations | Connection multiplexing |
| `appsettings.RateLimiting.json` | `src/McpServer.Web/` | Rate limiting configuration |
| `appsettings.ProtocolVersion.json` | `src/McpServer.Web/` | Protocol version settings |
| `appsettings.OpenTelemetry.json` | Root directory | OpenTelemetry configuration |
| `appsettings.Sessions.json` | Root directory | Session management |

### Build Configuration

| File | Location | Purpose |
|------|----------|---------|
| `Directory.Build.props` | Root directory | Global build properties |
| `global.json` | Root directory | .NET SDK version |
| `McpServer.sln` | Root directory | Solution file |

### Docker Configuration

| File | Location | Purpose |
|------|----------|---------|
| `Dockerfile` | Root directory | Production Docker image |
| `Dockerfile.dev` | Root directory | Development Docker image |
| `docker-compose.yml` | Root directory | Production compose |
| `docker-compose.dev.yml` | Root directory | Development compose |

## 🔍 Common Search Patterns

### Finding Implementations

```
# Find all tool implementations
Search: "class.*Tool.*:.*ITool"
Location: src/McpServer.Infrastructure/Tools/

# Find all handlers
Search: "Handler.*:.*IMessageHandler"
Location: src/McpServer.Application/Handlers/

# Find all resource providers
Search: "ResourceProvider.*:.*IResource"
Location: src/McpServer.Infrastructure/Resources/

# Find all validators
Search: "Validator.*:.*AbstractValidator"
Location: src/McpServer.Domain/Validation/FluentValidators/
```

### Finding Interfaces

```
# Core interfaces
Search: "interface I[A-Z]"
Primary locations:
- src/McpServer.Domain/
- src/McpServer.Application/Services/
- src/McpServer.Application/Server/

# Service interfaces
Search: "interface I.*Service"
Location: src/McpServer.Application/Services/

# Repository interfaces
Search: "interface I.*Repository"
Location: src/McpServer.Domain/Security/
```

### Finding Configuration

```
# Find where a setting is used
Search: "Configuration\[\".*\"\]"
Search: "GetValue<.*>\(\".*\"\)"
Search: "Configure<.*>"

# Find dependency injection
Search: "services\.Add"
Location: src/McpServer.*/Program.cs
Location: src/McpServer.Abstractions/ServiceCollectionExtensions.cs
```

## 🔗 Dependency Flow

### Layer Dependencies

```
McpServer.Web / McpServer.Console
    ↓ depends on
McpServer.Application
    ↓ depends on
McpServer.Domain
    ↑ implemented by
McpServer.Infrastructure
    ↓ depends on
McpServer.Domain

McpServer.Abstractions (shared by all)
```

### Key Dependency Chains

1. **Request Processing Flow**
   ```
   Transport (Infrastructure) 
   → MessageRouter (Application) 
   → Handler (Application) 
   → Service (Application)
   → Tool/Resource (Infrastructure)
   ```

2. **Authentication Flow**
   ```
   AuthController (Web)
   → AuthenticationService (Application)
   → AuthenticationProvider (Infrastructure)
   → UserRepository (Infrastructure)
   ```

3. **Tool Execution Flow**
   ```
   ToolsHandler (Application)
   → ToolRegistry (Application)
   → ValidatedToolWrapper (Application)
   → ITool Implementation (Infrastructure)
   → ToolResultCache (Application)
   ```

## 🛠️ Quick Access to Common Tasks

### Adding a New Tool
1. Create tool class in `src/McpServer.Infrastructure/Tools/`
2. Implement `ITool` interface
3. Register in `Program.cs` or via configuration

### Adding a New Resource Provider
1. Create provider in `src/McpServer.Infrastructure/Resources/`
2. Implement `IResourceProvider` interface
3. Register in dependency injection

### Adding a New Handler
1. Create handler in `src/McpServer.Application/Handlers/`
2. Implement `IMessageHandler<TRequest, TResponse>`
3. Handler auto-discovered via DI scanning

### Adding a New Validator
1. Create validator in `src/McpServer.Domain/Validation/FluentValidators/`
2. Inherit from `AbstractValidator<T>`
3. Register in `McpMessageValidatorFactory`

### Adding OAuth Provider
1. Create provider in `src/McpServer.Infrastructure/Security/OAuth/`
2. Inherit from `BaseOAuthProvider`
3. Configure in `appsettings.OAuth.json`

## 📚 Testing Structure

| Test Type | Location | Focus |
|-----------|----------|-------|
| Unit Tests | `tests/McpServer.*.Tests/` | Individual component testing |
| Integration Tests | `tests/McpServer.Integration.Tests/` | Cross-component testing |
| Handler Tests | `tests/McpServer.Application.Tests/Handlers/` | Message handler testing |
| Service Tests | `tests/McpServer.Application.Tests/Services/` | Service layer testing |
| Transport Tests | `tests/McpServer.Infrastructure.Tests/Transport/` | Transport testing |

## 🔧 Entry Points

| Application | Entry Point | Purpose |
|-------------|-------------|---------|
| Web Host | `src/McpServer.Web/Program.cs` | ASP.NET Core SSE/WebSocket host |
| Console Host | `src/McpServer.Console/Program.cs` | Stdio transport host |

## 📖 Documentation Links

| Document | Location | Description |
|----------|----------|-------------|
| Main README | `README.md` | Project overview and setup |
| Claude Context | `.claude.md` | Claude-specific instructions and context |
| Claude Spec | `CLAUDE.md` | Development specifications |
| **Component Indexes** | | |
| Transport Layer | `docs/INDEX_TRANSPORT.md` | Transport implementations guide |
| Message Handlers | `docs/INDEX_HANDLERS.md` | Handler patterns and details |
| Security System | `docs/INDEX_SECURITY.md` | Authentication and security |
| Tools System | `docs/INDEX_TOOLS.md` | Tool development guide |
| **Development Guides** | | |
| Common Patterns | `docs/COMMON_PATTERNS.md` | Code patterns and snippets |
| Performance Notes | `docs/PERFORMANCE_NOTES.md` | Performance optimization guide |
| Quick Fixes | `docs/QUICK_FIXES.md` | Common issue solutions |
| **Setup Guides** | | |
| OAuth Setup | `docs/OAuth-Setup.md` | OAuth configuration guide |
| Postman Testing | `docs/postman-testing.md` | API testing guide |
| Compliance Review | `docs/COMPLIANCE_AND_QUALITY_REVIEW.md` | Quality checklist |

## 🚀 Quick Commands

```bash
# Run web server (SSE transport)
cd src/McpServer.Web && dotnet run

# Run console server (stdio transport)
cd src/McpServer.Console && dotnet run

# Run all tests
dotnet test

# Build solution
dotnet build

# Run with Docker
docker-compose up

# Development mode
docker-compose -f docker-compose.dev.yml up
```