# Quick Fixes for Common Issues

## Connection Issues

### Client Cannot Connect

#### Symptom: "Connection refused" or timeout
```bash
# Check if server is running
netstat -an | grep 8080  # For web server
ps aux | grep McpServer  # For process

# Test endpoints
curl http://localhost:8080/health
curl -X POST http://localhost:8080/sse
```

#### Fixes:
1. **Verify transport is enabled**:
   ```json
   {
     "Transport": {
       "Sse": { "Enabled": true },
       "WebSocket": { "Enabled": true }
     }
   }
   ```

2. **Check port conflicts**:
   ```bash
   lsof -i :8080  # Mac/Linux
   netstat -ano | findstr :8080  # Windows
   ```

3. **Firewall/CORS issues**:
   ```json
   {
     "Transport": {
       "Sse": {
         "AllowedOrigins": ["*"],  // For testing only!
         "RequireHttps": false
       }
     }
   }
   ```

### Stdio Transport Not Working

#### Symptom: No response from console app
```bash
# Test with direct input
echo '{"jsonrpc":"2.0","method":"ping","id":1}' | dotnet run
```

#### Fix:
```csharp
// Ensure Console.IsInputRedirected is handled
if (!Console.IsInputRedirected)
{
    Console.WriteLine("Waiting for input...");
}
```

## Authentication Failures

### API Key Not Working

#### Symptom: 401 Unauthorized with valid key
```bash
curl -H "X-API-Key: mykey" http://localhost:8080/health
```

#### Fixes:
1. **Check header name matches config**:
   ```json
   {
     "Authentication": {
       "ApiKey": {
         "HeaderName": "X-API-Key",  // Must match exactly
         "Keys": ["mykey"]
       }
     }
   }
   ```

2. **Verify authentication is enabled**:
   ```json
   {
     "Authentication": {
       "Enabled": true,
       "Provider": "ApiKey"
     }
   }
   ```

### OAuth Login Fails

#### Symptom: Redirect loop or "Invalid redirect URI"

#### Fix:
1. **Update OAuth app settings** (e.g., GitHub):
   - Redirect URI: `http://localhost:8080/auth/callback/github`
   - Must match exactly including protocol and port

2. **Check OAuth config**:
   ```json
   {
     "OAuth": {
       "GitHub": {
         "ClientId": "your-id",
         "ClientSecret": "your-secret",
         "RedirectUri": "http://localhost:8080/auth/callback/github"
       }
     }
   }
   ```

### Session Expires Too Quickly

#### Fix:
```json
{
  "Authentication": {
    "SessionTimeout": "08:00:00",  // 8 hours
    "SlidingExpiration": true
  }
}
```

## Performance Issues

### High Memory Usage

#### Symptoms: Memory growing unbounded

#### Fixes:
1. **Configure cache limits**:
   ```json
   {
     "Caching": {
       "MemoryLimit": 200,  // MB
       "SizeLimit": 10000,  // Max entries
       "CompactionPercentage": 0.25
     }
   }
   ```

2. **Enable connection limits**:
   ```json
   {
     "ConnectionMultiplexing": {
       "MaxConnectionsPerClient": 5,
       "MaxTotalConnections": 1000
     }
   }
   ```

3. **Force garbage collection** (temporary):
   ```csharp
   // In a diagnostic endpoint
   GC.Collect();
   GC.WaitForPendingFinalizers();
   GC.Collect();
   ```

### Slow Response Times

#### Quick Diagnostics:
```csharp
// Add to appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.AspNetCore": "Debug",
      "McpServer": "Debug"
    }
  }
}
```

#### Fixes:
1. **Enable caching**:
   ```json
   {
     "Caching": {
       "Enabled": true,
       "DefaultExpiration": "00:05:00"
     }
   }
   ```

2. **Increase thread pool**:
   ```csharp
   // In Program.cs
   ThreadPool.SetMinThreads(50, 50);
   ```

3. **Check rate limits**:
   ```json
   {
     "RateLimiting": {
       "Default": {
         "Limit": 1000,  // Increase for testing
         "Window": "00:01:00"
       }
     }
   }
   ```

## Tool Execution Errors

### Tool Not Found

#### Symptom: "Unknown tool: myTool"

#### Fixes:
1. **Verify tool registration**:
   ```csharp
   // In Program.cs or Startup
   services.AddSingleton<ITool, MyTool>();
   ```

2. **Check tool name matches**:
   ```csharp
   public class MyTool : ITool
   {
       public string Name => "myTool";  // Case sensitive!
   }
   ```

3. **Enable tool in config**:
   ```json
   {
     "Tools": {
       "Enabled": ["myTool"],
       "Disabled": []
     }
   }
   ```

### Tool Timeout

#### Symptom: "Tool execution timed out"

#### Fix:
```json
{
  "Tools": {
    "DefaultTimeout": "00:05:00",  // Increase timeout
    "Timeouts": {
      "slowTool": "00:10:00"  // Tool-specific timeout
    }
  }
}
```

## Resource Access Issues

### File Not Found

#### Symptom: "Resource not found: file:///path/to/file"

