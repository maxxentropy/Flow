# Common Errors and Solutions

## 🔴 Startup Errors

### Error: "Unable to bind to port 8080"
**Symptoms**: Server fails to start, "address already in use"
```
System.IO.IOException: Failed to bind to address http://localhost:8080: address already in use.
```

**Causes**:
1. Another process using port 8080
2. Previous instance didn't shut down cleanly
3. Docker container still running

**Solutions**:
```bash
# Find process using port
lsof -i :8080  # macOS/Linux
netstat -ano | findstr :8080  # Windows

# Kill process
kill -9 <PID>  # macOS/Linux
taskkill /PID <PID> /F  # Windows

# Or use different port
dotnet run --urls "http://localhost:8081"
```

### Error: "Configuration section 'McpServer' not found"
**Symptoms**: NullReferenceException during startup
```
System.NullReferenceException: Configuration section 'McpServer' is required
```

**Causes**:
1. Missing appsettings.json
2. Incorrect JSON structure
3. Wrong environment

**Solutions**:
```json
// Ensure appsettings.json has required section
{
  "McpServer": {
    "Name": "MyServer",
    "Version": "1.0.0",
    "Transport": {
      "Stdio": { "Enabled": true },
      "Sse": { "Enabled": true, "Port": 8080 }
    }
  }
}
```

### Error: "SSL certificate not found"
**Symptoms**: HTTPS redirection fails
```
System.InvalidOperationException: Unable to configure HTTPS endpoint. No server certificate was specified.
```

**Solutions**:
```bash
# Generate dev certificate
dotnet dev-certs https --trust

# Or disable HTTPS in development
export ASPNETCORE_URLS="http://localhost:8080"
```

## 🟠 Transport Errors

### Error: "Invalid JSON-RPC message"
**Symptoms**: Parse errors in logs
```
JsonException: Invalid JSON-RPC message: missing 'jsonrpc' field
```

**Causes**:
1. Malformed JSON
2. Wrong content type
3. Encoding issues

**Solutions**:
```csharp
// Validate JSON structure
try
{
    var message = JsonSerializer.Deserialize<JsonRpcMessage>(json);
    if (message.JsonRpc != "2.0")
        throw new ProtocolException("Invalid JSON-RPC version");
}
catch (JsonException ex)
{
    _logger.LogError(ex, "Failed to parse message: {Json}", json);
    return ErrorResponse("Parse error", -32700);
}
```

### Error: "SSE connection timeout"
**Symptoms**: Client disconnects after 30 seconds
```
The remote party closed the WebSocket connection without completing the close handshake.
```

**Causes**:
1. No keep-alive messages
2. Proxy timeout
3. Client timeout setting

**Solutions**:
```csharp
// Add keep-alive to SSE transport
private async Task SendKeepAlive()
{
    while (!_cancellationToken.IsCancellationRequested)
    {
        await _response.WriteAsync(":keep-alive\n\n");
        await _response.Body.FlushAsync();
        await Task.Delay(TimeSpan.FromSeconds(30), _cancellationToken);
    }
}
```

### Error: "Message size exceeds limit"
**Symptoms**: Large messages rejected
```
InvalidOperationException: Message size 10485760 exceeds maximum 1048576
```

**Solutions**:
```json
// Increase limits in appsettings.json
{
  "Kestrel": {
    "Limits": {
      "MaxRequestBodySize": 52428800
    }
  },
  "McpServer": {
    "Transport": {
      "MaxMessageSize": 10485760
    }
  }
}
```

## 🟡 Tool Execution Errors

### Error: "Tool not found"
**Symptoms**: tools/call fails
```json
{
  "error": {
    "code": -32002,
    "message": "Tool 'unknown_tool' not found"
  }
}
```

**Causes**:
1. Tool not registered
2. Case sensitivity issue
3. Tool disabled

**Solutions**:
```csharp
// Ensure tool is registered
services.AddSingleton<ITool, MyCustomTool>();

// Or check registration
var tools = serviceProvider.GetServices<ITool>();
_logger.LogInformation("Registered tools: {Tools}", 
    string.Join(", ", tools.Select(t => t.Name)));
```

### Error: "Tool parameter validation failed"
**Symptoms**: Invalid arguments error
```json
{
  "error": {
    "code": -32602,
    "message": "Invalid params",
    "data": {
      "validationErrors": ["'path' is required"]
    }
  }
}
```

**Solutions**:
```csharp
// Add detailed validation
public class FileReaderTool : ITool
{
    public async Task<ToolResult> ExecuteAsync(ToolRequest request)
    {
        if (!request.Arguments.TryGetProperty("path", out var pathElement))
        {
            return ToolResult.Error("Missing required parameter 'path'");
        }
        
        var path = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolResult.Error("Parameter 'path' cannot be empty");
        }
        
        // Continue with execution
    }
}
```

