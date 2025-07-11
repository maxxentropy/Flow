# External System Integration Points

## 🔌 Transport Integrations

### stdio (Console) Integration
**Purpose**: Command-line tools and CLI applications
```csharp
// Integration example
var server = new McpServerBuilder()
    .UseStdioTransport()
    .Build();

// Client connection (Node.js example)
const { spawn } = require('child_process');
const mcpServer = spawn('dotnet', ['McpServer.Console.dll']);

mcpServer.stdout.on('data', (data) => {
    const message = JSON.parse(data.toString());
    // Process message
});

mcpServer.stdin.write(JSON.stringify({
    jsonrpc: "2.0",
    id: 1,
    method: "initialize"
}));
```

**Key Considerations**:
- Line-delimited JSON messages
- Proper stream flushing
- Process lifecycle management
- Error stream handling

### SSE (Server-Sent Events) Integration
**Purpose**: Web applications and HTTP clients
```javascript
// Browser client
const eventSource = new EventSource('http://localhost:8080/sse');

eventSource.onmessage = (event) => {
    const message = JSON.parse(event.data);
    // Process message
};

// Send request via fetch
fetch('http://localhost:8080/sse', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        jsonrpc: "2.0",
        id: 1,
        method: "tools/list"
    })
});
```

**Key Considerations**:
- CORS configuration required
- Connection timeout handling
- Reconnection logic
- Message buffering

### WebSocket Integration (Planned)
**Purpose**: Real-time bidirectional communication
```csharp
// Server setup
app.UseWebSockets();
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await HandleWebSocketConnection(webSocket);
    }
});

// Client connection
const ws = new WebSocket('ws://localhost:8080/ws');
ws.on('message', (data) => {
    const message = JSON.parse(data);
    // Process message
});
```

## 🛠️ Tool System Integrations

### File System Tool
**Purpose**: Local file operations
```csharp
public class FileSystemTool : ITool
{
    public async Task<ToolResult> ExecuteAsync(ToolRequest request)
    {
        var path = request.Arguments.GetProperty("path").GetString();
        
        // Security considerations
        if (!IsPathAllowed(path))
            return ToolResult.Error("Access denied");
        
        // Integration with file system
        var content = await File.ReadAllTextAsync(path);
        return ToolResult.Success(content);
    }
}
```

**Security Boundaries**:
- Sandbox paths to specific directories
- Validate path traversal attempts
- Check file permissions
- Limit file sizes

### HTTP Client Tool
**Purpose**: External API calls
```csharp
public class HttpClientTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    
    public async Task<ToolResult> ExecuteAsync(ToolRequest request)
    {
        var url = request.Arguments.GetProperty("url").GetString();
        
        // Apply policies
        var policy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        
        var response = await policy.ExecuteAsync(async () => 
            await _httpClient.GetAsync(url));
        
        return ToolResult.Success(await response.Content.ReadAsStringAsync());
    }
}
```

**Configuration**:
```json
{
  "HttpClient": {
    "Timeout": "00:00:30",
    "MaxResponseContentBufferSize": 10485760,
    "AllowedHosts": ["api.example.com", "*.trusted.com"],
    "DefaultHeaders": {
      "User-Agent": "MCP-Server/1.0"
    }
  }
}
```

### Database Query Tool
**Purpose**: SQL database access
```csharp
public class DatabaseTool : ITool
{
    private readonly string _connectionString;
    
    public async Task<ToolResult> ExecuteAsync(ToolRequest request)
    {
        var query = request.Arguments.GetProperty("query").GetString();
        
        // Validate query (read-only)
        if (!IsReadOnlyQuery(query))
            return ToolResult.Error("Only SELECT queries allowed");
        
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();
        
        var results = new List<Dictionary<string, object>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }
            results.Add(row);
        }
        
        return ToolResult.Success(JsonSerializer.Serialize(results));
    }
}
```

### Process Execution Tool
**Purpose**: Run external commands
```csharp
public class ProcessTool : ITool
{
    private readonly IConfiguration _config;
    
    public async Task<ToolResult> ExecuteAsync(ToolRequest request)
    {
        var command = request.Arguments.GetProperty("command").GetString();
        var args = request.Arguments.GetProperty("args").GetString();
        
        // Whitelist validation
        if (!IsCommandAllowed(command))
            return ToolResult.Error("Command not allowed");
        
        var processInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process { StartInfo = processInfo };
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
            return ToolResult.Error($"Process failed: {error}");
        
        return ToolResult.Success(output);
    }
}
```

## 📦 Resource Provider Integrations

### Git Resource Provider
**Purpose**: Access Git repositories
```csharp
public class GitResourceProvider : IResourceProvider
{
    public string Scheme => "git";
    
    public async Task<ResourceContent> ReadResourceAsync(string uri)
    {
        // Parse git://repo/branch/path
        var parts = ParseGitUri(uri);
        
        using var repo = new Repository(parts.RepoPath);
        var commit = repo.Branches[parts.Branch].Tip;
        var blob = commit[parts.FilePath].Target as Blob;
        
        if (blob == null)
            throw new ResourceNotFoundException(uri);
        
        return new ResourceContent
        {
            Uri = uri,
            MimeType = MimeTypes.GetMimeType(parts.FilePath),
            Data = blob.GetContentStream().ToArray()
        };
    }
}
```

