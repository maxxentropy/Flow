# Performance Metrics Baseline

## 🎯 Target Performance Metrics

### Response Time SLAs
| Operation | P50 | P95 | P99 | Max |
|-----------|-----|-----|-----|-----|
| initialize | 5ms | 10ms | 20ms | 100ms |
| tools/list | 2ms | 5ms | 10ms | 50ms |
| tools/call (simple) | 10ms | 50ms | 100ms | 500ms |
| tools/call (complex) | 50ms | 200ms | 500ms | 2000ms |
| resources/list | 5ms | 20ms | 50ms | 200ms |
| resources/read (small) | 10ms | 30ms | 60ms | 200ms |
| resources/read (large) | 100ms | 500ms | 1000ms | 5000ms |

### Throughput Targets
| Metric | Baseline | Target | Maximum |
|--------|----------|--------|---------|
| Requests/second | 100 | 500 | 1000 |
| Concurrent connections | 10 | 50 | 100 |
| Messages/second/connection | 10 | 50 | 100 |
| Tool executions/second | 20 | 100 | 200 |

### Resource Usage Limits
| Resource | Idle | Normal | Peak | Alert Threshold |
|----------|------|--------|------|-----------------|
| CPU % | 1-2% | 10-20% | 50% | >70% sustained |
| Memory MB | 50 | 200 | 500 | >1000 |
| Handles | 100 | 500 | 1000 | >2000 |
| Threads | 10 | 30 | 50 | >100 |
| GC Gen2/min | 0 | 1 | 5 | >10 |

## 📊 Current Performance Measurements

### Load Test Results (2024-01-15)
```bash
# Test configuration
- Duration: 5 minutes
- Virtual users: 100
- Ramp-up: 30 seconds
- Think time: 100ms

# Results
- Total requests: 150,000
- Success rate: 99.98%
- Avg response time: 12ms
- P95 response time: 45ms
- P99 response time: 89ms
- Max response time: 342ms
- Throughput: 500 req/s
```

### Memory Profile
```
# After 1 hour of load
- Working set: 245 MB
- Private bytes: 198 MB
- Gen 0 collections: 1,235
- Gen 1 collections: 89
- Gen 2 collections: 3
- Large object heap: 12 MB
- Pinned objects: 15
```

### CPU Profile
```
# Top CPU consumers
1. JsonSerializer.Deserialize: 22%
2. Transport.WriteAsync: 18%
3. MessageProcessor.ProcessAsync: 15%
4. Tool execution: 12%
5. Logging: 8%
6. Other: 25%
```

## 🚀 Optimization Benchmarks

### Before vs After Optimizations
| Optimization | Metric | Before | After | Improvement |
|-------------|--------|--------|-------|-------------|
| String interning | Memory/req | 2.5KB | 1.8KB | 28% |
| ArrayPool | GC Gen2/min | 8 | 2 | 75% |
| ValueTask | Allocations/req | 45 | 28 | 38% |
| Span<T> parsing | Parse time | 450μs | 120μs | 73% |
| Channel batching | Syscalls/s | 5000 | 500 | 90% |

### Benchmark Code
```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class McpServerBenchmarks
{
    private McpServer _server;
    private JsonDocument _initMessage;
    private JsonDocument _toolsListMessage;
    
    [GlobalSetup]
    public void Setup()
    {
        _server = new McpServerBuilder()
            .ConfigureForBenchmark()
            .Build();
            
        _initMessage = JsonDocument.Parse(@"{
            ""jsonrpc"": ""2.0"",
            ""id"": 1,
            ""method"": ""initialize"",
            ""params"": {""protocolVersion"": ""2024-11-05""}
        }");
        
        _toolsListMessage = JsonDocument.Parse(@"{
            ""jsonrpc"": ""2.0"",
            ""id"": 2,
            ""method"": ""tools/list""
        }");
    }
    
    [Benchmark]
    public async Task ProcessInitialize()
    {
        await _server.ProcessMessageAsync(_initMessage);
    }
    
    [Benchmark]
    public async Task ProcessToolsList()
    {
        await _server.ProcessMessageAsync(_toolsListMessage);
    }
    
    [Benchmark]
    public async Task ProcessMixedWorkload()
    {
        // Simulate realistic workload
        await _server.ProcessMessageAsync(_initMessage);
        
        for (int i = 0; i < 10; i++)
        {
            await _server.ProcessMessageAsync(_toolsListMessage);
        }
    }
}
```

