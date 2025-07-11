# Safe Refactoring Patterns

## 🎯 Refactoring Decision Matrix

| Code Smell | Risk | Effort | Pattern | Priority |
|------------|------|--------|---------|----------|
| Long method (>50 lines) | Low | Low | Extract Method | High |
| Large class (>500 lines) | Medium | Medium | Extract Class | High |
| Deep nesting (>3 levels) | Low | Low | Guard Clauses | High |
| Duplicate code | Medium | Medium | Extract Common | Medium |
| Feature envy | Medium | High | Move Method | Medium |
| God class | High | High | Split Responsibilities | Low |

## 🔒 Safety Checklist

### Before Starting
- [ ] All tests passing
- [ ] Code coverage baseline recorded
- [ ] Performance baseline measured
- [ ] Branch created from main
- [ ] Commit current work

### During Refactoring
- [ ] One change at a time
- [ ] Tests pass after each change
- [ ] Commit after each successful step
- [ ] Keep refactoring separate from features

### After Completion
- [ ] All tests still passing
- [ ] Coverage maintained or improved
- [ ] Performance not degraded
- [ ] Code reviewed
- [ ] Documentation updated

## 📚 Common Refactoring Patterns

### 1. Extract Method
```csharp
// Before
public async Task ProcessRequestAsync(Request request)
{
    // Validation logic (20 lines)
    if (request == null) throw new ArgumentNullException(nameof(request));
    if (string.IsNullOrEmpty(request.Id)) throw new ArgumentException("Id required");
    if (request.Items == null || !request.Items.Any()) throw new ArgumentException("Items required");
    // ... more validation
    
    // Business logic (30 lines)
    var result = new Result();
    foreach (var item in request.Items)
    {
        // Complex processing
    }
    
    // Logging and metrics (15 lines)
    _logger.LogInformation("Processed request {Id}", request.Id);
    _metrics.Increment("requests.processed");
    // ... more logging
}

// After
public async Task ProcessRequestAsync(Request request)
{
    ValidateRequest(request);
    var result = await ExecuteBusinessLogicAsync(request);
    LogRequestCompletion(request, result);
}

private void ValidateRequest(Request request)
{
    if (request == null) throw new ArgumentNullException(nameof(request));
    if (string.IsNullOrEmpty(request.Id)) throw new ArgumentException("Id required");
    if (request.Items == null || !request.Items.Any()) throw new ArgumentException("Items required");
}

private async Task<Result> ExecuteBusinessLogicAsync(Request request)
{
    var result = new Result();
    foreach (var item in request.Items)
    {
        // Complex processing
    }
    return result;
}

private void LogRequestCompletion(Request request, Result result)
{
    _logger.LogInformation("Processed request {Id}", request.Id);
    _metrics.Increment("requests.processed");
}
```

### 2. Replace Nested Conditionals with Guard Clauses
```csharp
// Before
public async Task<Result> ProcessAsync(Input input)
{
    if (input != null)
    {
        if (input.IsValid)
        {
            if (await CanProcessAsync(input))
            {
                // Actual logic
                return await DoProcessAsync(input);
            }
            else
            {
                throw new InvalidOperationException("Cannot process");
            }
        }
        else
        {
            throw new ArgumentException("Invalid input");
        }
    }
    else
    {
        throw new ArgumentNullException(nameof(input));
    }
}

// After
public async Task<Result> ProcessAsync(Input input)
{
    if (input == null)
        throw new ArgumentNullException(nameof(input));
    
    if (!input.IsValid)
        throw new ArgumentException("Invalid input");
    
    if (!await CanProcessAsync(input))
        throw new InvalidOperationException("Cannot process");
    
    return await DoProcessAsync(input);
}
```

### 3. Extract Interface
```csharp
// Before
public class MessageProcessor
{
    public async Task<Response> ProcessAsync(Message message) { }
    public void RegisterHandler(IHandler handler) { }
    public void UnregisterHandler(string method) { }
}

// After
public interface IMessageProcessor
{
    Task<Response> ProcessAsync(Message message);
}

public interface IHandlerRegistry
{
    void RegisterHandler(IHandler handler);
    void UnregisterHandler(string method);
}

public class MessageProcessor : IMessageProcessor, IHandlerRegistry
{
    // Implementation
}
```

### 4. Replace Magic Numbers with Constants
```csharp
// Before
public class TransportOptions
{
    public int BufferSize { get; set; } = 4096;
    public int Timeout { get; set; } = 30000;
    public int MaxRetries { get; set; } = 3;
}

// After
public class TransportOptions
{
    private const int DefaultBufferSize = 4096;
    private const int DefaultTimeoutMs = 30000;
    private const int DefaultMaxRetries = 3;
    
    public int BufferSize { get; set; } = DefaultBufferSize;
    public int Timeout { get; set; } = DefaultTimeoutMs;
    public int MaxRetries { get; set; } = DefaultMaxRetries;
}
```

### 5. Introduce Parameter Object
```csharp
// Before
public async Task<ToolResult> ExecuteToolAsync(
    string toolName,
    Dictionary<string, object> parameters,
    string userId,
    string correlationId,
    TimeSpan timeout,
    CancellationToken cancellationToken)
{
    // Method body
}

// After
public record ToolExecutionContext(
    string ToolName,
    Dictionary<string, object> Parameters,
    string UserId,
    string CorrelationId,
    TimeSpan Timeout);

public async Task<ToolResult> ExecuteToolAsync(
    ToolExecutionContext context,
    CancellationToken cancellationToken)
{
    // Method body
}
```

