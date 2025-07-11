# Security Implementation Index

## Overview
The MCP Server implements a comprehensive security framework supporting multiple authentication methods, session management, rate limiting, and authorization. Security is enforced at multiple layers with defense-in-depth principles.

## Authentication Architecture

### Authentication Middleware (`src/McpServer.Application/Middleware/AuthenticationMiddleware.cs`)
- **Purpose**: Intercepts all requests for authentication
- **Flow**:
  1. Extract credentials from request
  2. Validate against configured provider
  3. Create/update session
  4. Attach identity to context
- **Bypass**: Only `ping` and `initialize` methods

### Authentication Service (`src/McpServer.Application/Services/AuthenticationService.cs`)
- **Central authentication orchestrator**
- **Provider selection based on configuration**
- **Session creation and validation**
- **Token refresh handling**

## Authentication Providers

### API Key Authentication (`src/McpServer.Infrastructure/Security/ApiKeyAuthenticationProvider.cs`)
- **Simple shared secret authentication**
- **Configuration**:
  ```json
  {
    "Authentication": {
      "Provider": "ApiKey",
      "ApiKey": {
        "HeaderName": "X-API-Key",
        "Keys": ["key1", "key2"]
      }
    }
  }
  ```
- **Use Case**: Internal services, development

### JWT Authentication (`src/McpServer.Infrastructure/Security/JwtAuthenticationProvider.cs`)
- **Token-based authentication**
- **Features**:
  - RS256/HS256 algorithms
  - Token validation and expiry
  - Claims extraction
  - Refresh token support
- **Configuration**:
  ```json
  {
    "Authentication": {
      "Provider": "Jwt",
      "Jwt": {
        "Issuer": "https://auth.example.com",
        "Audience": "mcp-server",
        "SigningKey": "base64-encoded-key"
      }
    }
  }
  ```

### OAuth Authentication (`src/McpServer.Infrastructure/Security/OAuthAuthenticationProvider.cs`)
- **Third-party authentication**
- **Supported Providers**:
  - GitHub (`GitHubOAuthProvider.cs`)
  - Google (`GoogleOAuthProvider.cs`)
  - Microsoft (`MicrosoftOAuthProvider.cs`)
- **Flow**:
  1. Redirect to OAuth provider
  2. Handle callback with code
  3. Exchange for access token
  4. Fetch user profile
  5. Create local session

### Session Authentication (`src/McpServer.Infrastructure/Security/SessionAuthenticationProvider.cs`)
- **Cookie/header-based sessions**
- **Features**:
  - Session token generation
  - Configurable expiration
  - Sliding expiration support
  - Session storage abstraction

## Session Management

### Session Service (`src/McpServer.Application/Services/SessionService.cs`)
- **Session lifecycle management**
- **Key Operations**:
  ```csharp
  Task<Session> CreateSessionAsync(User user)
  Task<Session?> GetSessionAsync(string token)
  Task RefreshSessionAsync(string token)
  Task RevokeSessionAsync(string token)
  ```

### Session Storage (`src/McpServer.Infrastructure/Security/InMemorySessionRepository.cs`)
- **Default in-memory implementation**
- **Thread-safe with concurrent collections**
- **Automatic cleanup of expired sessions**
- **Extensible for Redis/database storage**

### Session Cleanup (`src/McpServer.Infrastructure/Services/SessionCleanupService.cs`)
- **Background service for session maintenance**
- **Configurable cleanup interval**
- **Removes expired sessions**
- **Memory optimization**

## User Management

### User Profile Service (`src/McpServer.Application/Services/UserProfileService.cs`)
- **User profile management**
- **Profile enrichment from OAuth**
- **Caching for performance**
- **Audit trail support**

### User Repository (`src/McpServer.Infrastructure/Security/InMemoryUserRepository.cs`)
- **User storage abstraction**
- **Default in-memory implementation**
- **CRUD operations**
- **Extensible for database storage**

## Rate Limiting

### Rate Limiting Middleware (`src/McpServer.Application/Middleware/RateLimitingMiddleware.cs`)
- **Request throttling per connection**
- **Configurable limits by method**
- **Sliding window algorithm**
- **HTTP 429 responses**

### Rate Limiter Service (`src/McpServer.Application/Services/RateLimiter.cs`)
- **Core rate limiting logic**
- **Features**:
  - Per-user/IP limits
  - Method-specific limits
  - Burst allowances
  - Distributed rate limiting support
- **Configuration**:
  ```json
  {
    "RateLimiting": {
      "Default": {
        "Limit": 100,
        "Window": "00:01:00"
      },
      "MethodLimits": {
        "tools/call": { "Limit": 10, "Window": "00:01:00" },
        "resources/read": { "Limit": 50, "Window": "00:01:00" }
      }
    }
  }
  ```

## Authorization

### Resource-Based Authorization
- **Check permissions per resource**
- **Implemented in handlers**
- **Example pattern**:
  ```csharp
  if (!await _authService.CanAccessResourceAsync(user, resourceUri))
      throw new UnauthorizedException();
  ```

