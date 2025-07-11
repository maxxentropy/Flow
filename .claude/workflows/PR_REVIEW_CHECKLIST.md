# Pull Request Review Checklist

## 🚀 Quick Checks (< 5 min)

### Code Quality
- [ ] **No obvious bugs** - Logic errors, null refs, off-by-one
- [ ] **Naming is clear** - Methods, variables, and classes
- [ ] **No commented code** - Remove or explain with TODO
- [ ] **Formatting consistent** - Matches project style

### Tests
- [ ] **Tests exist** - New code has test coverage
- [ ] **Tests pass** - All unit and integration tests green
- [ ] **Edge cases** - Boundary conditions tested

## 🔍 Deep Review (10-15 min)

### Architecture
- [ ] **Follows Clean Architecture** - Dependencies flow correctly
- [ ] **SOLID principles** - Single responsibility, proper abstractions
- [ ] **No layer violations** - Domain doesn't reference Infrastructure

### Performance
- [ ] **Async/await correct** - ConfigureAwait(false) used
- [ ] **No blocking calls** - .Result, .Wait() avoided
- [ ] **Resource disposal** - IDisposable/IAsyncDisposable implemented

### Security
- [ ] **Input validation** - All inputs validated
- [ ] **No hardcoded secrets** - Use configuration
- [ ] **SQL injection safe** - Parameterized queries

### Error Handling
- [ ] **Exceptions caught** - At appropriate boundaries
- [ ] **Meaningful errors** - User-friendly messages
- [ ] **Logging present** - Errors logged with context

## 📋 MCP Protocol Specific

### Protocol Compliance
- [ ] **Message format** - Valid JSON-RPC 2.0
- [ ] **Required fields** - All mandatory fields present
- [ ] **Error codes** - Standard MCP error codes used

### Transport Layer
- [ ] **Thread-safe** - Concurrent operations handled
- [ ] **Graceful shutdown** - Cleanup on disconnect
- [ ] **Backpressure** - Message queue limits

### Tool Implementation
- [ ] **Schema valid** - JSON Schema correctly defined
- [ ] **Validation works** - Parameters validated
- [ ] **Errors handled** - Returns proper error format

## 🎯 Review Strategy by File Type

### Controllers/Endpoints
```csharp
// Check for:
- Authorization attributes
- Model validation
- Proper HTTP status codes
- CORS configuration
```

### Handlers/Services
```csharp
// Check for:
- Dependency injection setup
- Cancellation token propagation
- Transaction boundaries
- Idempotency
```

### Domain Models
```csharp
// Check for:
- Immutability where appropriate
- Business rule enforcement
- Value object usage
- No external dependencies
```

### Tests
```csharp
// Check for:
- Arrange/Act/Assert pattern
- Mock verification
- Test isolation
- Meaningful assertions
```

## 🚨 Red Flags

### Immediate Rejection
- ❌ Breaks existing tests
- ❌ No tests for new code
- ❌ Security vulnerabilities
- ❌ Breaking API changes without version bump

### Needs Discussion
- ⚠️ Performance regression
- ⚠️ Large refactoring without issue
- ⚠️ New external dependencies
- ⚠️ Significant tech debt added

## 📝 Review Comments Template

### For Issues
```markdown
🐛 **Issue**: [Brief description]

**Location**: `FileName.cs:LineNumber`

**Problem**: [Detailed explanation]

**Suggestion**:
```csharp
// Proposed fix
```

**Impact**: Low/Medium/High
```

### For Suggestions
```markdown
💡 **Suggestion**: [Brief description]

Consider using [pattern/approach] here for better [benefit].

Example:
```csharp
// Suggested implementation
```

This would improve [specific metric].
```

### For Questions
```markdown
❓ **Question**: [Context]

I'm not clear on why [specific choice]. Could you explain the reasoning?

Alternative approach:
```csharp
// Alternative
```

What are your thoughts?
```

## 🎪 Review Workflow

### 1. Initial Scan (2 min)
- Read PR description
- Check linked issues
- Review changed files list
- Note areas of concern

### 2. Automated Checks (1 min)
- CI/CD status
- Code coverage delta
- Linting results
- Security scan results

### 3. Code Review (10-15 min)
- Start with tests
- Review implementation
- Check for patterns
- Verify error handling

### 4. Local Testing (5 min)
```bash
git checkout pr-branch
dotnet test
dotnet run --project McpServer.Web
# Manual testing if needed
```

### 5. Feedback (5 min)
- Write constructive comments
- Suggest improvements
- Acknowledge good patterns
- Summarize overall impression

## 🏁 Approval Criteria

### Ready to Merge
- ✅ All checks pass
- ✅ No blocking issues
- ✅ Tests adequate
- ✅ Documentation updated

### Conditional Approval
- ✅ Minor issues only
- ✅ Can be fixed in follow-up
- ✅ Document remaining work

### Request Changes
- ❌ Major issues found
- ❌ Missing critical tests
- ❌ Architecture violations
- ❌ Security concerns

## 💭 Post-Review

### Knowledge Sharing
- Share interesting patterns found
- Document new conventions
- Update team guidelines
- Create tech debt items

### Metrics to Track
- Review turnaround time
- Defect escape rate
- Review effectiveness
- Team velocity impact