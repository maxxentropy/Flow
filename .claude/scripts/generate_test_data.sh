#!/bin/bash
# Test data generation for MCP Server

set -e

echo "🧪 Generating test data for MCP Server..."

# Create test data directory
TEST_DATA_DIR="tests/TestData"
mkdir -p "$TEST_DATA_DIR"

# Generate JSON-RPC test messages
cat > "$TEST_DATA_DIR/jsonrpc_messages.json" << 'EOF'
{
  "valid_requests": [
    {
      "jsonrpc": "2.0",
      "id": 1,
      "method": "initialize",
      "params": {
        "protocolVersion": "2024-11-05",
        "capabilities": {
          "tools": {},
          "resources": {"subscribe": true}
        },
        "clientInfo": {
          "name": "TestClient",
          "version": "1.0.0"
        }
      }
    },
    {
      "jsonrpc": "2.0",
      "id": 2,
      "method": "tools/list",
      "params": {}
    },
    {
      "jsonrpc": "2.0",
      "id": 3,
      "method": "tools/call",
      "params": {
        "name": "test_tool",
        "arguments": {"param1": "value1"}
      }
    }
  ],
  "invalid_requests": [
    {
      "jsonrpc": "2.0",
      "method": "unknown_method"
    },
    {
      "jsonrpc": "1.0",
      "id": 1,
      "method": "initialize"
    },
    {
      "id": 1,
      "method": "tools/list"
    }
  ],
  "edge_cases": [
    {
      "jsonrpc": "2.0",
      "id": null,
      "method": "notification"
    },
    {
      "jsonrpc": "2.0",
      "id": "string-id",
      "method": "tools/list"
    },
    {
      "jsonrpc": "2.0",
      "id": 9999999999999999999,
      "method": "tools/list"
    }
  ]
}
EOF

# Generate SSE test data
cat > "$TEST_DATA_DIR/sse_test_data.txt" << 'EOF'
data: {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}

data: {"jsonrpc":"2.0","id":2,"method":"tools/list"}

data: {"jsonrpc":"2.0","method":"notification","params":{"type":"progress","value":50}}

event: error
data: {"error":"Connection timeout"}

event: ping
data: keep-alive

data: {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"complex_tool","arguments":{"nested":{"deep":{"value":true}}}}}

EOF

# Generate tool schemas
cat > "$TEST_DATA_DIR/tool_schemas.json" << 'EOF'
{
  "tools": [
    {
      "name": "file_reader",
      "description": "Reads content from a file",
      "inputSchema": {
        "type": "object",
        "properties": {
          "path": {
            "type": "string",
            "description": "File path to read"
          },
          "encoding": {
            "type": "string",
            "enum": ["utf8", "ascii", "base64"],
            "default": "utf8"
          }
        },
        "required": ["path"]
      }
    },
    {
      "name": "calculator",
      "description": "Performs mathematical operations",
      "inputSchema": {
        "type": "object",
        "properties": {
          "operation": {
            "type": "string",
            "enum": ["add", "subtract", "multiply", "divide"]
          },
          "operands": {
            "type": "array",
            "items": {"type": "number"},
            "minItems": 2
          }
        },
        "required": ["operation", "operands"]
      }
    },
    {
      "name": "async_processor",
      "description": "Simulates long-running async operation",
      "inputSchema": {
        "type": "object",
        "properties": {
          "duration": {
            "type": "integer",
            "minimum": 100,
            "maximum": 5000
          },
          "shouldFail": {
            "type": "boolean",
            "default": false
          }
        }
      }
    }
  ]
}
EOF

# Generate resource test data
cat > "$TEST_DATA_DIR/resource_test_data.json" << 'EOF'
{
  "resources": [
    {
      "uri": "file:///test/sample.txt",
      "name": "sample.txt",
      "mimeType": "text/plain",
      "content": "This is a test file content"
    },
    {
      "uri": "file:///test/data.json",
      "name": "data.json",
      "mimeType": "application/json",
      "content": "{\"key\": \"value\", \"nested\": {\"array\": [1,2,3]}}"
    },
    {
      "uri": "http://api.example.com/data",
      "name": "API Data",
      "mimeType": "application/json",
      "content": "{\"status\": \"ok\", \"timestamp\": \"2024-01-01T00:00:00Z\"}"
    }
  ],
  "subscriptions": [
    {
      "uri": "file:///test/watch.txt",
      "events": [
        {"type": "created", "timestamp": "2024-01-01T00:00:00Z"},
        {"type": "updated", "timestamp": "2024-01-01T00:01:00Z", "content": "Updated content"},
        {"type": "deleted", "timestamp": "2024-01-01T00:02:00Z"}
      ]
    }
  ]
}
EOF

