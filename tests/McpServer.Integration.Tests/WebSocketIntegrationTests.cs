using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using McpServer.Web;

namespace McpServer.Integration.Tests;

/// <summary>
/// Integration tests for WebSocket functionality.
/// </summary>
public class WebSocketIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public WebSocketIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task WebSocket_Endpoint_AcceptsConnections()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Create WebSocket client
        using var webSocketClient = new ClientWebSocket();
        
        // Convert HTTP URL to WebSocket URL
        var httpUrl = client.BaseAddress!.ToString();
        var wsUrl = httpUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "ws";
        
        try
        {
            // Act - Connect to WebSocket endpoint
            var uri = new Uri(wsUrl);
            await webSocketClient.ConnectAsync(uri, CancellationToken.None);
            
            // Assert
            Assert.Equal(WebSocketState.Open, webSocketClient.State);
            
            _output.WriteLine($"Successfully connected to WebSocket at {wsUrl}");
            
            // Test initialize message first (required before ping)
            var initMessage = new
            {
                jsonrpc = "2.0",
                method = "initialize",
                id = 1,
                @params = new
                {
                    protocolVersion = "0.1.0",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "Test Client",
                        version = "1.0.0"
                    }
                }
            };
            
            var initJson = JsonSerializer.Serialize(initMessage);
            var initBytes = Encoding.UTF8.GetBytes(initJson);
            
            await webSocketClient.SendAsync(
                new ArraySegment<byte>(initBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            
            _output.WriteLine($"Sent initialize message: {initJson}");
            
            // Receive initialize response
            var buffer = new byte[4096];
            var initResult = await webSocketClient.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None);
            
            Assert.Equal(WebSocketMessageType.Text, initResult.MessageType);
            
            var initResponseJson = Encoding.UTF8.GetString(buffer, 0, initResult.Count);
            _output.WriteLine($"Received initialize response: {initResponseJson}");
            
            // Now test ping message
            var pingMessage = new
            {
                jsonrpc = "2.0",
                method = "ping",
                id = 2
            };
            
            var pingJson = JsonSerializer.Serialize(pingMessage);
            var pingBytes = Encoding.UTF8.GetBytes(pingJson);
            
            await webSocketClient.SendAsync(
                new ArraySegment<byte>(pingBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            
            _output.WriteLine($"Sent ping message: {pingJson}");
            
            // Receive ping response
            var pingResult = await webSocketClient.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None);
            
            Assert.Equal(WebSocketMessageType.Text, pingResult.MessageType);
            
            var responseJson = Encoding.UTF8.GetString(buffer, 0, pingResult.Count);
            _output.WriteLine($"Received ping response: {responseJson}");
            
            // Parse and validate response
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
            Assert.True(response.TryGetProperty("jsonrpc", out var jsonrpc));
            Assert.Equal("2.0", jsonrpc.GetString());
            Assert.True(response.TryGetProperty("id", out var id));
            Assert.Equal(2, id.GetInt32());
        }
        catch (WebSocketException ex)
        {
            _output.WriteLine($"WebSocket error: {ex.Message}");
            throw;
        }
        finally
        {
            if (webSocketClient.State == WebSocketState.Open)
            {
                await webSocketClient.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Test completed",
                    CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task WebSocket_Endpoint_HandlesInvalidRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        using var webSocketClient = new ClientWebSocket();
        var httpUrl = client.BaseAddress!.ToString();
        var wsUrl = httpUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "ws";
        
        try
        {
            // Act - Connect and send invalid JSON
            await webSocketClient.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
            
            var invalidJson = "{ invalid json";
            var bytes = Encoding.UTF8.GetBytes(invalidJson);
            
            await webSocketClient.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            
            _output.WriteLine($"Sent invalid message: {invalidJson}");
            
            // Receive error response
            var buffer = new byte[4096];
            var result = await webSocketClient.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None);
            
            var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
            _output.WriteLine($"Received error response: {responseJson}");
            
            // Parse and validate error response
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
            Assert.True(response.TryGetProperty("jsonrpc", out _));
            Assert.True(response.TryGetProperty("error", out var error));
            Assert.True(error.TryGetProperty("code", out var code));
            Assert.Equal(-32700, code.GetInt32()); // Parse error
        }
        finally
        {
            if (webSocketClient.State == WebSocketState.Open)
            {
                await webSocketClient.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Test completed",
                    CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task WebSocket_Endpoint_HandlesInitializeAndToolsList()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        using var webSocketClient = new ClientWebSocket();
        var httpUrl = client.BaseAddress!.ToString();
        var wsUrl = httpUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "ws";
        
        try
        {
            // Connect
            await webSocketClient.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
            
            // Initialize
            var initMessage = new
            {
                jsonrpc = "2.0",
                method = "initialize",
                id = 1,
                @params = new
                {
                    protocolVersion = "0.1.0",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "Test Client",
                        version = "1.0.0"
                    }
                }
            };
            
            var initJson = JsonSerializer.Serialize(initMessage);
            var initBytes = Encoding.UTF8.GetBytes(initJson);
            
            await webSocketClient.SendAsync(
                new ArraySegment<byte>(initBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            
            // Receive initialize response
            var buffer = new byte[4096];
            var initResult = await webSocketClient.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None);
            
            var initResponseJson = Encoding.UTF8.GetString(buffer, 0, initResult.Count);
            _output.WriteLine($"Initialize response: {initResponseJson}");
            
            // Send tools/list request
            var toolsMessage = new
            {
                jsonrpc = "2.0",
                method = "tools/list",
                id = 2
            };
            
            var toolsJson = JsonSerializer.Serialize(toolsMessage);
            var toolsBytes = Encoding.UTF8.GetBytes(toolsJson);
            
            await webSocketClient.SendAsync(
                new ArraySegment<byte>(toolsBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            
            // Receive tools response
            var toolsResult = await webSocketClient.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None);
            
            var toolsResponseJson = Encoding.UTF8.GetString(buffer, 0, toolsResult.Count);
            _output.WriteLine($"Tools response: {toolsResponseJson}");
            
            // Validate tools response
            var toolsResponse = JsonSerializer.Deserialize<JsonElement>(toolsResponseJson);
            Assert.True(toolsResponse.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("tools", out var tools));
            
            var toolsArray = tools.EnumerateArray().ToList();
            Assert.True(toolsArray.Count > 0);
            
            // Should have echo, calculator, and datetime tools
            var toolNames = toolsArray.Select(t => t.GetProperty("name").GetString()).ToList();
            Assert.Contains("echo", toolNames);
            Assert.Contains("calculator", toolNames);
            Assert.Contains("datetime", toolNames);
        }
        finally
        {
            if (webSocketClient.State == WebSocketState.Open)
            {
                await webSocketClient.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Test completed",
                    CancellationToken.None);
            }
        }
    }
}