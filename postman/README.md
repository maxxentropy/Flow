# MCP Server Postman Collection

This directory contains a comprehensive Postman collection for testing the Model Context Protocol (MCP) Server implementation.

## Files

- **`MCP-Server-Collection.json`** - Main Postman collection with all API endpoints
- **`MCP-Server-Environment.json`** - Environment variables for development testing
- **`README.md`** - This documentation file

## Quick Start

### 1. Import into Postman

1. Open Postman
2. Click **Import** button
3. Upload both files:
   - `MCP-Server-Collection.json`
   - `MCP-Server-Environment.json`
4. Select the "MCP Server Development" environment

### 2. Start the MCP Server

Before testing, ensure the MCP Server is running:

```bash
# Run the web server
dotnet run --project src/McpServer.Web

# Or run the console version for stdio testing
dotnet run --project src/McpServer.Console
```

The server will be available at `http://localhost:5080` by default.

### 3. Test the Server

#### Basic Health Check
1. Run **"Get Server Info"** to verify the server is running
2. Run **"Health Check"** to confirm all services are healthy

#### Complete MCP Protocol Flow
Execute the requests in the **"MCP Protocol Flow (SSE)"** folder in order:

1. **Initialize Connection** - Establishes MCP protocol handshake
2. **Send Initialized Notification** - Completes the initialization
3. **List Available Tools** - Shows all registered tools
4. **Execute Echo Tool** - Tests basic tool execution
5. **Execute Calculator Tool** - Tests computational tools
6. **Execute DateTime Tool** - Tests utility tools

## Collection Structure

### 📁 Server Info & Health
- Basic server information and health monitoring
- No authentication required

### 📁 MCP Protocol Flow (SSE)
- Complete protocol implementation via Server-Sent Events
- **Must be executed in sequence** for proper session state
- Includes automated tests for response validation

### 📁 Resources
- File system and resource access endpoints
- List and read various resource types

### 📁 Prompts
- Prompt template management
- List available prompts and execute them with parameters

### 📁 Utility Methods
- Ping for connectivity testing
- Request cancellation for long-running operations

### 📁 Error Scenarios
- Comprehensive error handling tests
- Invalid JSON, unknown methods, unauthorized access
- Automated test assertions for error codes

### 📁 Authentication & Security
- OAuth flow testing (Google, Microsoft, GitHub)
- Session management
- User profile access

### 📁 Load Testing
- Performance testing requests
- Use Postman Runner for concurrent execution
- Random data generation for varied testing

## Environment Variables

The environment includes these key variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `baseUrl` | `http://localhost:5080` | Server base URL |
| `wsUrl` | `ws://localhost:5080/ws` | WebSocket endpoint |
| `protocolVersion` | `0.1.0` | MCP protocol version |
| `accessToken` | _(empty)_ | OAuth access token |
| `sessionId` | _(empty)_ | Current session ID |

## Testing Features

### Automated Tests
Each request includes automated tests that verify:
- HTTP status codes
- JSON-RPC response format
- Expected response structure
- Business logic validation

### Dynamic Data
- Uses Postman's `{{$randomInt}}` for load testing
- Correlation IDs for request tracing
- Environment-specific configurations

### Error Validation
Comprehensive error scenario testing with assertions for:
- JSON-RPC error codes (-32700, -32601, -32602, -32603)
- Custom application errors
- Authentication and authorization failures

## Advanced Usage

### Load Testing
1. Select requests in the "Load Testing" folder
2. Use Postman Runner with:
   - Multiple iterations (10-100)
   - Concurrent requests (5-20)
   - Delays between requests (100-1000ms)

### WebSocket Testing
While this collection focuses on HTTP/SSE endpoints, WebSocket testing can be done using:
- Postman's WebSocket client
- External tools like `wscat`
- Browser developer tools

### Custom Environments
Create additional environments for:
- **Production**: `https://your-production-server.com`
- **Staging**: `https://staging.your-domain.com`
- **Docker**: `http://localhost:8080`

## Troubleshooting

### Common Issues

1. **Connection Refused**
   - Verify server is running on correct port
   - Check firewall settings
   - Ensure environment `baseUrl` is correct

2. **Initialization Required Errors**
   - Run initialization sequence in order
   - Don't skip the "Initialize Connection" step
   - Each new Postman session needs re-initialization

3. **Tool Not Found Errors**
   - Verify tools are properly registered on server startup
   - Check server logs for tool registration messages
   - Ensure required dependencies are available

4. **Authentication Failures**
   - Set up OAuth providers in server configuration
   - Obtain valid access tokens through OAuth flow
   - Update `accessToken` environment variable

### Debug Tips

1. **Enable Postman Console** to see detailed request/response logs
2. **Check Response Headers** for correlation IDs and timing information
3. **Review Server Logs** for detailed error information
4. **Use Health Check** endpoint to verify service status

## MCP Protocol Compliance

This collection tests full compliance with the Model Context Protocol specification:

- ✅ **Protocol Handshake** - Initialize and initialized notifications
- ✅ **Tool Management** - List, call, and error handling
- ✅ **Resource Access** - List and read operations  
- ✅ **Prompt Templates** - List and get with parameters
- ✅ **Error Handling** - All standard JSON-RPC error codes
- ✅ **Capabilities Negotiation** - Client and server capabilities
- ✅ **Progress Tracking** - Long-running operation support
- ✅ **Cancellation** - Request cancellation mechanism

## Contributing

To add new test cases:

1. Add requests to appropriate folders
2. Include automated test scripts
3. Update environment variables as needed
4. Document any new features in this README
5. Test with different server configurations

For issues or improvements, please refer to the main project repository.