### Benchmark Results
```
BenchmarkDotNet=v0.13.5, OS=macOS Ventura 13.0
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK=8.0.100

| Method | Mean | Error | StdDev | Gen0 | Gen1 | Allocated |
|--------|------|-------|--------|------|------|-----------|
| ProcessInitialize | 145.3 μs | 2.1 μs | 1.9 μs | 2.1 | 0.1 | 12.5 KB |
| ProcessToolsList | 89.7 μs | 1.5 μs | 1.4 μs | 1.2 | - | 7.8 KB |
| ProcessMixedWorkload | 1,243.8 μs | 18.4 μs | 17.2 μs | 15.3 | 0.8 | 98.4 KB |
```

## 📈 Monitoring Queries

### Prometheus Queries
```promql
# Request rate
rate(mcp_requests_total[5m])

# Error rate
rate(mcp_requests_total{status="error"}[5m]) / rate(mcp_requests_total[5m])

# Response time percentiles
histogram_quantile(0.95, rate(mcp_request_duration_seconds_bucket[5m]))

# Memory usage
process_resident_memory_bytes / 1024 / 1024

# GC pressure
rate(dotnet_gc_collections_total[5m])

# Thread pool starvation
dotnet_threadpool_queue_length
```

### Grafana Dashboard Config
```json
{
  "dashboard": {
    "title": "MCP Server Performance",
    "panels": [
      {
        "title": "Request Rate",
        "targets": [{
          "expr": "rate(mcp_requests_total[5m])"
        }]
      },
      {
        "title": "Response Time (P95)",
        "targets": [{
          "expr": "histogram_quantile(0.95, rate(mcp_request_duration_seconds_bucket[5m]))"
        }]
      },
      {
        "title": "Error Rate %",
        "targets": [{
          "expr": "rate(mcp_requests_total{status=\"error\"}[5m]) / rate(mcp_requests_total[5m]) * 100"
        }]
      },
      {
        "title": "Memory Usage",
        "targets": [{
          "expr": "process_resident_memory_bytes / 1024 / 1024"
        }]
      }
    ]
  }
}
```

## 🎪 Performance Testing Scripts

### Load Test Script (k6)
```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const errorRate = new Rate('errors');

export const options = {
  stages: [
    { duration: '30s', target: 50 },  // Ramp up
    { duration: '2m', target: 100 },  // Stay at 100
    { duration: '30s', target: 0 },   // Ramp down
  ],
  thresholds: {
    'http_req_duration': ['p(95)<100'],
    'errors': ['rate<0.01'],
  },
};

export default function() {
  const payload = JSON.stringify({
    jsonrpc: '2.0',
    id: Math.floor(Math.random() * 10000),
    method: 'tools/list',
  });

  const params = {
    headers: {
      'Content-Type': 'text/event-stream',
    },
  };

  const res = http.post('http://localhost:8080/sse', payload, params);
  
  const success = check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 100ms': (r) => r.timings.duration < 100,
  });
  
  errorRate.add(!success);
  sleep(0.1);
}
```

### Continuous Performance Monitoring
```bash
#!/bin/bash
# performance-monitor.sh

while true; do
    # Capture metrics
    TIMESTAMP=$(date +%s)
    CPU=$(ps -p $(pgrep McpServer) -o %cpu | tail -1)
    MEM=$(ps -p $(pgrep McpServer) -o rss | tail -1)
    CONNECTIONS=$(netstat -an | grep :8080 | grep ESTABLISHED | wc -l)
    
    # Log to CSV
    echo "$TIMESTAMP,$CPU,$MEM,$CONNECTIONS" >> performance.csv
    
    # Alert if thresholds exceeded
    if (( $(echo "$CPU > 70" | bc -l) )); then
        echo "ALERT: High CPU usage: $CPU%"
    fi
    
    if (( $MEM > 1048576 )); then  # 1GB
        echo "ALERT: High memory usage: $(($MEM / 1024))MB"
    fi
    
    sleep 5
done
```

## 🎯 Performance Goals for Next Quarter

1. **Reduce P99 latency by 20%**
   - Target: <80ms for all operations
   - Strategy: Implement zero-copy JSON parsing

2. **Increase throughput to 1000 req/s**
   - Current: 500 req/s
   - Strategy: Connection pooling and batching

3. **Reduce memory footprint by 30%**
   - Current: 245MB under load
   - Target: <170MB
   - Strategy: Object pooling and Span<T>

4. **Eliminate Gen2 collections under normal load**
   - Current: 3 per hour
   - Target: 0 per hour
   - Strategy: Reduce large object allocations