# MCP Protocol Domain Glossary

## Core Protocol Terms

### **JSON-RPC 2.0**
The underlying protocol for MCP communication. All messages follow JSON-RPC 2.0 specification with `jsonrpc: "2.0"` field.

### **Transport**
The communication channel between MCP client and server. Current supported transports:
- **stdio**: Standard input/output streams (console applications)
- **SSE**: Server-Sent Events over HTTP (web applications)
- **WebSocket**: Bidirectional web communication (planned)

### **Capability**
Features that a server advertises during initialization. Examples:
- `tools`: Server provides tool execution
- `resources`: Server provides resource access
- `prompts`: Server provides prompt templates

### **Initialization Handshake**
The required startup sequence where client and server exchange capabilities and protocol version.

## Message Types

### **Request**
A message requiring a response, containing:
- `id`: Unique identifier (number or string)
- `method`: The method to invoke
- `params`: Optional parameters

### **Response**
Reply to a request, containing:
- `id`: Matching the request ID
- `result`: Success data, OR
- `error`: Error object with code and message

### **Notification**
A message not requiring a response (no `id` field). Used for:
- Progress updates
- Log messages
- State changes

### **Batch**
Array of multiple requests/notifications sent together. Not currently used in MCP but supported by JSON-RPC.

## Tool System Terms

### **Tool**
A callable function exposed by the server with:
- Unique name
- Description
- Input schema (JSON Schema)
- Execution logic

### **Tool Schema**
JSON Schema defining the expected parameters for a tool. Used for:
- Client-side validation
- API documentation
- Type safety

### **Tool Result**
Response from tool execution containing:
- Success indicator
- Content array (text, images, etc.)
- Error details if failed

### **Tool Registry**
Server component managing tool discovery and execution. Handles:
- Registration
- Lookup by name
- Schema generation

## Resource System Terms

### **Resource**
External data accessible through the server, identified by URI. Examples:
- `file:///path/to/file.txt`
- `https://api.example.com/data`
- `git://repo/branch/file`

### **Resource Provider**
Component handling specific URI schemes. Responsibilities:
- List resources
- Read content
- Watch for changes

### **Resource Subscription**
Long-lived connection monitoring resource changes. Events:
- Created
- Updated
- Deleted

### **Resource Content**
The actual data of a resource including:
- MIME type
- Encoding
- Binary or text data
- Metadata

## Protocol Operations

### **initialize**
First method called to establish connection. Parameters:
- `protocolVersion`: MCP version (e.g., "2024-11-05")
- `capabilities`: Client capabilities
- `clientInfo`: Client identification

### **initialized**
Notification sent after successful initialization. No parameters.

### **tools/list**
Request to enumerate available tools. Returns array of tool definitions.

### **tools/call**
Execute a specific tool. Parameters:
- `name`: Tool identifier
- `arguments`: Tool-specific parameters

### **resources/list**
Enumerate available resources. Optional parameters:
- `pathPrefix`: Filter results
- `recursive`: Include subdirectories

### **resources/read**
Retrieve resource content. Parameters:
- `uri`: Resource identifier

### **resources/subscribe**
Monitor resource changes. Parameters:
- `uri`: Resource to watch

### **resources/unsubscribe**
Stop monitoring resource. Parameters:
- `subscriptionId`: From subscribe response

## Error Codes

### Standard JSON-RPC Errors
- `-32700`: Parse error (invalid JSON)
- `-32600`: Invalid request
- `-32601`: Method not found
- `-32602`: Invalid params
- `-32603`: Internal error

### MCP-Specific Errors
- `-32000`: Server error (generic)
- `-32001`: Tool not found
- `-32002`: Tool execution failed
- `-32003`: Resource not found
- `-32004`: Resource access denied
- `-32005`: Invalid protocol version

## Architecture Terms

### **Clean Architecture**
Architectural pattern with layers:
- **Domain**: Core business logic, no dependencies
- **Application**: Use cases and orchestration
- **Infrastructure**: External concerns (I/O, frameworks)
- **Presentation**: User interface (API endpoints)

### **Handler**
Component processing specific message types. Follows:
- Single Responsibility Principle
- Async execution pattern
- Cancellation support

### **Middleware**
Cross-cutting concern processors:
- Logging
- Error handling
- Performance monitoring
- Request correlation

### **Dependency Injection**
Pattern for managing object dependencies. Key concepts:
- Service lifetime (Singleton, Scoped, Transient)
- Interface segregation
- Inversion of control

## Performance Terms

### **Backpressure**
Mechanism preventing overwhelming slow consumers. Implemented via:
- Channel capacity limits
- Async enumerable
- Rate limiting

### **Circuit Breaker**
Pattern preventing cascading failures:
- Closed: Normal operation
- Open: Failing fast
- Half-open: Testing recovery

### **Connection Pooling**
Reusing connections for efficiency:
- HTTP connections for resources
- Database connections
- WebSocket management

## Security Terms

### **CORS (Cross-Origin Resource Sharing)**
Browser security for SSE transport:
- Allowed origins configuration
- Preflight requests
- Credential handling

### **Input Validation**
Protecting against malicious input:
- Schema validation
- Size limits
- Injection prevention

### **Rate Limiting**
Preventing abuse:
- Per-client limits
- Per-method limits
- Sliding window algorithm

## Operational Terms

### **Health Check**
Endpoint indicating server status:
- Liveness: Is it running?
- Readiness: Can it serve requests?

### **Metrics**
Quantitative measurements:
- Request rate
- Error rate
- Response time
- Resource usage

### **Correlation ID**
Unique identifier tracking requests across components. Used for:
- Distributed tracing
- Log aggregation
- Debugging

### **Graceful Shutdown**
Clean termination process:
- Stop accepting new requests
- Complete in-flight requests
- Release resources
- Signal completion