### Error: "Tool execution timeout"
**Symptoms**: Long-running tools fail
```
OperationCanceledException: The operation was canceled.
```

**Solutions**:
```csharp
// Configure timeout per tool
public class LongRunningTool : ITool
{
    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request, 
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5)); // Tool-specific timeout
        
        try
        {
            return await DoWork(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return ToolResult.Error("Tool execution timed out");
        }
    }
}
```

## 🔵 Resource Access Errors

### Error: "Resource not found"
**Symptoms**: 404 for resource reads
```json
{
  "error": {
    "code": -32003,
    "message": "Resource not found: file:///missing.txt"
  }
}
```

**Solutions**:
```csharp
// Add existence check
public async Task<ResourceContent> ReadResourceAsync(string uri)
{
    var path = GetPathFromUri(uri);
    
    if (!File.Exists(path))
    {
        throw new ResourceNotFoundException($"File not found: {path}");
    }
    
    // Add permission check
    try
    {
        using var file = File.OpenRead(path);
        // Read content
    }
    catch (UnauthorizedAccessException)
    {
        throw new ResourceAccessException($"Access denied: {path}");
    }
}
```

### Error: "Invalid resource URI"
**Symptoms**: URI parsing fails
```
UriFormatException: Invalid URI: The format of the URI could not be determined.
```

**Solutions**:
```csharp
// Validate URI format
public bool IsValidResourceUri(string uri)
{
    if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        return false;
    
    // Check supported schemes
    var supportedSchemes = new[] { "file", "http", "https" };
    return supportedSchemes.Contains(parsed.Scheme);
}
```

## 🟣 Performance Issues

### Error: "High memory usage"
**Symptoms**: OutOfMemoryException or slow GC
```
OutOfMemoryException: Insufficient memory to continue the execution of the program.
```

**Causes**:
1. Memory leaks in handlers
2. Large message buffering
3. No connection limits

**Solutions**:
```csharp
// Use memory-efficient streaming
public async Task StreamLargeResponse(Stream output)
{
    const int bufferSize = 4096;
    using var buffer = MemoryPool<byte>.Shared.Rent(bufferSize);
    
    // Stream instead of loading all in memory
    await sourceStream.CopyToAsync(output, buffer.Memory);
}

// Add connection limits
services.Configure<McpServerOptions>(options =>
{
    options.MaxConcurrentConnections = 100;
    options.ConnectionTimeout = TimeSpan.FromMinutes(30);
});
```

### Error: "Thread pool starvation"
**Symptoms**: Increasing response times
```
ThreadPool starvation detected! Consider increasing MinThreads.
```

**Solutions**:
```csharp
// Configure thread pool
ThreadPool.SetMinThreads(100, 100);

// Use async properly
// BAD: Blocking async
var result = SomeAsyncMethod().Result;

// GOOD: Async all the way
var result = await SomeAsyncMethod();
```

## 🟤 Integration Errors

### Error: "Circuit breaker open"
**Symptoms**: External calls failing fast
```
BrokenCircuitException: The circuit is now open and is not allowing calls.
```

**Solutions**:
```csharp
// Configure circuit breaker
services.AddHttpClient<IExternalService>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            5,    // failures before opening
            TimeSpan.FromSeconds(30)); // duration open
}
```

## 📊 Diagnostic Commands

### General Diagnostics
```bash
# Check server health
curl http://localhost:8080/health

# View real-time logs
dotnet run | grep -E "ERROR|WARN"

# Monitor memory usage
dotnet-counters monitor -p $(pgrep McpServer)

# Capture trace
dotnet-trace collect -p $(pgrep McpServer) --duration 00:00:30
```

### Error Analysis
```bash
# Count errors by type
grep ERROR logs/*.log | cut -d' ' -f5- | sort | uniq -c | sort -nr

# Find error patterns
grep -B5 -A5 "Exception" logs/*.log

# Track specific correlation ID
grep "correlationId=abc-123" logs/*.log | grep -E "ERROR|WARN"
```

## 🚑 Emergency Procedures

### Server Unresponsive
1. Check process status: `ps aux | grep McpServer`
2. Check memory/CPU: `top -p $(pgrep McpServer)`
3. Force restart: `kill -TERM $(pgrep McpServer) && dotnet run`

### Data Corruption
1. Stop server immediately
2. Backup current state
3. Check logs for root cause
4. Restore from last known good state

### Security Breach
1. Isolate affected server
2. Revoke compromised credentials
3. Analyze logs for attack vector
4. Apply security patches
5. Notify security team