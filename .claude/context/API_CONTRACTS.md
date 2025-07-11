# Internal API Contracts

## Transport Layer APIs

### ITransport Interface
```csharp
public interface ITransport : IAsyncDisposable
{
    /// <summary>
    /// Starts the transport and begins listening for messages
    /// </summary>
    /// <exception cref="TransportException">Connection failed</exception>
    Task StartAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Sends a message through the transport
    /// </summary>
    /// <exception cref="TransportException">Send failed</exception>
    /// <exception cref="ObjectDisposedException">Transport disposed</exception>
    Task SendMessageAsync(object message, CancellationToken cancellationToken);
    
    /// <summary>
    /// Occurs when a message is received
    /// </summary>
    event AsyncEventHandler<MessageReceivedEventArgs> MessageReceived;
    
    /// <summary>
    /// Occurs when the transport encounters an error
    /// </summary>
    event AsyncEventHandler<TransportErrorEventArgs> ErrorOccurred;
}
```

### Transport Events
```csharp
public class MessageReceivedEventArgs : EventArgs
{
    public required JsonDocument Message { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public string? ConnectionId { get; init; }
}

public class TransportErrorEventArgs : EventArgs
{
    public required Exception Exception { get; init; }
    public required ErrorSeverity Severity { get; init; }
    public string? ConnectionId { get; init; }
}
```

## Tool System APIs

### ITool Interface
```csharp
public interface ITool
{
    /// <summary>
    /// Unique name for the tool
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Human-readable description
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// JSON Schema for input validation
    /// </summary>
    ToolSchema Schema { get; }
    
    /// <summary>
    /// Executes the tool with given parameters
    /// </summary>
    /// <exception cref="ToolExecutionException">Execution failed</exception>
    /// <exception cref="ToolValidationException">Invalid parameters</exception>
    Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken);
}
```

### Tool Models
```csharp
public record ToolRequest
{
    public required string Name { get; init; }
    public required JsonElement Arguments { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public record ToolResult
{
    public required bool Success { get; init; }
    public IReadOnlyList<ToolResultContent>? Content { get; init; }
    public ToolError? Error { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public record ToolResultContent
{
    public required string Type { get; init; } // "text", "image", "binary"
    public string? Text { get; init; }
    public string? MimeType { get; init; }
    public byte[]? Data { get; init; }
}
```

## Resource System APIs

### IResourceProvider Interface
```csharp
public interface IResourceProvider
{
    /// <summary>
    /// URI scheme this provider handles (e.g., "file", "http")
    /// </summary>
    string Scheme { get; }
    
    /// <summary>
    /// Lists available resources
    /// </summary>
    Task<IReadOnlyList<Resource>> ListResourcesAsync(
        string? pathPrefix, 
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Reads a specific resource
    /// </summary>
    /// <exception cref="ResourceNotFoundException">Resource not found</exception>
    /// <exception cref="ResourceAccessException">Access denied</exception>
    Task<ResourceContent> ReadResourceAsync(
        string uri, 
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Subscribes to resource changes
    /// </summary>
    Task<IResourceSubscription> SubscribeAsync(
        string uri,
        IResourceObserver observer,
        CancellationToken cancellationToken);
}
```

### Resource Models
```csharp
public record Resource
{
    public required string Uri { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? MimeType { get; init; }
    public ResourceMetadata? Metadata { get; init; }
}

public record ResourceContent
{
    public required string Uri { get; init; }
    public required string MimeType { get; init; }
    public required ReadOnlyMemory<byte> Data { get; init; }
    public string? Encoding { get; init; }
    public DateTimeOffset? LastModified { get; init; }
}

public interface IResourceObserver
{
    Task OnResourceCreatedAsync(ResourceCreatedEvent evt);
    Task OnResourceUpdatedAsync(ResourceUpdatedEvent evt);
    Task OnResourceDeletedAsync(ResourceDeletedEvent evt);
    Task OnErrorAsync(ResourceErrorEvent evt);
}
```

## Message Processing APIs

### IMessageHandler Interface
```csharp
public interface IMessageHandler
{
    /// <summary>
    /// Method name this handler processes
    /// </summary>
    string Method { get; }
    
    /// <summary>
    /// Processes the request and returns a response
    /// </summary>
    Task<JsonDocument?> HandleAsync(
        JsonDocument request,
        CancellationToken cancellationToken);
}
```

