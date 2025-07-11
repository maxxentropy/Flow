# Tools System Index

## Overview
The tools system provides extensible functionality that MCP clients can discover and execute. Tools are self-describing, validated, and can report progress during execution.

## Tool Architecture

### Core Tool Interface (`src/McpServer.Domain/Tools/ITool.cs`)
```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolSchema Schema { get; }
    Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken);
}
```

### Tool Components
- **Name**: Unique identifier for the tool
- **Description**: Human-readable description
- **Schema**: JSON Schema defining parameters
- **ExecuteAsync**: Async execution method

## Built-in Tools

### CalculatorTool (`src/McpServer.Infrastructure/Tools/CalculatorTool.cs`)
- **Purpose**: Basic arithmetic operations
- **Operations**: add, subtract, multiply, divide
- **Example**:
  ```json
  {
    "operation": "add",
    "a": 10,
    "b": 5
  }
  ```

### DateTimeTool (`src/McpServer.Infrastructure/Tools/DateTimeTool.cs`)
- **Purpose**: Date/time operations
- **Features**:
  - Current time in various formats
  - Timezone conversions
  - Date arithmetic
  - Parsing and formatting

### EchoTool (`src/McpServer.Infrastructure/Tools/EchoTool.cs`)
- **Purpose**: Testing and debugging
- **Simple echo of input**
- **Useful for connection testing**

### DataProcessingTool (`src/McpServer.Infrastructure/Tools/DataProcessingTool.cs`)
- **Purpose**: Data transformation operations
- **Features**:
  - JSON transformation
  - CSV processing
  - Data filtering
  - Aggregations

### AiAssistantTool (`src/McpServer.Infrastructure/Tools/AiAssistantTool.cs`)
- **Purpose**: AI model integration
- **Features**:
  - Text generation
  - Summarization
  - Translation
  - Q&A capabilities

### AuthenticationDemoTool (`src/McpServer.Infrastructure/Tools/AuthenticationDemoTool.cs`)
- **Purpose**: Demonstrates auth-aware tools
- **Shows current user context**
- **Permission-based execution**

### CompletionDemoTool (`src/McpServer.Infrastructure/Tools/CompletionDemoTool.cs`)
- **Purpose**: Auto-completion demonstration
- **Provides completion suggestions**
- **Context-aware responses**

### LoggingDemoTool (`src/McpServer.Infrastructure/Tools/LoggingDemoTool.cs`)
- **Purpose**: Logging system demonstration
- **Creates log entries**
- **Shows structured logging**

### RootsDemoTool (`src/McpServer.Infrastructure/Tools/RootsDemoTool.cs`)
- **Purpose**: File system roots demonstration
- **Lists available roots**
- **Platform-specific handling**

## Tool Management

### Tool Registry (`src/McpServer.Application/Services/ToolRegistry.cs`)
- **Central tool repository**
- **Features**:
  - Dynamic tool registration
  - Tool discovery
  - Metadata caching
  - Hot-reload support
- **Key Methods**:
  ```csharp
  Task RegisterToolAsync(ITool tool)
  Task<IEnumerable<ITool>> GetToolsAsync()
  Task<ITool?> GetToolAsync(string name)
  ```

### Tool Discovery
- **Automatic**: Scan assemblies for ITool implementations
- **Manual**: Register via DI container
- **Dynamic**: Load from plugins
- **Configuration**: Enable/disable via settings

## Tool Execution

### Execution Flow
1. **Request Reception**: Client sends tool call request
2. **Tool Resolution**: Registry finds tool by name
3. **Validation**: Parameters validated against schema
4. **Authorization**: Check user permissions
5. **Execution**: Tool processes request
6. **Result/Error**: Response sent to client

### Validated Tool Wrapper (`src/McpServer.Application/Tools/ValidatedToolWrapper.cs`)
- **Wraps tools with validation**
- **Features**:
  - Schema validation
  - Input sanitization
  - Error standardization
  - Performance tracking

### Progress-Aware Tool (`src/McpServer.Application/Tools/ProgressAwareTool.cs`)
- **Base class for long-running tools**
- **Progress reporting**:
  ```csharp
  protected async Task ReportProgressAsync(double percentage, string message)
  {
      await _progressTracker.ReportAsync(new ProgressUpdate
      {
          Percentage = percentage,
          Message = message
      });
  }
  ```

## Tool Schema Definition

### Schema Structure
```csharp
public class ToolSchema
{
    public string Type { get; set; } = "object";
    public Dictionary<string, PropertySchema> Properties { get; set; }
    public List<string> Required { get; set; }
    public bool AdditionalProperties { get; set; } = false;
}
```

### Property Types
- **string**: Text inputs
- **number**: Numeric inputs
- **boolean**: True/false flags
- **array**: Lists of values
- **object**: Nested structures

### Schema Example
```csharp
Schema = new ToolSchema
{
    Properties = new Dictionary<string, PropertySchema>
    {
        ["operation"] = new PropertySchema
        {
            Type = "string",
            Description = "Operation to perform",
            Enum = new[] { "add", "subtract", "multiply", "divide" }
        },
        ["a"] = new PropertySchema
        {
            Type = "number",
            Description = "First operand"
        },
        ["b"] = new PropertySchema
        {
            Type = "number",
            Description = "Second operand"
        }
    },
    Required = new[] { "operation", "a", "b" }
}
```

