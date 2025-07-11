# Common Code Patterns

## Dependency Injection Patterns

### Service Registration
```csharp
// Singleton for stateless services
services.AddSingleton<IMessageRouter, MessageRouter>();

// Scoped for per-request services
services.AddScoped<IAuthenticationService, AuthenticationService>();

// Transient for lightweight factories
services.AddTransient<IValidator<ToolRequest>, ToolRequestValidator>();

// Factory pattern
services.AddSingleton<Func<TransportType, ITransport>>(provider => type =>
{
    return type switch
    {
        TransportType.Stdio => provider.GetRequiredService<StdioTransport>(),
        TransportType.ServerSentEvents => provider.GetRequiredService<SseTransport>(),
        TransportType.WebSocket => provider.GetRequiredService<WebSocketTransport>(),
        _ => throw new NotSupportedException($"Transport {type} not supported")
    };
});
```

### Options Pattern
```csharp
// Configuration class
public class McpServerOptions
{
    public string Name { get; set; } = "MCP Server";
    public string Version { get; set; } = "1.0.0";
    public TransportOptions Transport { get; set; } = new();
}

// Registration
services.Configure<McpServerOptions>(configuration.GetSection("McpServer"));

// Usage
public class MyService
{
    private readonly McpServerOptions _options;
    
    public MyService(IOptions<McpServerOptions> options)
    {
        _options = options.Value;
    }
}
```

## Async/Await Patterns

### Proper Async Implementation
```csharp
// Good - async all the way down
public async Task<Result> ProcessAsync(Request request, CancellationToken ct)
{
    // ConfigureAwait(false) for library code
    var data = await LoadDataAsync(request.Id, ct).ConfigureAwait(false);
    var processed = await TransformAsync(data, ct).ConfigureAwait(false);
    return await SaveAsync(processed, ct).ConfigureAwait(false);
}

// Bad - blocking async code
public Result Process(Request request)
{
    // Don't do this!
    var data = LoadDataAsync(request.Id).Result;
    return Transform(data);
}
```

### Cancellation Token Propagation
```csharp
public async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
{
    // Create linked token for timeout
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(TimeSpan.FromSeconds(30));
    
    try
    {
        return await ProcessAsync(request, cts.Token);
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
    {
        throw new TimeoutException("Operation timed out");
    }
}
```

## Error Handling Patterns

### Exception Handling with MCP Errors
```csharp
public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken ct)
{
    try
    {
        ValidateRequest(request);
        return await ExecuteInternalAsync(request, ct);
    }
    catch (ValidationException ex)
    {
        _logger.LogWarning(ex, "Validation failed for tool {Tool}", Name);
        throw new McpException(McpErrorCodes.InvalidParams, ex.Message);
    }
    catch (UnauthorizedException ex)
    {
        _logger.LogWarning(ex, "Unauthorized access to tool {Tool}", Name);
        throw new McpException(McpErrorCodes.Unauthorized, "Access denied");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error in tool {Tool}", Name);
        throw new McpException(McpErrorCodes.InternalError, "An error occurred");
    }
}
```

### Result Pattern
```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }
    
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Usage
public async Task<Result<User>> GetUserAsync(string id)
{
    var user = await _repository.FindAsync(id);
    return user != null 
        ? Result<User>.Success(user)
        : Result<User>.Failure("User not found");
}
```

## Logging Patterns

### Structured Logging
```csharp
public class ToolExecutor
{
    private readonly ILogger<ToolExecutor> _logger;
    
    public async Task ExecuteAsync(string toolName, ToolRequest request)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["ToolName"] = toolName,
            ["RequestId"] = request.Id,
            ["UserId"] = request.UserId
        }))
        {
            _logger.LogInformation("Executing tool {ToolName}", toolName);
            
            try
            {
                await ProcessAsync(request);
                _logger.LogInformation("Tool {ToolName} executed successfully", toolName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tool {ToolName} execution failed", toolName);
                throw;
            }
        }
    }
}
```

### Performance Logging
```csharp
public async Task<T> ExecuteWithTimingAsync<T>(Func<Task<T>> operation, string operationName)
{
    using var activity = Activity.StartActivity(operationName);
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        var result = await operation();
        _logger.LogInformation("{Operation} completed in {ElapsedMs}ms", 
            operationName, stopwatch.ElapsedMilliseconds);
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "{Operation} failed after {ElapsedMs}ms", 
            operationName, stopwatch.ElapsedMilliseconds);
        throw;
    }
}
```

## Validation Patterns

### FluentValidation Usage
```csharp
public class ToolRequestValidator : AbstractValidator<ToolRequest>
{
    public ToolRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tool name is required")
            .Matches("^[a-zA-Z0-9_-]+$").WithMessage("Invalid tool name format");
            
        RuleFor(x => x.Arguments)
            .NotNull().WithMessage("Arguments are required")
            .Must(BeValidJson).WithMessage("Arguments must be valid JSON");
            
        RuleFor(x => x.Timeout)
            .InclusiveBetween(1, 300).When(x => x.Timeout.HasValue)
            .WithMessage("Timeout must be between 1 and 300 seconds");
    }
    
    private bool BeValidJson(JObject? args)
    {
        try
        {
            return args != null && args.HasValues;
        }
        catch
        {
            return false;
        }
    }
}
```

