# Performance Notes

## Large Files and Directories to Avoid

### Binary and Build Artifacts
- **`bin/`** directories - Compiled binaries, can be gigabytes
- **`obj/`** directories - Intermediate build files
- **`.nuget/`** - NuGet package cache
- **`packages/`** - Legacy NuGet packages folder
- **`node_modules/`** - If any JavaScript tools are used

### Generated Files
- **`*.dll`, `*.exe`, `*.pdb`** - Compiled assemblies and symbols
- **`*.cache`** - Build cache files
- **`*.log`** - Log files can grow very large
- **`*.trx`** - Test result files
- **`*.coverage`** - Code coverage data

### Development Tools
- **`.vs/`** - Visual Studio cache
- **`.idea/`** - JetBrains Rider cache
- **`.vscode/`** - VS Code settings (small, but not needed for code analysis)

## Performance-Critical Components

### High-Frequency Operations

#### MessageRouter (`src/McpServer.Application/Server/MessageRouter.cs`)
- **Called for every message**
- **Performance considerations**:
  - Handler lookup is O(1) using dictionary
  - Reflection is cached after first use
  - Async execution prevents blocking
- **Optimization opportunities**:
  - Pre-compile expression trees for handler invocation
  - Use source generators for zero-reflection routing

#### ValidationService (`src/McpServer.Application/Services/ValidationService.cs`)
- **Validates every incoming message**
- **Performance tips**:
  - Cache validator instances
  - Use async validation for I/O operations
  - Fail fast on common validation errors
- **Current optimizations**:
  - Validators are singletons
  - Validation rules are compiled

#### RateLimiter (`src/McpServer.Application/Services/RateLimiter.cs`)
- **Checks every request**
- **Implementation**:
  - Sliding window algorithm
  - In-memory token buckets
  - Lock-free operations where possible
- **Scalability**:
  - Consider distributed rate limiting for multi-instance
  - Use Redis for shared state

### Memory-Intensive Components

#### ConnectionManager (`src/McpServer.Application/Connection/ConnectionManager.cs`)
- **Stores all active connections**
- **Memory per connection**: ~1-2KB base + message buffers
- **Optimization strategies**:
  - Implement connection pooling
  - Set maximum connection limits
  - Clean up inactive connections aggressively

#### ToolResultCache (`src/McpServer.Application/Caching/ToolResultCache.cs`)
- **Caches tool execution results**
- **Memory management**:
  - Size-based eviction policies
  - TTL for all entries
  - Monitor cache hit rates
- **Configuration**:
  ```json
  {
    "Caching": {
      "ToolResults": {
        "MaxSizeMB": 100,
        "DefaultTTL": "00:05:00",
        "MaxEntries": 10000
      }
    }
  }
  ```

#### ResourceContentCache (`src/McpServer.Application/Caching/ResourceContentCache.cs`)
- **Caches file and resource contents**
- **Considerations**:
  - Large files should not be cached
  - Stream large resources instead
  - Implement partial caching for huge resources

## Async/Await Optimization

### ConfigureAwait Usage
```csharp
// Use ConfigureAwait(false) in library code
public async Task<Result> ProcessAsync()
{
    var data = await LoadAsync().ConfigureAwait(false);
    return await TransformAsync(data).ConfigureAwait(false);
}

// Don't use in ASP.NET Core controllers (not needed)
public async Task<IActionResult> GetAsync()
{
    var result = await _service.GetAsync(); // No ConfigureAwait needed
    return Ok(result);
}
```

### Parallel Execution
```csharp
// Good - parallel execution when operations are independent
public async Task<CombinedResult> GetDataAsync()
{
    var task1 = _service1.GetAsync();
    var task2 = _service2.GetAsync();
    var task3 = _service3.GetAsync();
    
    await Task.WhenAll(task1, task2, task3);
    
    return new CombinedResult
    {
        Data1 = task1.Result,
        Data2 = task2.Result,
        Data3 = task3.Result
    };
}
```

### Avoid Sync-over-Async
```csharp
// Bad - blocks thread
public void BadMethod()
{
    var result = GetDataAsync().Result; // Don't do this!
}

// Good - async all the way
public async Task GoodMethodAsync()
{
    var result = await GetDataAsync();
}
```

## JSON Serialization Performance

### System.Text.Json Configuration
```csharp
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false, // More compact
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };
    
    public static readonly JsonSerializerOptions Indented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true // For debugging
    };
}
```

