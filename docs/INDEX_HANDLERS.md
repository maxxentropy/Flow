# Message Handlers Index

## Overview
Message handlers process incoming MCP protocol messages. Each handler is responsible for a specific message type and follows a consistent pattern for request processing, validation, and response generation.

## Handler Architecture

### Base Handler Pattern
All handlers implement `IMessageHandler<TRequest, TResponse>`:
```csharp
public interface IMessageHandler<TRequest, TResponse>
{
    string Method { get; }
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
```

### Handler Discovery
- Handlers are auto-discovered via reflection
- Registered by `Method` property matching JSON-RPC method name
- Singleton lifecycle for stateless operation

## Core Handlers

### InitializeHandler (`src/McpServer.Application/Handlers/InitializeHandler.cs`)
- **Method**: `initialize`
- **Purpose**: Establishes client connection and negotiates capabilities
- **Key Responsibilities**:
  - Protocol version negotiation
  - Capability exchange
  - Connection registration
  - Server info response
- **Related**: `InitializedHandler`, `ProtocolVersionNegotiator`

### ToolsHandler (`src/McpServer.Application/Handlers/ToolsHandler.cs`)
- **Method**: `tools/list`, `tools/call`
- **Purpose**: Tool discovery and execution
- **Key Features**:
  - Dynamic tool listing
  - Parameter validation
  - Async tool execution
  - Progress reporting support
- **Related**: `IToolRegistry`, `ITool`, `ValidatedToolWrapper`

### ResourcesHandler (`src/McpServer.Application/Handlers/ResourcesHandler.cs`)
- **Method**: `resources/list`, `resources/read`, `resources/subscribe`
- **Purpose**: Resource management and access
- **Key Features**:
  - Resource discovery
  - Content reading with caching
  - Subscription management
  - Template support
- **Related**: `IResourceRegistry`, `IResourceProvider`, `ResourceContentCache`

### CompletionHandler (`src/McpServer.Application/Handlers/CompletionHandler.cs`)
- **Method**: `completion/complete`
- **Purpose**: Auto-completion support
- **Key Features**:
  - Context-aware suggestions
  - Multi-provider support
  - Caching for performance
  - Partial match algorithms
- **Related**: `ICompletionService`, `CompletionProvider`

### PromptsHandler (`src/McpServer.Application/Handlers/PromptsHandler.cs`)
- **Method**: `prompts/list`, `prompts/get`
- **Purpose**: Prompt template management
- **Key Features**:
  - Template discovery
  - Parameter substitution
  - Validation of arguments
  - Dynamic prompt generation
- **Related**: `IPromptRegistry`, `IPrompt`

### LoggingHandler (`src/McpServer.Application/Handlers/LoggingHandler.cs`)
- **Method**: `logging/setLevel`
- **Purpose**: Dynamic logging configuration
- **Key Features**:
  - Per-connection log levels
  - Runtime configuration changes
  - Structured logging support
  - Log filtering
- **Related**: `ILoggingService`, `LogLevel`

### SamplingHandler (`src/McpServer.Application/Handlers/SamplingHandler.cs`)
- **Method**: `sampling/createMessage`
- **Purpose**: Message sampling for AI models
- **Key Features**:
  - Message creation from samples
  - Model-specific formatting
  - Token estimation
  - Context management
- **Related**: `ISamplingService`, `SamplingMessage`

### RootsHandler (`src/McpServer.Application/Handlers/RootsHandler.cs`)
- **Method**: `roots/list`
- **Purpose**: Root directory management
- **Key Features**:
  - File system root discovery
  - URI formatting
  - Access control
  - Platform-specific handling
- **Related**: `IRootRegistry`, `Root`

### PingHandler (`src/McpServer.Application/Handlers/PingHandler.cs`)
- **Method**: `ping`
- **Purpose**: Connection health check
- **Simple echo response**
- **No authentication required**
- **Used for keep-alive**

### CancelHandler (`src/McpServer.Application/Handlers/CancelHandler.cs`)
- **Method**: `$/cancelRequest`
- **Purpose**: Request cancellation
- **Key Features**:
  - Cancellation token propagation
  - In-flight request tracking
  - Graceful operation abort
  - Resource cleanup

## Handler Lifecycle