#### Fixes:
1. **Check path format**:
   ```csharp
   // Windows paths need proper escaping
   "file:///C:/Users/name/file.txt"  // Correct
   "file://C:\\Users\\name\\file.txt"  // Wrong
   ```

2. **Verify permissions**:
   ```bash
   # Check file permissions
   ls -la /path/to/file  # Unix
   icacls "C:\path\to\file"  # Windows
   ```

3. **Add to allowed paths**:
   ```json
   {
     "Resources": {
       "FileSystem": {
         "AllowedPaths": ["/home/user/documents", "C:\\Users\\Public"]
       }
     }
   }
   ```

## Logging Issues

### No Logs Appearing

#### Fix:
1. **Check log level**:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "McpServer": "Debug"
       }
     }
   }
   ```

2. **Verify Serilog config**:
   ```json
   {
     "Serilog": {
       "MinimumLevel": "Debug",
       "WriteTo": [
         {
           "Name": "Console"
         },
         {
           "Name": "File",
           "Args": {
             "path": "logs/log-.txt",
             "rollingInterval": "Day"
           }
         }
       ]
     }
   }
   ```

### Log Files Too Large

#### Fix:
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,  // Keep only 7 days
          "fileSizeLimitBytes": 10485760  // 10MB per file
        }
      }
    ]
  }
}
```

## Development Environment Issues

### Hot Reload Not Working

#### Fix for .NET 6+:
```bash
# Use dotnet watch
dotnet watch run

# Or in launchSettings.json
{
  "profiles": {
    "Development": {
      "environmentVariables": {
        "DOTNET_WATCH_RESTART_ON_RUDE_EDIT": "true"
      }
    }
  }
}
```

### Docker Build Fails

#### Common fixes:
1. **Clear Docker cache**:
   ```bash
   docker system prune -a
   docker build --no-cache .
   ```

2. **Check .dockerignore**:
   ```
   **/bin
   **/obj
   **/.vs
   **/.vscode
   **/logs
   ```

3. **Multi-stage build issue**:
   ```dockerfile
   # Ensure all projects are restored
   COPY *.sln .
   COPY src/McpServer.Domain/*.csproj ./src/McpServer.Domain/
   # ... copy all project files first
   RUN dotnet restore
   ```

## Testing Issues

### Integration Tests Fail Randomly

#### Fixes:
1. **Use unique test databases**:
   ```csharp
   services.AddDbContext<TestDbContext>(options =>
       options.UseInMemoryDatabase($"Test_{Guid.NewGuid()}"));
   ```

2. **Proper async test setup**:
   ```csharp
   [Test]
   public async Task TestMethod()
   {
       await using var server = new TestServer();
       await server.StartAsync();
       // ... test code
   }
   ```

3. **Reset state between tests**:
   ```csharp
   [TearDown]
   public async Task Cleanup()
   {
       await _connectionManager.DisconnectAllAsync();
       _cache.Clear();
   }
   ```

## Quick Diagnostic Commands

### Health Check
```bash
# Basic health
curl http://localhost:8080/health

# Detailed health
curl http://localhost:8080/health/ready

# With jq for pretty output
curl http://localhost:8080/health | jq .
```

### Test Message
```bash
# Ping test
echo '{"jsonrpc":"2.0","method":"ping","id":1}' | curl -X POST -H "Content-Type: application/json" -d @- http://localhost:8080/rpc

# With authentication
curl -X POST -H "Content-Type: application/json" -H "X-API-Key: mykey" -d '{"jsonrpc":"2.0","method":"tools/list","id":1}' http://localhost:8080/rpc
```

### Performance Check
```bash
# Simple load test
for i in {1..100}; do
  curl -s http://localhost:8080/health > /dev/null &
done
wait

# Monitor connections
watch -n 1 'netstat -an | grep :8080 | grep ESTABLISHED | wc -l'
```

## Emergency Fixes

### Server Won't Start
1. Delete temporary files:
   ```bash
   rm -rf bin obj
   dotnet clean
   dotnet restore
   ```

2. Reset configuration:
   ```bash
   cp appsettings.json appsettings.json.backup
   cp appsettings.Development.json.template appsettings.Development.json
   ```

3. Check port availability:
   ```bash
   # Kill process on port 8080
   lsof -ti:8080 | xargs kill -9  # Mac/Linux
   netstat -ano | findstr :8080  # Windows (then kill PID)
   ```

### Memory Leak Suspected
1. Enable detailed GC logging:
   ```bash
   export DOTNET_gcServer=1
   export DOTNET_GCHeapCount=8
   export DOTNET_GCLogEnabled=1
   ```

2. Take memory dump:
   ```bash
   dotnet-dump collect -p <PID>
   dotnet-dump analyze <dump-file>
   ```

3. Quick memory profile:
   ```csharp
   // Add diagnostic endpoint
   app.MapGet("/diag/memory", () => new
   {
       WorkingSet = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024,
       GC = new
       {
           Gen0 = GC.CollectionCount(0),
           Gen1 = GC.CollectionCount(1),
           Gen2 = GC.CollectionCount(2),
           TotalMemory = GC.GetTotalMemory(false) / 1024 / 1024
       }
   });
   ```