# Testing MCP Server with Postman

This guide explains how to test the MCP Server using the provided Postman collection.

## Prerequisites

1. [Postman](https://www.postman.com/downloads/) installed
2. MCP Server running (either Console or Web version)

## Setup

### 1. Import the Collection

1. Open Postman
2. Click "Import" in the top left
3. Select the `McpServer.postman_collection.json` file
4. The "MCP Server Collection" will appear in your Collections

### 2. Import the Environment

1. Click the gear icon in the top right (Manage Environments)
2. Click "Import"
3. Select the `McpServer.postman_environment.json` file
4. Select "MCP Server Local" from the environment dropdown

### 3. Start the MCP Server

#### Option A: Web Server (Recommended for Postman testing)
```bash
cd src/McpServer.Web
dotnet run
```
The server will start on `http://localhost:5000`

#### Option B: Console Server
```bash
cd src/McpServer.Console
dotnet run -- --transport stdio
```
Note: The console version uses stdio transport and is not suitable for HTTP-based Postman testing.

## Testing Flow

### 1. Initialize Connection

Run the requests in this order:

1. **SSE Connection → Initialize Connection**
   - This establishes the MCP session
   - Should return server capabilities and information

2. **SSE Connection → Send Initialized Notification**
   - Completes the handshake
   - No response expected (notification)

### 2. Test Tools

After initialization, you can test the tools:

1. **Tools → List Tools**
   - Returns available tools: echo, calculator, datetime

2. **Tools → Call Echo Tool**
   - Tests the echo tool functionality

3. **Tools → Call Calculator Tool - Add**
   - Tests calculator addition

4. **Tools → Call DateTime Tool**
   - Gets current date/time

### 3. Test Resources

1. **Resources → List Resources**
   - Lists available file resources

2. **Resources → Read Resource**
   - Reads a specific file (update the URI as needed)

### 4. Test Error Handling

The "Error Cases" folder contains requests to test error scenarios:

- Invalid JSON-RPC version
- Method not found
- Invalid parameters
- Parse errors

## Important Notes

### Server-Sent Events (SSE)

The MCP Server uses SSE for real-time communication. In Postman:

1. Regular POST requests to `/sse` will show the immediate response
2. For real SSE streaming, you may need to use:
   - Browser DevTools
   - curl with `--no-buffer`
   - Specialized SSE clients

### Example with curl:
```bash
# Start SSE connection
curl -N -H "Accept: text/event-stream" http://localhost:5000/sse

# In another terminal, send requests
curl -X POST http://localhost:5000/sse \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "0.1.0",
      "capabilities": {},
      "clientInfo": {
        "name": "curl client",
        "version": "1.0.0"
      }
    }
  }'
```

## Customization

### Environment Variables

You can modify the environment variables:

- `baseUrl`: Change if running on a different port
- `clientName`: Your client application name
- `clientVersion`: Your client version
- `protocolVersion`: MCP protocol version

### Adding Custom Requests

1. Right-click on a folder in the collection
2. Select "Add Request"
3. Configure your custom JSON-RPC request

## Troubleshooting

### Connection Refused
- Ensure the MCP Web server is running
- Check the port in the environment matches your server

### Invalid Response
- Verify you've initialized the connection first
- Check the JSON-RPC format is correct

### SSE Not Working
- Postman has limited SSE support
- Use browser DevTools or curl for full SSE testing

## Example Responses

### Successful Initialize Response
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "0.1.0",
    "serverInfo": {
      "name": "MCP Server",
      "version": "1.0.0"
    },
    "capabilities": {
      "tools": {},
      "resources": {
        "subscribe": true,
        "listChanged": true
      },
      "prompts": {
        "listChanged": true
      },
      "logging": {}
    }
  }
}
```

### Tool Call Response
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Echo: Hello from Postman!"
      }
    ]
  }
}
```

### Error Response
```json
{
  "jsonrpc": "2.0",
  "id": 100,
  "error": {
    "code": -32600,
    "message": "Invalid JSON-RPC version"
  }
}
```