### Guard Clauses
```csharp
public static class Guard
{
    public static T NotNull<T>(T? value, [CallerArgumentExpression("value")] string? paramName = null)
        where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
        return value;
    }
    
    public static string NotNullOrWhiteSpace(string? value, [CallerArgumentExpression("value")] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace", paramName);
        return value;
    }
    
    public static void Range(int value, int min, int max, [CallerArgumentExpression("value")] string? paramName = null)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(paramName, $"Value must be between {min} and {max}");
    }
}

// Usage
public void ProcessRequest(Request request)
{
    Guard.NotNull(request);
    Guard.NotNullOrWhiteSpace(request.Id);
    Guard.Range(request.Priority, 1, 10);
}
```

## Caching Patterns

### Memory Cache with Expiration
```csharp
public class CachedService<T> where T : class
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedService<T>> _logger;
    
    public async Task<T?> GetOrCreateAsync(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue<T>(key, out var cached))
        {
            _logger.LogDebug("Cache hit for key {Key}", key);
            return cached;
        }
        
        _logger.LogDebug("Cache miss for key {Key}", key);
        var value = await factory();
        
        if (value != null)
        {
            var options = new MemoryCacheEntryOptions();
            if (expiration.HasValue)
                options.SetAbsoluteExpiration(expiration.Value);
                
            _cache.Set(key, value, options);
        }
        
        return value;
    }
}
```

### Distributed Cache Pattern
```csharp
public class DistributedCacheService
{
    private readonly IDistributedCache _cache;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var json = await _cache.GetStringAsync(key, ct);
        return json != null 
            ? JsonSerializer.Deserialize<T>(json, _jsonOptions)
            : default;
    }
    
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        var options = new DistributedCacheEntryOptions();
        
        if (expiration.HasValue)
            options.SetAbsoluteExpiration(expiration.Value);
            
        await _cache.SetStringAsync(key, json, options, ct);
    }
}
```

## Connection Management Patterns

### Connection State Tracking
```csharp
public class ConnectionManager
{
    private readonly ConcurrentDictionary<string, Connection> _connections = new();
    
    public bool TryAddConnection(Connection connection)
    {
        return _connections.TryAdd(connection.Id, connection);
    }
    
    public bool TryRemoveConnection(string connectionId, out Connection? connection)
    {
        return _connections.TryRemove(connectionId, out connection);
    }
    
    public Connection? GetConnection(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var connection) 
            ? connection 
            : null;
    }
    
    public IEnumerable<Connection> GetActiveConnections()
    {
        return _connections.Values.Where(c => c.IsActive);
    }
}
```

## Message Processing Patterns

### Message Router Pattern
```csharp
public class MessageRouter
{
    private readonly Dictionary<string, Type> _handlerTypes;
    private readonly IServiceProvider _serviceProvider;
    
    public async Task<JsonRpcResponse> RouteAsync(JsonRpcRequest request, CancellationToken ct)
    {
        if (!_handlerTypes.TryGetValue(request.Method, out var handlerType))
        {
            throw new McpException(McpErrorCodes.MethodNotFound, 
                $"Unknown method: {request.Method}");
        }
        
        var handler = _serviceProvider.GetRequiredService(handlerType);
        var handleMethod = handlerType.GetMethod("HandleAsync");
        
        var task = (Task)handleMethod!.Invoke(handler, new object[] { request.Params, ct })!;
        await task.ConfigureAwait(false);
        
        var resultProperty = task.GetType().GetProperty("Result");
        var result = resultProperty!.GetValue(task);
        
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = result
        };
    }
}
```

## Resource Management Patterns

### Disposable Pattern
```csharp
public class ResourceManager : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private bool _disposed;
    
    public T Register<T>(T resource) where T : IDisposable
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ResourceManager));
            
        _disposables.Add(resource);
        return resource;
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
            
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing resource");
            }
        }
        
        _disposables.Clear();
        _disposed = true;
    }
}
```

### Using Statement Pattern
```csharp
public async Task ProcessFileAsync(string path, CancellationToken ct)
{
    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
    using var reader = new StreamReader(stream);
    
    string? line;
    while ((line = await reader.ReadLineAsync(ct)) != null)
    {
        await ProcessLineAsync(line, ct);
    }
}
```

## Testing Patterns

### Mock Setup Pattern
```csharp
[TestFixture]
public class ServiceTests
{
    private Mock<IRepository> _repositoryMock;
    private Mock<ILogger<Service>> _loggerMock;
    private Service _service;
    
    [SetUp]
    public void Setup()
    {
        _repositoryMock = new Mock<IRepository>();
        _loggerMock = new Mock<ILogger<Service>>();
        _service = new Service(_repositoryMock.Object, _loggerMock.Object);
    }
    
    [Test]
    public async Task GetAsync_ValidId_ReturnsEntity()
    {
        // Arrange
        var id = "123";
        var expected = new Entity { Id = id };
        _repositoryMock.Setup(x => x.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        
        // Act
        var result = await _service.GetAsync(id);
        
        // Assert
        Assert.That(result, Is.EqualTo(expected));
        _repositoryMock.Verify(x => x.GetAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### Test Data Builder Pattern
```csharp
public class ToolRequestBuilder
{
    private string _name = "defaultTool";
    private JObject _arguments = new();
    private string? _connectionId;
    
    public ToolRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }
    
    public ToolRequestBuilder WithArguments(object args)
    {
        _arguments = JObject.FromObject(args);
        return this;
    }
    
    public ToolRequestBuilder WithConnectionId(string connectionId)
    {
        _connectionId = connectionId;
        return this;
    }
    
    public ToolRequest Build()
    {
        return new ToolRequest
        {
            Name = _name,
            Arguments = _arguments,
            ConnectionId = _connectionId
        };
    }
}

// Usage in tests
var request = new ToolRequestBuilder()
    .WithName("calculator")
    .WithArguments(new { operation = "add", a = 1, b = 2 })
    .Build();
```