# Generate performance test scenarios
cat > "$TEST_DATA_DIR/performance_scenarios.json" << 'EOF'
{
  "scenarios": [
    {
      "name": "High Frequency Requests",
      "description": "1000 requests/second for tool execution",
      "duration": "10s",
      "rps": 1000,
      "request_template": {
        "method": "tools/call",
        "params": {
          "name": "calculator",
          "arguments": {"operation": "add", "operands": [1, 2]}
        }
      }
    },
    {
      "name": "Large Payload Processing",
      "description": "Process 1MB JSON payloads",
      "duration": "30s",
      "rps": 10,
      "payload_size": "1MB"
    },
    {
      "name": "Concurrent Connections",
      "description": "100 concurrent SSE connections",
      "duration": "60s",
      "concurrent_connections": 100
    },
    {
      "name": "Memory Leak Detection",
      "description": "Long running test for memory leaks",
      "duration": "300s",
      "rps": 100,
      "monitor": ["memory", "handles", "threads"]
    }
  ]
}
EOF

# Generate error injection scenarios
cat > "$TEST_DATA_DIR/error_scenarios.json" << 'EOF'
{
  "network_errors": [
    {"type": "timeout", "after_ms": 5000},
    {"type": "disconnect", "after_bytes": 1024},
    {"type": "slow_connection", "throughput_bps": 1024}
  ],
  "protocol_errors": [
    {"type": "invalid_json", "payload": "{invalid json}"},
    {"type": "oversized_message", "size_mb": 10},
    {"type": "malformed_id", "id": {"nested": "object"}}
  ],
  "application_errors": [
    {"type": "tool_not_found", "tool": "non_existent_tool"},
    {"type": "resource_access_denied", "uri": "file:///etc/passwd"},
    {"type": "rate_limit_exceeded", "requests": 1001}
  ]
}
EOF

# Create C# test data generator
cat > "$TEST_DATA_DIR/TestDataGenerator.cs" << 'EOF'
using System;
using System.Collections.Generic;
using System.Text.Json;
using Bogus;

namespace McpServer.Tests.Data
{
    public static class TestDataGenerator
    {
        private static readonly Faker _faker = new();
        
        public static string GenerateJsonRpcRequest(string method, object? @params = null)
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = _faker.Random.Int(1, 1000),
                method,
                @params
            };
            
            return JsonSerializer.Serialize(request);
        }
        
        public static IEnumerable<string> GenerateToolNames(int count = 10)
        {
            for (int i = 0; i < count; i++)
            {
                yield return $"{_faker.Hacker.Verb()}_{_faker.Hacker.Noun()}".ToLower();
            }
        }
        
        public static Dictionary<string, object> GenerateComplexPayload(int depth = 3)
        {
            if (depth <= 0) return new Dictionary<string, object> { ["value"] = _faker.Random.Word() };
            
            return new Dictionary<string, object>
            {
                ["id"] = _faker.Random.Guid(),
                ["name"] = _faker.Name.FullName(),
                ["data"] = GenerateComplexPayload(depth - 1),
                ["items"] = _faker.Make(3, () => _faker.Lorem.Word()),
                ["timestamp"] = DateTimeOffset.UtcNow
            };
        }
        
        public static string GenerateLargePayload(int sizeMb)
        {
            var data = new List<object>();
            var targetSize = sizeMb * 1024 * 1024;
            var currentSize = 0;
            
            while (currentSize < targetSize)
            {
                var item = GenerateComplexPayload();
                var json = JsonSerializer.Serialize(item);
                currentSize += json.Length;
                data.Add(item);
            }
            
            return JsonSerializer.Serialize(data);
        }
    }
}
EOF

echo "✅ Test data generated in $TEST_DATA_DIR"
echo ""
echo "📁 Generated files:"
echo "  - jsonrpc_messages.json - Protocol message examples"
echo "  - sse_test_data.txt - SSE transport test data"
echo "  - tool_schemas.json - Tool definition examples"
echo "  - resource_test_data.json - Resource examples"
echo "  - performance_scenarios.json - Load test scenarios"
echo "  - error_scenarios.json - Error injection tests"
echo "  - TestDataGenerator.cs - Dynamic test data generator"