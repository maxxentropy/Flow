// Example MCP client for testing the server
// This demonstrates how to connect to the MCP server using stdio or SSE

const readline = require('readline');
const { spawn } = require('child_process');

// Example 1: Connecting via stdio transport
function connectStdio() {
    const mcpServer = spawn('dotnet', ['run', '--project', '../src/McpServer.Console/McpServer.Console.csproj']);
    
    // Handle server output
    mcpServer.stdout.on('data', (data) => {
        try {
            const response = JSON.parse(data.toString());
            console.log('Received:', response);
        } catch (e) {
            console.log('Server output:', data.toString());
        }
    });
    
    mcpServer.stderr.on('data', (data) => {
        console.error('Server error:', data.toString());
    });
    
    // Send initialize request
    const initRequest = {
        jsonrpc: "2.0",
        id: 1,
        method: "initialize",
        params: {
            protocolVersion: "0.1.0",
            capabilities: {},
            clientInfo: {
                name: "Example MCP Client",
                version: "1.0.0"
            }
        }
    };
    
    mcpServer.stdin.write(JSON.stringify(initRequest) + '\n');
    
    // Send initialized notification after receiving response
    setTimeout(() => {
        const initializedNotification = {
            jsonrpc: "2.0",
            method: "initialized"
        };
        mcpServer.stdin.write(JSON.stringify(initializedNotification) + '\n');
        
        // List tools
        const listToolsRequest = {
            jsonrpc: "2.0",
            id: 2,
            method: "tools/list"
        };
        mcpServer.stdin.write(JSON.stringify(listToolsRequest) + '\n');
        
        // Call echo tool
        const callToolRequest = {
            jsonrpc: "2.0",
            id: 3,
            method: "tools/call",
            params: {
                name: "echo",
                arguments: {
                    message: "Hello from MCP client!"
                }
            }
        };
        mcpServer.stdin.write(JSON.stringify(callToolRequest) + '\n');
    }, 1000);
    
    return mcpServer;
}

// Example 2: Connecting via SSE transport
async function connectSSE() {
    const EventSource = require('eventsource');
    
    // Initialize connection
    const initResponse = await fetch('http://localhost:8080/sse', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            jsonrpc: "2.0",
            id: 1,
            method: "initialize",
            params: {
                protocolVersion: "0.1.0",
                capabilities: {},
                clientInfo: {
                    name: "Example SSE Client",
                    version: "1.0.0"
                }
            }
        })
    });
    
    if (!initResponse.ok) {
        throw new Error(`HTTP error! status: ${initResponse.status}`);
    }
    
    // Set up SSE connection
    const eventSource = new EventSource('http://localhost:8080/sse');
    
    eventSource.onmessage = (event) => {
        try {
            const data = JSON.parse(event.data);
            console.log('SSE Received:', data);
        } catch (e) {
            console.log('SSE Raw data:', event.data);
        }
    };
    
    eventSource.onerror = (error) => {
        console.error('SSE Error:', error);
    };
    
    // Send requests via POST
    async function sendRequest(request) {
        const response = await fetch('http://localhost:8080/sse', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(request)
        });
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
    }
    
    // Send initialized notification
    await sendRequest({
        jsonrpc: "2.0",
        method: "initialized"
    });
    
    // List tools
    await sendRequest({
        jsonrpc: "2.0",
        id: 2,
        method: "tools/list"
    });
    
    // Call calculator tool
    await sendRequest({
        jsonrpc: "2.0",
        id: 3,
        method: "tools/call",
        params: {
            name: "calculator",
            arguments: {
                operation: "multiply",
                a: 7,
                b: 6
            }
        }
    });
    
    return eventSource;
}

// Interactive CLI for testing
function startInteractiveCLI(mcpServer) {
    const rl = readline.createInterface({
        input: process.stdin,
        output: process.stdout
    });
    
    let requestId = 10;
    
    console.log('\nMCP Client Interactive Mode');
    console.log('Commands:');
    console.log('  tools - List available tools');
    console.log('  echo <message> - Call echo tool');
    console.log('  calc <op> <a> <b> - Call calculator (op: add/subtract/multiply/divide)');
    console.log('  time [format] [timezone] - Get current time');
    console.log('  exit - Quit\n');
    
    rl.on('line', (input) => {
        const [cmd, ...args] = input.trim().split(' ');
        
        switch (cmd) {
            case 'tools':
                mcpServer.stdin.write(JSON.stringify({
                    jsonrpc: "2.0",
                    id: requestId++,
                    method: "tools/list"
                }) + '\n');
                break;
                
            case 'echo':
                mcpServer.stdin.write(JSON.stringify({
                    jsonrpc: "2.0",
                    id: requestId++,
                    method: "tools/call",
                    params: {
                        name: "echo",
                        arguments: {
                            message: args.join(' ')
                        }
                    }
                }) + '\n');
                break;
                
            case 'calc':
                const [op, a, b] = args;
                mcpServer.stdin.write(JSON.stringify({
                    jsonrpc: "2.0",
                    id: requestId++,
                    method: "tools/call",
                    params: {
                        name: "calculator",
                        arguments: {
                            operation: op,
                            a: parseFloat(a),
                            b: parseFloat(b)
                        }
                    }
                }) + '\n');
                break;
                
            case 'time':
                const timeArgs = {};
                if (args[0]) timeArgs.format = args[0];
                if (args[1]) timeArgs.timezone = args[1];
                
                mcpServer.stdin.write(JSON.stringify({
                    jsonrpc: "2.0",
                    id: requestId++,
                    method: "tools/call",
                    params: {
                        name: "datetime",
                        arguments: timeArgs
                    }
                }) + '\n');
                break;
                
            case 'exit':
                mcpServer.kill();
                process.exit(0);
                break;
                
            default:
                console.log('Unknown command');
        }
    });
}

// Main
if (require.main === module) {
    const transport = process.argv[2] || 'stdio';
    
    if (transport === 'stdio') {
        console.log('Connecting via stdio transport...');
        const server = connectStdio();
        setTimeout(() => startInteractiveCLI(server), 2000);
    } else if (transport === 'sse') {
        console.log('Connecting via SSE transport...');
        connectSSE().catch(console.error);
    } else {
        console.log('Usage: node mcp-client-example.js [stdio|sse]');
    }
}