### Tool Execution Authorization
- **Permission checks before tool execution**
- **Configurable per tool**
- **Audit logging of attempts**

## Security Headers and CORS

### Web Security Headers
- **Implemented in ASP.NET Core pipeline**
- **Headers**:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `X-XSS-Protection: 1; mode=block`
  - `Strict-Transport-Security` (if HTTPS)

### CORS Configuration
- **Configurable per transport**:
  ```json
  {
    "Transport": {
      "Sse": {
        "AllowedOrigins": ["https://app.example.com"],
        "AllowCredentials": true
      }
    }
  }
  ```

## Input Validation

### Validation Middleware (`src/McpServer.Application/Middleware/ValidationMiddleware.cs`)
- **Validates all incoming requests**
- **Uses FluentValidation**
- **Custom validators per message type**
- **Detailed error responses**

### Security-Focused Validators
- **Path traversal prevention**
- **SQL injection protection**
- **Command injection prevention**
- **Size limits enforcement**

## Cryptographic Security

### Token Generation
- **Cryptographically secure random tokens**
- **Example**:
  ```csharp
  var bytes = new byte[32];
  using var rng = RandomNumberGenerator.Create();
  rng.GetBytes(bytes);
  return Convert.ToBase64String(bytes);
  ```

### Password Hashing (if implemented)
- **BCrypt or Argon2**
- **Configurable work factors**
- **Salt generation per password**

## Audit and Logging

### Security Event Logging
- **Authentication attempts**
- **Authorization failures**
- **Rate limit violations**
- **Session lifecycle events**
- **Structured logging format**:
  ```csharp
  _logger.LogWarning("Authentication failed for user {UserId} from {IpAddress}", 
      userId, ipAddress);
  ```

### Audit Trail
- **Sensitive operations logged**
- **User actions tracked**
- **Timestamp and context included**
- **Compliant with regulations**

## Security Configuration

### Main Security Settings (`appsettings.Authentication.json`)
```json
{
  "Authentication": {
    "Provider": "Jwt",
    "RequireHttps": true,
    "SessionTimeout": "00:30:00",
    "MaxConcurrentSessions": 5,
    "EnableAuditLogging": true
  }
}
```

### OAuth Configuration (`appsettings.OAuth.json`)
```json
{
  "OAuth": {
    "GitHub": {
      "ClientId": "your-client-id",
      "ClientSecret": "your-secret",
      "Scopes": ["read:user", "user:email"]
    }
  }
}
```

## Common Security Patterns

### Secure Error Handling
```csharp
try
{
    // Operation
}
catch (SecurityException ex)
{
    _logger.LogWarning(ex, "Security violation");
    throw new McpException(McpErrorCodes.Unauthorized, "Access denied");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error");
    throw new McpException(McpErrorCodes.InternalError, "An error occurred");
}
```

### Authentication Check Pattern
```csharp
public async Task<TResponse> HandleAsync(TRequest request, CancellationToken ct)
{
    var context = _connectionManager.GetContext();
    if (context?.User == null)
        throw new UnauthorizedException();
    
    // Proceed with authenticated user
}
```

## Security Testing

### Test Scenarios
1. **Authentication bypass attempts**
2. **Token expiration handling**
3. **Rate limit enforcement**
4. **CORS policy validation**
5. **Input validation edge cases**

### Security Test Example
```csharp
[Test]
public async Task AuthenticationMiddleware_InvalidToken_ReturnsUnauthorized()
{
    // Arrange
    var middleware = new AuthenticationMiddleware(next, authService);
    var context = CreateContextWithToken("invalid-token");
    
    // Act & Assert
    await Assert.ThrowsAsync<UnauthorizedException>(
        () => middleware.InvokeAsync(context));
}
```

## Security Best Practices

### Development
1. **Never log sensitive data**
2. **Use secure defaults**
3. **Validate all inputs**
4. **Implement least privilege**
5. **Regular security updates**

### Deployment
1. **Use HTTPS in production**
2. **Secure configuration storage**
3. **Network isolation**
4. **Regular security audits**
5. **Monitor security events**

## Threat Model

### Common Threats and Mitigations
1. **Injection Attacks**: Input validation, parameterized queries
2. **Authentication Bypass**: Strong auth providers, session validation
3. **Session Hijacking**: Secure tokens, HTTPS, session timeouts
4. **DoS Attacks**: Rate limiting, resource limits
5. **Data Exposure**: Encryption, secure logging

## Security Troubleshooting

### Common Issues
1. **401 Unauthorized**: Check auth configuration and credentials
2. **403 Forbidden**: Verify user permissions
3. **429 Too Many Requests**: Review rate limit settings
4. **CORS Errors**: Update allowed origins configuration

### Debug Security Issues
- Enable debug logging for auth middleware
- Check security event logs
- Verify token contents with JWT debugger
- Test with minimal security for isolation