### S3 Resource Provider
**Purpose**: Amazon S3 integration
```csharp
public class S3ResourceProvider : IResourceProvider
{
    private readonly IAmazonS3 _s3Client;
    
    public string Scheme => "s3";
    
    public async Task<ResourceContent> ReadResourceAsync(string uri)
    {
        // Parse s3://bucket/key
        var parts = ParseS3Uri(uri);
        
        var request = new GetObjectRequest
        {
            BucketName = parts.Bucket,
            Key = parts.Key
        };
        
        try
        {
            var response = await _s3Client.GetObjectAsync(request);
            
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            
            return new ResourceContent
            {
                Uri = uri,
                MimeType = response.Headers.ContentType,
                Data = memoryStream.ToArray(),
                LastModified = response.LastModified
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ResourceNotFoundException(uri);
        }
    }
}
```

### GraphQL Resource Provider
**Purpose**: Query GraphQL APIs
```csharp
public class GraphQLResourceProvider : IResourceProvider
{
    private readonly HttpClient _httpClient;
    
    public string Scheme => "graphql";
    
    public async Task<ResourceContent> ReadResourceAsync(string uri)
    {
        // Parse graphql://endpoint/query-name
        var parts = ParseGraphQLUri(uri);
        
        var query = LoadQuery(parts.QueryName);
        var request = new
        {
            query,
            variables = parts.Variables
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            parts.Endpoint, 
            request);
        
        var content = await response.Content.ReadAsStringAsync();
        
        return new ResourceContent
        {
            Uri = uri,
            MimeType = "application/json",
            Data = Encoding.UTF8.GetBytes(content)
        };
    }
}
```

## 🔐 Authentication Integrations

### API Key Authentication
```csharp
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKey))
            return AuthenticateResult.Fail("Missing API key");
        
        // Validate API key
        var isValid = await ValidateApiKeyAsync(apiKey);
        if (!isValid)
            return AuthenticateResult.Fail("Invalid API key");
        
        var claims = new[] { new Claim(ClaimTypes.Name, "api-user") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        
        return AuthenticateResult.Success(ticket);
    }
}
```

### OAuth2 Integration
```csharp
services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.example.com";
        options.Audience = "mcp-server";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });
```

## 📊 Monitoring Integrations

### OpenTelemetry Integration
```csharp
services.AddOpenTelemetryTracing(builder =>
{
    builder
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(serviceName: "mcp-server"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("McpServer")
        .AddJaegerExporter(options =>
        {
            options.AgentHost = "localhost";
            options.AgentPort = 6831;
        });
});

// Custom spans
using var activity = Activity.StartActivity("Tool.Execute");
activity?.SetTag("tool.name", toolName);
activity?.SetTag("tool.duration", stopwatch.ElapsedMilliseconds);
```

### Prometheus Metrics
```csharp
public class MetricsService
{
    private readonly Counter _requestCounter;
    private readonly Histogram _requestDuration;
    private readonly Gauge _activeConnections;
    
    public MetricsService()
    {
        _requestCounter = Metrics.CreateCounter(
            "mcp_requests_total", 
            "Total number of requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "method", "status" }
            });
        
        _requestDuration = Metrics.CreateHistogram(
            "mcp_request_duration_seconds",
            "Request duration in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "method" },
                Buckets = Histogram.LinearBuckets(0.001, 0.001, 100)
            });
        
        _activeConnections = Metrics.CreateGauge(
            "mcp_active_connections",
            "Number of active connections");
    }
}
```

## 🔄 Event System Integrations

### RabbitMQ Integration
```csharp
public class RabbitMQEventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    
    public async Task PublishAsync<T>(T @event) where T : IEvent
    {
        using var channel = _connection.CreateModel();
        
        var exchange = "mcp.events";
        var routingKey = @event.GetType().Name.ToLower();
        
        var message = JsonSerializer.SerializeToUtf8Bytes(@event);
        
        channel.BasicPublish(
            exchange: exchange,
            routingKey: routingKey,
            basicProperties: null,
            body: message);
    }
}
```

### Kafka Integration
```csharp
public class KafkaEventPublisher : IEventPublisher
{
    private readonly IProducer<string, string> _producer;
    
    public async Task PublishAsync<T>(T @event) where T : IEvent
    {
        var message = new Message<string, string>
        {
            Key = @event.Id,
            Value = JsonSerializer.Serialize(@event),
            Headers = new Headers
            {
                { "event-type", Encoding.UTF8.GetBytes(@event.GetType().Name) }
            }
        };
        
        await _producer.ProduceAsync("mcp-events", message);
    }
}
```

## 📝 Integration Best Practices

1. **Always use circuit breakers** for external calls
2. **Implement proper retry policies** with exponential backoff
3. **Set reasonable timeouts** for all integrations
4. **Use connection pooling** where applicable
5. **Implement health checks** for each integration
6. **Log integration points** with correlation IDs
7. **Monitor integration performance** separately
8. **Version external API calls** for compatibility
9. **Validate all external inputs** thoroughly
10. **Document integration requirements** clearly