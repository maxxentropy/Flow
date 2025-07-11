# Claude Optimization System Usage Guide

This guide explains how to effectively use the Claude optimization files in your development workflow for the Flow MCP Server project.

## Quick Start

### 1. Understanding the System

The Claude optimization system consists of three key files:
- `CLAUDE.md` - Project-specific instructions and architectural guidelines
- `.clinerules` - General development rules and standards
- `.cursorrules` - IDE-specific rules (if using Cursor)
- `.claude/` - Directory for additional Claude-specific configurations

### 2. Basic Workflow

When starting a new conversation with Claude:

1. **Reference the context**: Claude automatically reads `CLAUDE.md` which contains:
   - MCP Server implementation requirements
   - Clean Architecture guidelines
   - Code quality standards
   - Technical specifications

2. **Be specific about your task**: Instead of vague requests, provide clear objectives:
   ```
   ❌ "Help me with the MCP server"
   ✅ "Implement the SSE transport handler following the Clean Architecture pattern in Infrastructure layer"
   ```

3. **Leverage the pre-loaded context**: You don't need to explain the project structure or standards - they're already loaded.

## Daily Workflow Examples

### Example 1: Implementing a New Feature

**Scenario**: Adding a new tool to the MCP server

**Effective Request**:
```
"Create a new tool called 'FileSearchTool' that implements the ITool interface. It should:
- Search for files by pattern in a given directory
- Return file paths and metadata
- Follow the existing tool patterns in the Application layer
- Include comprehensive error handling and logging"
```

**Why it works**: 
- References the `ITool` interface from the loaded context
- Specifies the layer (Application)
- Mentions requirements already defined in CLAUDE.md

### Example 2: Refactoring Existing Code

**Scenario**: Improving the transport layer

**Effective Request**:
```
"Refactor the SseTransport class to:
1. Improve thread safety using the concurrent collections mentioned in our standards
2. Add proper disposal pattern
3. Implement retry logic for failed connections
Show me the current implementation first, then propose changes"
```

**Why it works**:
- References specific classes and patterns from the loaded architecture
- Asks to review before changing (following the "read before edit" rule)
- Aligns with documented standards

### Example 3: Writing Tests

**Scenario**: Adding integration tests

**Effective Request**:
```
"Create integration tests for the SSE transport that:
- Test the full initialization sequence
- Verify proper error handling for connection failures
- Follow the test structure pattern shown in CLAUDE.md
- Use the builder pattern for test data
Target 90% coverage as specified in our standards"
```

## Common Scenarios

### 1. Starting Fresh Development

**Request Template**:
```
"I need to implement [FEATURE]. First, show me:
1. Existing related code in the [LAYER] layer
2. Interfaces I need to implement
3. Similar implementations for reference

Then create the implementation following our Clean Architecture principles."
```

### 2. Debugging Issues

**Request Template**:
```
"I'm experiencing [ISSUE] in [COMPONENT]. Please:
1. Review the current implementation
2. Check for violations of our threading/async patterns
3. Verify proper error handling
4. Suggest fixes that maintain our architectural standards"
```

### 3. Adding New Endpoints

**Request Template**:
```
"Add a new endpoint for [FUNCTIONALITY]:
1. Follow the minimal API structure in our web project
2. Include proper CORS configuration as documented
3. Add health check support
4. Implement with our standard error handling"
```

## Prompt Engineering Tips

### DO:
- ✅ Reference specific interfaces, classes, or patterns from CLAUDE.md
- ✅ Mention the architectural layer you're working in
- ✅ Ask Claude to review existing code before making changes
- ✅ Request adherence to specific standards (e.g., "using our member ordering standards")
- ✅ Be explicit about test coverage requirements

### DON'T:
- ❌ Repeat information already in CLAUDE.md
- ❌ Ask Claude to create unnecessary documentation
- ❌ Request changes without context about existing code
- ❌ Ignore the established patterns and standards

## Effective vs Ineffective Requests

### Ineffective:
```
"Make the server better"
"Add some tools"
"Fix the bug"
"Write tests"
```

