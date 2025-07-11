# Performance Optimization Opportunities

## 🚀 Quick Wins (< 1 day each)

### OPT-001: Enable Server GC
**Location**: All host projects  
**Current**: Workstation GC (default)  
**Improvement**: ~20% throughput increase  
**Implementation**:
```xml
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
</PropertyGroup>
```

### OPT-002: Use ValueTask for Hot Paths
**Location**: ITransport.ReadAsync, ITransport.WriteAsync  
**Current**: Task<T>  
**Improvement**: Reduce allocations by ~40%  
**Implementation**:
```csharp
public ValueTask<Message?> ReadAsync(CancellationToken ct)
{
    if (_messageQueue.TryDequeue(out var message))
        return new ValueTask<Message?>(message);
    
    return ReadAsyncSlow(ct);
}
```

### OPT-003: String Interning for Method Names
**Location**: JsonRpcProcessor  
**Current**: New string allocations  
**Improvement**: ~5% memory reduction  
**Implementation**:
```csharp
private static readonly HashSet<string> KnownMethods = new()
{
    "initialize", "tools/list", "tools/call", "resources/list"
};

method = KnownMethods.TryGetValue(method, out var interned) 
    ? interned 
    : string.Intern(method);
```

## 🎯 Medium Effort (1-3 days)

### OPT-004: Implement ArrayPool for JSON Buffers
**Location**: Transport serialization  
**Current**: New byte[] allocations  
**Improvement**: ~30% reduction in Gen 2 collections  
**Implementation**:
```csharp
var buffer = ArrayPool<byte>.Shared.Rent(4096);
try
{
    var written = JsonSerializer.Serialize(buffer, message);
    await stream.WriteAsync(buffer.AsMemory(0, written));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### OPT-005: Batch SSE Writes
**Location**: SseTransport  
**Current**: Individual writes per message  
**Improvement**: ~50% reduction in syscalls  
**Implementation**:
```csharp
private readonly Channel<Message> _outboundQueue;

private async Task ProcessOutboundMessages()
{
    var batch = new List<Message>(10);
    while (await _outboundQueue.Reader.WaitToReadAsync())
    {
        while (_outboundQueue.Reader.TryRead(out var msg) && batch.Count < 10)
            batch.Add(msg);
        
        await WriteBatch(batch);
        batch.Clear();
    }
}
```

### OPT-006: Cache Tool Schemas
**Location**: ToolRegistry  
**Current**: Schema generation on each request  
**Improvement**: ~100ms saved per tools/list  
**Implementation**:
```csharp
private readonly ConcurrentDictionary<Type, ToolSchema> _schemaCache = new();

public ToolSchema GetSchema(Type toolType)
{
    return _schemaCache.GetOrAdd(toolType, type => 
    {
        // Generate schema once
        return GenerateSchema(type);
    });
}
```

## 🏗️ Major Optimizations (3-5 days)

### OPT-007: Implement Zero-Copy JSON Parsing
**Location**: Message deserialization  
**Current**: Full object materialization  
**Improvement**: ~60% faster for large payloads  
**Implementation**:
```csharp
public async Task ProcessMessage(ReadOnlyMemory<byte> buffer)
{
    var reader = new Utf8JsonReader(buffer.Span);
    
    // Parse without allocating strings
    while (reader.Read())
    {
        if (reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("method"u8))
            {
                // Direct UTF-8 comparison
            }
        }
    }
}
```

### OPT-008: Connection Pooling for Resources
**Location**: HttpResourceProvider  
**Current**: New HttpClient per request  
**Improvement**: ~70% faster resource access  
**Implementation**:
```csharp
services.AddHttpClient<IResourceProvider, HttpResourceProvider>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        MaxConnectionsPerServer = 10
    });
```

### OPT-009: Async State Machine Optimization
**Location**: Core async methods  
**Current**: Default compiler generation  
**Improvement**: ~15% faster async operations  
**Implementation**:
```csharp
[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
public async ValueTask<Result> ProcessAsync()
{
    // Use IValueTaskSource for truly allocation-free async
}
```

## 📊 Measurement Strategy

### Before Optimization
```bash
dotnet run -c Release --no-build -- benchmark
```

### Key Metrics to Track
1. **Throughput**: Requests/second
2. **Latency**: P50, P95, P99
3. **Memory**: Allocations/request, Gen 2 collections
4. **CPU**: % utilization under load

### Benchmark Code
```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class TransportBenchmarks
{
    [Benchmark]
    public async Task ProcessSingleMessage()
    {
        // Benchmark implementation
    }
    
    [Benchmark]
    public async Task ProcessMessageBatch()
    {
        // Benchmark implementation
    }
}
```

## 🎲 Experimental Optimizations

### EXP-001: SIMD for JSON Parsing
**Potential**: 2-3x faster parsing  
**Risk**: Platform-specific code  
**Research**: SimdJson.NET integration

### EXP-002: io_uring for Linux
**Potential**: 50% better I/O performance  
**Risk**: Linux-only optimization  
**Research**: New .NET 8 APIs

### EXP-003: Native AOT Compilation
**Potential**: 80% smaller memory footprint  
**Risk**: Reflection limitations  
**Research**: Trimming compatibility

## 📈 Expected Overall Impact

Implementing all optimizations:
- **Throughput**: +150% (2.5x)
- **Latency**: -40% (P99)
- **Memory**: -60% allocations
- **Startup**: -70% time

## 🔄 Optimization Workflow

1. **Measure** baseline with benchmarks
2. **Implement** one optimization at a time
3. **Verify** no functional regressions
4. **Measure** improvement
5. **Document** in this file
6. **Deploy** with feature flag if risky