## 🛠️ Refactoring Tools

### Automated Refactoring Commands

```bash
# Find long methods
find . -name "*.cs" -exec grep -l "^[[:space:]]*{" {} \; | \
  xargs -I {} sh -c 'echo "File: {}" && \
  awk "/^[[:space:]]*{/{count++} /^[[:space:]]*}/{count--; if(count==0) lines=0} \
  {lines++} lines>50 && count>0 {print FILENAME \":\" NR \":\" lines}" {}'

# Find duplicate code
dotnet tool install -g dotnet-duplicates
dotnet-duplicates analyze --min-lines 10

# Analyze complexity
dotnet tool install -g dotnet-complexity
dotnet-complexity analyze --max-complexity 10
```

### IDE Refactoring Shortcuts

```csharp
// Visual Studio / Rider shortcuts
// Ctrl+R, M - Extract Method
// Ctrl+R, I - Extract Interface  
// Ctrl+R, F - Extract Field
// Ctrl+R, P - Extract Parameter
// Ctrl+R, V - Extract Variable
// Ctrl+R, R - Rename
// Ctrl+Shift+R - Refactor This menu
```

## 🎯 Specific MCP Server Refactorings

### 1. Consolidate Message Handlers
```csharp
// Before - Scattered handlers
public class InitializeHandler { }
public class ToolsListHandler { }
public class ToolsCallHandler { }

// After - Cohesive handler groups
public interface IProtocolHandler
{
    Task<Response> HandleInitializeAsync(InitializeRequest request);
}

public interface IToolHandler  
{
    Task<Response> HandleListAsync(ListRequest request);
    Task<Response> HandleCallAsync(CallRequest request);
}
```

### 2. Extract Transport Abstractions
```csharp
// Before - Concrete implementations
public class StdioTransport
{
    private Stream _input;
    private Stream _output;
    // Lots of stdio-specific code
}

// After - Shared abstractions
public abstract class StreamTransport : ITransport
{
    protected abstract Stream GetInputStream();
    protected abstract Stream GetOutputStream();
    
    // Common stream handling logic
}

public class StdioTransport : StreamTransport
{
    protected override Stream GetInputStream() => Console.OpenStandardInput();
    protected override Stream GetOutputStream() => Console.OpenStandardOutput();
}
```

### 3. Simplify Tool Registration
```csharp
// Before - Manual registration
services.AddSingleton<ITool, CalculatorTool>();
services.AddSingleton<ITool, FileReaderTool>();
services.AddSingleton<ITool, HttpClientTool>();

// After - Convention-based
services.AddToolsFromAssembly(typeof(Program).Assembly);

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddToolsFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ITool).IsAssignableFrom(t));
        
        foreach (var toolType in toolTypes)
        {
            services.AddSingleton(typeof(ITool), toolType);
        }
        
        return services;
    }
}
```

## 📊 Measuring Refactoring Success

### Code Metrics Before/After
```csharp
public class RefactoringMetrics
{
    public void MeasureBeforeAfter(string className)
    {
        var metrics = new Dictionary<string, object>
        {
            ["LinesOfCode"] = CountLines(className),
            ["CyclomaticComplexity"] = CalculateComplexity(className),
            ["CouplingBetweenObjects"] = CountDependencies(className),
            ["ResponseForClass"] = CountResponsibilities(className),
            ["LackOfCohesion"] = CalculateCohesion(className)
        };
        
        Console.WriteLine($"Metrics for {className}:");
        foreach (var metric in metrics)
        {
            Console.WriteLine($"  {metric.Key}: {metric.Value}");
        }
    }
}
```

### Test Coverage Validation
```bash
# Before refactoring
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput=./coverage-before.json

# After refactoring  
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput=./coverage-after.json

# Compare coverage
dotnet tool install -g dotnet-coverage-report
coverage-report compare coverage-before.json coverage-after.json
```

## 🚫 Refactoring Anti-Patterns

### 1. Big Bang Refactoring
❌ **Don't**: Refactor everything at once
✅ **Do**: Small, incremental changes

### 2. Refactoring Without Tests
❌ **Don't**: Refactor untested code
✅ **Do**: Add tests first, then refactor

### 3. Mixing Features and Refactoring
❌ **Don't**: Add new features while refactoring
✅ **Do**: Separate PRs for features and refactoring

### 4. Over-Engineering
❌ **Don't**: Add abstractions "just in case"
✅ **Do**: Refactor based on actual needs

## 📝 Refactoring Checklist Template

```markdown
## Refactoring: [Name]

### Motivation
- [ ] Code smell identified: ___________
- [ ] Measurable improvement expected: ___________

### Pre-refactoring
- [ ] Tests cover affected code
- [ ] Baseline metrics recorded
- [ ] Branch created

### Steps
1. [ ] Step 1: ___________ (commit: abc123)
2. [ ] Step 2: ___________ (commit: def456)
3. [ ] Step 3: ___________ (commit: ghi789)

### Validation  
- [ ] All tests passing
- [ ] No performance regression
- [ ] Code coverage maintained
- [ ] Metrics improved

### Review
- [ ] Self-review completed
- [ ] Peer review requested
- [ ] Documentation updated
```