### Effective:
```
"Optimize the McpServer's tool execution pipeline to handle concurrent requests better, following our async patterns and using the SemaphoreSlim approach documented in our standards"

"Add a new ResourceProvider for SQL databases that follows the IResourceProvider interface pattern, includes subscription support, and uses our standard error handling"

"Fix the race condition in SseTransport.SendMessageAsync by implementing proper locking using our documented thread safety patterns"

"Write unit tests for the ToolExecutor class that achieve 90% coverage, use the builder pattern for test data, and follow our TestCase attribute approach"
```

## Maintaining the Optimization Files

### When to Update CLAUDE.md

Update when:
- Architecture decisions change
- New patterns are adopted
- Technical requirements evolve
- New standards are established

Example update:
```markdown
### New Pattern: Circuit Breaker for External Services
All external service calls must implement circuit breaker pattern:
- Use Polly library for implementation
- Configure with: 3 failures trigger open circuit
- 30-second break duration
- Log all state transitions
```

### When to Update .clinerules

Update when:
- Development practices change
- New tools are adopted
- Workflow improvements are identified

Example update:
```
# Performance profiling required for all PRs
- Run BenchmarkDotNet on modified code paths
- Include results in PR description
- Flag any regression > 10%
```

## Advanced Usage

### 1. Multi-Step Development

For complex features, break down requests:

```
Step 1: "Review the current transport architecture and identify extension points for WebSocket support"

Step 2: "Design the WebSocketTransport interface following our existing transport patterns"

Step 3: "Implement WebSocketTransport with our standard error handling and threading patterns"

Step 4: "Add integration tests following our 90% coverage requirement"
```

### 2. Architecture Validation

Before major changes:
```
"Validate that adding [FEATURE] maintains our Clean Architecture principles:
1. Check layer dependencies
2. Verify no domain layer contamination
3. Ensure proper abstraction usage
Show me potential issues before implementation"
```

### 3. Code Review Assistance

```
"Review this implementation against our standards:
- SOLID principles adherence
- Proper async/await usage
- Thread safety concerns
- Error handling completeness
- Test coverage gaps
[paste code]"
```

## Project-Specific Examples

### Working with MCP Protocol

```
"Implement the MCP handshake sequence in the StdioTransport class:
1. Follow the initialization sequence documented in CLAUDE.md
2. Include capability negotiation
3. Add proper error handling for initialization failures
4. Use our standard logging approach"
```

### Adding Tools

```
"Create a new tool called DatabaseQueryTool:
1. Implement ITool interface from Domain layer
2. Add schema validation using FluentValidation
3. Include cancellation token support
4. Follow our ExecuteAsync pattern
5. Add to the tool registration in Application layer"
```

### Implementing Resources

```
"Add a new ConfigurationResourceProvider:
1. Implement IResourceProvider interface
2. Support subscription for configuration changes
3. Use IOptionsMonitor for hot-reload
4. Follow our async enumerable patterns
5. Include proper disposal"
```

## Troubleshooting

### Claude seems to ignore the context

**Solution**: Start your request with a reference to the specific section:
```
"Following the Clean Architecture structure defined in our project, implement..."
"Using the tool interface pattern from CLAUDE.md, create..."
```

### Claude creates unnecessary files

**Solution**: Be explicit about editing vs creating:
```
"Edit the existing McpServer.cs file to add..."
"Modify the current implementation of..."
"Update the existing tests to include..."
```

### Claude doesn't follow the standards

**Solution**: Reference specific standards:
```
"Implement this following our member ordering standards..."
"Use the error handling pattern documented in our guidelines..."
"Apply the thread safety approach specified in our standards..."
```

## Best Practices Summary

1. **Always reference existing code** before making changes
2. **Specify the architectural layer** you're working in
3. **Mention specific patterns or interfaces** from the documentation
4. **Break complex tasks** into manageable steps
5. **Request validation** against standards before implementation
6. **Be explicit** about test coverage and quality requirements
7. **Use the loaded context** instead of re-explaining the project

## Conclusion

The Claude optimization system is designed to make development faster and more consistent. By leveraging the pre-loaded context and following these guidelines, you can get high-quality, standards-compliant code with minimal back-and-forth.

Remember: The more specific your requests and the better you reference the loaded context, the more effective Claude will be in helping you develop the MCP Server project.