## Tool Development

### Creating a New Tool
1. **Create tool class**:
   ```csharp
   public class MyTool : ITool
   {
       public string Name => "myTool";
       public string Description => "My custom tool";
       
       public ToolSchema Schema => new ToolSchema
       {
           // Define parameters
       };
       
       public async Task<ToolResult> ExecuteAsync(
           ToolRequest request, 
           CancellationToken cancellationToken)
       {
           // Implementation
       }
   }
   ```

2. **Register in DI**:
   ```csharp
   services.AddSingleton<ITool, MyTool>();
   ```

3. **Add tests**:
   ```csharp
   [Test]
   public async Task MyTool_ValidInput_ReturnsExpectedResult()
   {
       // Test implementation
   }
   ```

### Tool Best Practices
1. **Validate inputs thoroughly**
2. **Handle cancellation properly**
3. **Report progress for long operations**
4. **Return structured results**
5. **Log important operations**
6. **Handle errors gracefully**

## Tool Security

### Permission Model
- **Tool-level permissions**
- **Parameter validation**
- **Output sanitization**
- **Audit logging**

### Security Patterns
```csharp
public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken ct)
{
    // Check permissions
    var user = _connectionManager.GetCurrentUser();
    if (!await _authService.CanExecuteToolAsync(user, Name))
        throw new UnauthorizedException($"User cannot execute {Name}");
    
    // Validate inputs
    ValidateInputs(request.Arguments);
    
    // Execute with timeout
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromMinutes(5));
    
    return await ExecuteInternalAsync(request, cts.Token);
}
```

## Tool Performance

### Caching (`src/McpServer.Application/Caching/ToolResultCache.cs`)
- **Cache tool results by parameters**
- **Configurable TTL**
- **Cache invalidation strategies**
- **Memory limits**

### Performance Patterns
```csharp
public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken ct)
{
    // Check cache
    var cacheKey = GenerateCacheKey(request);
    if (_cache.TryGet(cacheKey, out var cached))
        return cached;
    
    // Execute
    var result = await ExecuteInternalAsync(request, ct);
    
    // Cache result
    _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
    
    return result;
}
```

### Optimization Tips
1. **Use async/await properly**
2. **Stream large results**
3. **Implement cancellation**
4. **Pool expensive resources**
5. **Monitor execution times**

## Tool Testing

### Unit Testing Pattern
```csharp
[TestFixture]
public class MyToolTests
{
    private MyTool _tool;
    
    [SetUp]
    public void Setup()
    {
        _tool = new MyTool(/* dependencies */);
    }
    
    [Test]
    public async Task ExecuteAsync_ValidInput_Success()
    {
        // Arrange
        var request = new ToolRequest
        {
            Name = "myTool",
            Arguments = JObject.FromObject(new { /* params */ })
        };
        
        // Act
        var result = await _tool.ExecuteAsync(request, CancellationToken.None);
        
        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.NotNull(result.Content);
    }
}
```

### Integration Testing
- Test with real ToolRegistry
- Verify schema validation
- Test error scenarios
- Check performance requirements

## Tool Configuration

### Enable/Disable Tools
```json
{
  "Tools": {
    "Enabled": ["calculator", "dateTime", "echo"],
    "Disabled": ["experimental"],
    "MaxConcurrentExecutions": 10,
    "DefaultTimeout": "00:02:00"
  }
}
```

### Tool-Specific Configuration
```json
{
  "Tools": {
    "Settings": {
      "aiAssistant": {
        "ModelEndpoint": "https://api.ai.example.com",
        "ApiKey": "secret-key",
        "MaxTokens": 1000
      }
    }
  }
}
```

## Tool Monitoring

### Metrics Collection
- **Execution count**
- **Success/failure rates**
- **Execution duration**
- **Parameter patterns**
- **Error frequencies**

### Monitoring Implementation
```csharp
public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken ct)
{
    using var activity = Activity.StartActivity($"Tool.{Name}");
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        var result = await ExecuteInternalAsync(request, ct);
        _metrics.RecordSuccess(Name, stopwatch.Elapsed);
        return result;
    }
    catch (Exception ex)
    {
        _metrics.RecordFailure(Name, stopwatch.Elapsed, ex);
        throw;
    }
}
```

## Common Tool Patterns

### Parameter Transformation
```csharp
private T ExtractParameter<T>(JObject arguments, string name, T defaultValue = default)
{
    if (arguments.TryGetValue(name, out var value))
        return value.ToObject<T>() ?? defaultValue;
    return defaultValue;
}
```

### Result Building
```csharp
protected ToolResult Success(object content, string? mimeType = null)
{
    return new ToolResult
    {
        Content = new List<ToolContent>
        {
            new ToolContent
            {
                Type = "text",
                Text = JsonSerializer.Serialize(content),
                MimeType = mimeType ?? "application/json"
            }
        },
        IsSuccess = true
    };
}
```

### Error Handling
```csharp
protected ToolResult Error(string message, Exception? exception = null)
{
    _logger.LogError(exception, "Tool error: {Message}", message);
    return new ToolResult
    {
        Content = new List<ToolContent>
        {
            new ToolContent
            {
                Type = "text",
                Text = message
            }
        },
        IsSuccess = false,
        IsError = true
    };
}
```