### Request Processing Flow
1. **Message Reception**: Transport layer receives JSON-RPC message
2. **Routing**: MessageRouter identifies handler by method name
3. **Validation**: Request structure and parameters validated
4. **Authentication**: Security checks if required
5. **Execution**: Handler processes request
6. **Response**: Result or error returned to client

### Error Handling Pattern
```csharp
try
{
    // Validate request
    var validationResult = await _validator.ValidateAsync(request);
    if (!validationResult.IsValid)
        throw new McpException(McpErrorCodes.InvalidParams, validationResult.ToString());
    
    // Process request
    var result = await ProcessAsync(request);
    return result;
}
catch (McpException)
{
    throw; // Re-throw MCP exceptions
}
catch (Exception ex)
{
    _logger.LogError(ex, "Handler error");
    throw new McpException(McpErrorCodes.InternalError, "Internal server error");
}
```

## Handler Dependencies

### Common Dependencies
- **ILogger**: Structured logging
- **IValidator**: Request validation
- **IMetricsService**: Performance tracking
- **ICacheService**: Response caching
- **IConnectionManager**: Connection state

### Service Injection Pattern
```csharp
public class ExampleHandler : IMessageHandler<ExampleRequest, ExampleResponse>
{
    private readonly ILogger<ExampleHandler> _logger;
    private readonly IExampleService _service;
    private readonly IValidator<ExampleRequest> _validator;
    
    public ExampleHandler(
        ILogger<ExampleHandler> logger,
        IExampleService service,
        IValidator<ExampleRequest> validator)
    {
        _logger = logger;
        _service = service;
        _validator = validator;
    }
}
```

## Performance Considerations

### Caching Strategies
- Tool results cached by parameters
- Resource contents cached by URI
- Completion suggestions cached by prefix
- Cache invalidation on updates

### Async Best Practices
- Use `ConfigureAwait(false)` for library code
- Avoid blocking calls in handlers
- Implement cancellation properly
- Stream large responses when possible

### Concurrency Handling
- Handlers are stateless and thread-safe
- Use concurrent collections where needed
- Implement proper locking for shared state
- Consider rate limiting for expensive operations

## Testing Handlers

### Unit Test Pattern
```csharp
[Test]
public async Task HandleAsync_ValidRequest_ReturnsExpectedResponse()
{
    // Arrange
    var handler = new ExampleHandler(logger, mockService, validator);
    var request = new ExampleRequest { /* ... */ };
    
    // Act
    var response = await handler.HandleAsync(request, CancellationToken.None);
    
    // Assert
    Assert.NotNull(response);
    Assert.AreEqual(expected, response.Result);
}
```

### Integration Test Considerations
- Test with real MessageRouter
- Verify end-to-end message flow
- Test error scenarios
- Validate performance requirements

## Creating New Handlers

### Implementation Steps
1. Create handler class in `Handlers/` directory
2. Implement `IMessageHandler<TRequest, TResponse>`
3. Define request/response types in Domain layer
4. Add validation rules if needed
5. Register any required services
6. Add unit tests
7. Update documentation

### Handler Template
```csharp
public class NewHandler : IMessageHandler<NewRequest, NewResponse>
{
    private readonly ILogger<NewHandler> _logger;
    private readonly INewService _service;
    
    public string Method => "namespace/method";
    
    public NewHandler(ILogger<NewHandler> logger, INewService service)
    {
        _logger = logger;
        _service = service;
    }
    
    public async Task<NewResponse> HandleAsync(
        NewRequest request, 
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing {Method} request", Method);
        
        // Implementation
        var result = await _service.ProcessAsync(request, cancellationToken);
        
        return new NewResponse { Result = result };
    }
}
```

## Handler Security

### Authentication Requirements
- Most handlers require authenticated connection
- `ping` handler is typically exempt
- Authentication checked before handler execution
- Connection context provides user identity

### Authorization Patterns
- Resource-based authorization
- Tool execution permissions
- Rate limiting per user/connection
- Audit logging for sensitive operations

## Debugging Handlers

### Common Issues
1. **Handler Not Found**: Check method name matches exactly
2. **Validation Errors**: Review request structure and validators
3. **Null Reference**: Ensure all dependencies injected
4. **Timeout**: Check for blocking operations

### Debugging Tools
- Structured logging with correlation IDs
- Request/response logging middleware
- Performance profiling per handler
- Exception details in development mode