### Source Generation (for hot paths)
```csharp
[JsonSerializable(typeof(ToolRequest))]
[JsonSerializable(typeof(ToolResponse))]
public partial class McpJsonContext : JsonSerializerContext
{
}

// Usage
var json = JsonSerializer.Serialize(request, McpJsonContext.Default.ToolRequest);
```

## Database and I/O Optimization

### Connection Pooling
```csharp
// For SQL connections
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(3);
    });
}, ServiceLifetime.Scoped);

// For HTTP clients
services.AddHttpClient<ApiClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        MaxConnectionsPerServer = 10
    });
```

### Batching Operations
```csharp
public async Task ProcessBatchAsync<T>(IEnumerable<T> items, Func<T, Task> processor)
{
    const int batchSize = 100;
    var semaphore = new SemaphoreSlim(10); // Max 10 concurrent operations
    
    var tasks = new List<Task>();
    
    foreach (var batch in items.Chunk(batchSize))
    {
        foreach (var item in batch)
        {
            await semaphore.WaitAsync();
            tasks.Add(ProcessItemAsync(item, processor, semaphore));
        }
    }
    
    await Task.WhenAll(tasks);
}

private async Task ProcessItemAsync<T>(T item, Func<T, Task> processor, SemaphoreSlim semaphore)
{
    try
    {
        await processor(item);
    }
    finally
    {
        semaphore.Release();
    }
}
```

## Memory Management

### Object Pooling
```csharp
public class BufferPool
{
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    
    public byte[] Rent(int minimumLength)
    {
        return _arrayPool.Rent(minimumLength);
    }
    
    public void Return(byte[] array, bool clearArray = false)
    {
        _arrayPool.Return(array, clearArray);
    }
}

// Usage
var buffer = _bufferPool.Rent(4096);
try
{
    // Use buffer
}
finally
{
    _bufferPool.Return(buffer);
}
```

### Memory-Efficient Collections
```csharp
// Use capacity when size is known
var list = new List<Item>(expectedCount);

// Use ArraySegment for views
var segment = new ArraySegment<byte>(buffer, offset, count);

// Use Memory<T> and Span<T> for zero-allocation slicing
ReadOnlyMemory<byte> memory = buffer.AsMemory(0, length);
```

## Monitoring and Profiling

### Performance Counters
```csharp
public class PerformanceMonitor
{
    private readonly IMetricsService _metrics;
    
    public IDisposable MeasureOperation(string operationName)
    {
        return new OperationTimer(_metrics, operationName);
    }
    
    private class OperationTimer : IDisposable
    {
        private readonly IMetricsService _metrics;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        
        public OperationTimer(IMetricsService metrics, string operationName)
        {
            _metrics = metrics;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            _metrics.RecordDuration(_operationName, _stopwatch.Elapsed);
        }
    }
}
```

### Key Metrics to Monitor
1. **Request latency** - P50, P95, P99
2. **Throughput** - Requests per second
3. **Error rates** - By error type
4. **Resource usage** - CPU, memory, connections
5. **Cache performance** - Hit rates, evictions

## Optimization Checklist

### Before Optimizing
- [ ] Profile to identify bottlenecks
- [ ] Measure baseline performance
- [ ] Set performance targets
- [ ] Consider trade-offs (memory vs CPU)

### Common Optimizations
- [ ] Enable response caching where appropriate
- [ ] Implement pagination for large results
- [ ] Use streaming for large payloads
- [ ] Batch database operations
- [ ] Optimize hot paths with profiler data
- [ ] Review and optimize LINQ queries
- [ ] Consider async enumerable for large collections

### After Optimizing
- [ ] Measure improvement
- [ ] Document changes and rationale
- [ ] Add performance tests
- [ ] Monitor in production

## Configuration Tuning

### Thread Pool Settings
```json
{
  "ThreadPool": {
    "MinWorkerThreads": 50,
    "MinCompletionPortThreads": 50,
    "MaxWorkerThreads": 500,
    "MaxCompletionPortThreads": 500
  }
}
```

### GC Settings
```xml
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
  <GCRetainVM>true</GCRetainVM>
</PropertyGroup>
```

### ASP.NET Core Settings
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 1000;
    options.Limits.MaxConcurrentUpgradedConnections = 1000;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    options.Limits.MinRequestBodyDataRate = new MinDataRate(
        bytesPerSecond: 100, 
        gracePeriod: TimeSpan.FromSeconds(10));
});
```