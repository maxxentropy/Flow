# Transport Layer Index

## Overview
The transport layer handles communication between MCP clients and the server. It supports multiple simultaneous transports with a pluggable architecture.

## Transport Implementations

### StdioTransport (`src/McpServer.Infrastructure/Transport/StdioTransport.cs`)
- **Purpose**: Console-based communication via stdin/stdout
- **Use Case**: CLI tools, development, testing
- **Key Features**:
  - Line-delimited JSON-RPC messages
  - Synchronous read loop with async processing
  - Graceful shutdown on EOF or cancellation
  - Configurable buffer sizes

### SseTransport (`src/McpServer.Infrastructure/Transport/SseTransport.cs`)
- **Purpose**: Server-Sent Events over HTTP
- **Use Case**: Web applications, browser-based clients
- **Key Features**:
  - HTTP POST endpoint for SSE streams
  - CORS support for cross-origin requests
  - Keep-alive heartbeats
  - Automatic reconnection support
  - Multiple concurrent connections

### WebSocketTransport (`src/McpServer.Infrastructure/Transport/WebSocketTransport.cs`)
- **Purpose**: Full-duplex WebSocket communication
- **Use Case**: Real-time applications, bidirectional communication
- **Key Features**:
  - Binary and text message support
  - Ping/pong heartbeats
  - Connection state management
  - Automatic reconnection handling
  - Message fragmentation support

## Transport Management

### TransportManager (`src/McpServer.Infrastructure/Transport/TransportManager.cs`)
**Responsibilities**:
- Lifecycle management for all transports
- Simultaneous multi-transport operation
- Health monitoring and reporting
- Graceful shutdown coordination

**Key Methods**:
```csharp
Task StartAsync(TransportType type, CancellationToken ct)
Task StartAllAsync(CancellationToken ct)
Task StopAsync(CancellationToken ct)
bool IsRunning(TransportType type)
```

## Configuration

### Transport Configuration Structure
```json
{
  "Transport": {
    "Stdio": {
      "Enabled": true,
      "BufferSize": 4096,
      "Timeout": "00:05:00"
    },
    "Sse": {
      "Enabled": true,
      "Port": 8080,
      "Path": "/sse",
      "AllowedOrigins": ["*"],
      "RequireHttps": false,
      "ConnectionTimeout": "00:30:00",
      "HeartbeatInterval": "00:00:30"
    },
    "WebSocket": {
      "Enabled": true,
      "Port": 8080,
      "Path": "/ws",
      "MaxMessageSize": 65536,
      "KeepAliveInterval": "00:00:30"
    }
  }
}
```

## Message Flow

### Incoming Messages
1. Transport receives raw data
2. Parse JSON-RPC message
3. Validate message structure
4. Route to MessageRouter
5. Execute handler
6. Send response via transport

### Outgoing Messages
1. Handler/Service creates response
2. Serialize to JSON-RPC format
3. Transport-specific encoding
4. Send to client
5. Handle send errors/retries

## Connection Lifecycle

### Connection Establishment
1. **Stdio**: Immediate on process start
2. **SSE**: HTTP POST to endpoint
3. **WebSocket**: HTTP upgrade handshake

### Connection Maintenance
- Heartbeat/keepalive mechanisms
- Connection state tracking
- Error recovery strategies
- Resource cleanup on disconnect

### Connection Termination
- Graceful shutdown sequences
- Resource disposal
- State cleanup
- Event notifications

## Error Handling

### Common Transport Errors
1. **Connection Lost**: Automatic reconnection or cleanup
2. **Invalid Message**: Error response with details
3. **Timeout**: Configurable timeout handling
4. **Buffer Overflow**: Message size limits enforced

### Error Recovery Patterns
```csharp
// Retry with exponential backoff
await RetryPolicy.ExecuteAsync(async () => {
    await transport.SendAsync(message);
});

// Circuit breaker for transport failures
await circuitBreaker.ExecuteAsync(async () => {
    await transport.ConnectAsync();
});
```

## Performance Optimizations

### Message Batching
- Combine multiple messages when possible
- Reduce network overhead
- Maintain message ordering

### Buffer Management
- Configurable buffer sizes
- Memory pool usage
- Efficient serialization

### Connection Pooling
- Reuse HTTP connections for SSE
- WebSocket connection multiplexing
- Resource sharing strategies

## Security Considerations

### Transport Security
- TLS/SSL for SSE and WebSocket
- CORS policy enforcement
- Authentication token handling
- Message integrity validation

### Attack Prevention
- Message size limits
- Rate limiting per connection
- Input validation at transport layer
- Connection count limits

## Extension Points

### Custom Transport Implementation
1. Implement `ITransport` interface
2. Register in DI container
3. Add to `TransportType` enum
4. Update `TransportManager`
5. Add configuration section

### Transport Middleware
- Pre/post message processing
- Logging and metrics collection
- Security enforcement
- Message transformation

## Debugging Tips

### Common Issues
1. **Message Not Received**: Check transport enabled in config
2. **Connection Drops**: Review timeout settings
3. **CORS Errors**: Verify allowed origins configuration
4. **Performance Issues**: Monitor buffer sizes and connection limits

### Diagnostic Tools
- Transport-specific health endpoints
- Connection monitoring endpoints
- Structured logging with correlation IDs
- Performance counters per transport

## Testing Strategies

### Unit Testing
- Mock transport dependencies
- Test message parsing/serialization
- Verify error handling
- Connection state transitions

### Integration Testing
- End-to-end message flow
- Multi-transport scenarios
- Load testing per transport
- Failure scenario testing

## Related Components
- `MessageRouter` - Routes messages to handlers
- `ConnectionManager` - Manages connection state
- `AuthenticationMiddleware` - Transport-agnostic auth
- `RateLimitingMiddleware` - Request throttling