### IMessageProcessor Interface
```csharp
public interface IMessageProcessor
{
    /// <summary>
    /// Processes a JSON-RPC message
    /// </summary>
    /// <returns>Response if request, null if notification</returns>
    Task<JsonDocument?> ProcessAsync(
        JsonDocument message,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Registers a handler for a specific method
    /// </summary>
    void RegisterHandler(IMessageHandler handler);
}
```

## Server Lifecycle APIs

### IMcpServer Interface
```csharp
public interface IMcpServer
{
    /// <summary>
    /// Server information
    /// </summary>
    ServerInfo Info { get; }
    
    /// <summary>
    /// Current server state
    /// </summary>
    ServerState State { get; }
    
    /// <summary>
    /// Starts the server with specified transport
    /// </summary>
    Task StartAsync(ITransport transport, CancellationToken cancellationToken);
    
    /// <summary>
    /// Stops the server gracefully
    /// </summary>
    Task StopAsync(TimeSpan timeout);
    
    /// <summary>
    /// Server state changed
    /// </summary>
    event EventHandler<ServerStateChangedEventArgs> StateChanged;
}
```

### Server Models
```csharp
public enum ServerState
{
    Created,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed
}

public record ServerInfo
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public ServerCapabilities? Capabilities { get; init; }
}
```

## Extension Points

### IToolRegistry Interface
```csharp
public interface IToolRegistry
{
    /// <summary>
    /// Registers a tool instance
    /// </summary>
    void Register(ITool tool);
    
    /// <summary>
    /// Registers a tool factory
    /// </summary>
    void Register<TTool>(Func<IServiceProvider, TTool> factory) where TTool : ITool;
    
    /// <summary>
    /// Gets all registered tools
    /// </summary>
    IReadOnlyCollection<ITool> GetTools();
    
    /// <summary>
    /// Finds a tool by name
    /// </summary>
    ITool? FindTool(string name);
}
```

### IServerBuilder Interface
```csharp
public interface IServerBuilder
{
    /// <summary>
    /// Configures server options
    /// </summary>
    IServerBuilder Configure(Action<McpServerOptions> configure);
    
    /// <summary>
    /// Adds a tool to the server
    /// </summary>
    IServerBuilder AddTool<TTool>() where TTool : class, ITool;
    
    /// <summary>
    /// Adds a resource provider
    /// </summary>
    IServerBuilder AddResourceProvider<TProvider>() where TProvider : class, IResourceProvider;
    
    /// <summary>
    /// Builds the server instance
    /// </summary>
    IMcpServer Build();
}
```

## Error Handling Contracts

### Standard Exceptions
```csharp
// Base exception for all MCP errors
public class McpException : Exception
{
    public string ErrorCode { get; }
    public object? ErrorData { get; }
}

// Transport layer errors
public class TransportException : McpException { }
public class ConnectionException : TransportException { }
public class MessageException : TransportException { }

// Tool errors
public class ToolException : McpException { }
public class ToolNotFoundException : ToolException { }
public class ToolValidationException : ToolException { }
public class ToolExecutionException : ToolException { }

// Resource errors  
public class ResourceException : McpException { }
public class ResourceNotFoundException : ResourceException { }
public class ResourceAccessException : ResourceException { }
```

### Error Response Format
```json
{
    "jsonrpc": "2.0",
    "id": 1,
    "error": {
        "code": -32000,
        "message": "Tool execution failed",
        "data": {
            "tool": "calculator",
            "reason": "Division by zero"
        }
    }
}
```

## Threading Contracts

### Thread Safety Requirements
1. **ITransport**: Must be thread-safe for concurrent Send operations
2. **ITool**: Must be thread-safe for concurrent Execute operations
3. **IResourceProvider**: Must be thread-safe for all operations
4. **IMessageProcessor**: Must be thread-safe for concurrent Process operations

### Async Guidelines
1. Use `ConfigureAwait(false)` in library code
2. Return `ValueTask` for hot paths
3. Use `CancellationToken` for all async operations
4. Implement timeouts for external calls

## Versioning Contract

### API Version Strategy
- Major version changes break compatibility
- Minor version changes add functionality
- Patch versions are bug fixes only
- Use `[Obsolete]` attribute for deprecation

### Protocol Version Negotiation
```csharp
public interface IProtocolNegotiator
{
    /// <summary>
    /// Negotiates protocol version with client
    /// </summary>
    Task<string> NegotiateVersionAsync(
        string[] clientVersions,
        CancellationToken cancellationToken);
}
```