using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using McpServer.Web;

namespace McpServer.Integration.Tests;

/// <summary>
/// Integration tests for Server-Sent Events (SSE) functionality.
/// </summary>
public class SseIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public SseIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task SSE_Endpoint_AcceptsConnections()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act & Assert
        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Root endpoint response: {content}");

        // Parse JSON response
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.True(json.TryGetProperty("name", out var name));
        Assert.Equal("MCP Server", name.GetString());
        
        Assert.True(json.TryGetProperty("endpoints", out var endpoints));
        Assert.True(endpoints.TryGetProperty("sse", out var sseEndpoint));
        Assert.Equal("/sse", sseEndpoint.GetString());
    }

    [Fact]
    public async Task SSE_Endpoint_HandlesPingRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        var pingMessage = new
        {
            jsonrpc = "2.0",
            method = "ping",
            id = 1
        };

        var json = JsonSerializer.Serialize(pingMessage);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/sse", content);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Ping response: {responseContent}");

        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
        Assert.True(responseJson.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());
        Assert.True(responseJson.TryGetProperty("id", out var id));
        Assert.Equal(1, id.GetInt32());
        Assert.True(responseJson.TryGetProperty("result", out var result));
    }

    [Fact]
    public async Task SSE_Endpoint_HandlesInitializeRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

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

        var json = JsonSerializer.Serialize(initMessage);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/sse", content);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Initialize response: {responseContent}");

        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
        Assert.True(responseJson.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());
        Assert.True(responseJson.TryGetProperty("id", out var id));
        Assert.Equal(1, id.GetInt32());
        Assert.True(responseJson.TryGetProperty("result", out var result));
        
        // Check initialize result structure
        Assert.True(result.TryGetProperty("protocolVersion", out var protocolVersion));
        Assert.Equal("0.1.0", protocolVersion.GetString());
        Assert.True(result.TryGetProperty("serverInfo", out var serverInfo));
        Assert.True(result.TryGetProperty("capabilities", out var capabilities));
    }

    [Fact]
    public async Task SSE_Endpoint_HandlesToolsListAfterInitialize()
    {
        // Arrange
        var client = _factory.CreateClient();

        // First initialize
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
        var initContent = new StringContent(initJson, Encoding.UTF8, "application/json");
        
        var initResponse = await client.PostAsync("/sse", initContent);
        initResponse.EnsureSuccessStatusCode();
        
        var initResponseContent = await initResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Initialize response: {initResponseContent}");

        // Then list tools
        var toolsMessage = new
        {
            jsonrpc = "2.0",
            method = "tools/list",
            id = 2
        };

        var toolsJson = JsonSerializer.Serialize(toolsMessage);
        var toolsContent = new StringContent(toolsJson, Encoding.UTF8, "application/json");

        // Act
        var toolsResponse = await client.PostAsync("/sse", toolsContent);

        // Assert
        toolsResponse.EnsureSuccessStatusCode();
        
        var toolsResponseContent = await toolsResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Tools response: {toolsResponseContent}");

        var toolsResponseJson = JsonSerializer.Deserialize<JsonElement>(toolsResponseContent);
        Assert.True(toolsResponseJson.TryGetProperty("result", out var result));
        Assert.True(result.TryGetProperty("tools", out var tools));
        
        var toolsArray = tools.EnumerateArray().ToList();
        Assert.True(toolsArray.Count > 0);
        
        // Should have the registered tools
        var toolNames = toolsArray.Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("echo", toolNames);
        Assert.Contains("calculator", toolNames);
        Assert.Contains("datetime", toolNames);
    }

    [Fact]
    public async Task SSE_Endpoint_HandlesInvalidJSON()
    {
        // Arrange
        var client = _factory.CreateClient();
        var invalidJson = "{ invalid json";
        var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/sse", content);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Error response: {responseContent}");

        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
        Assert.True(responseJson.TryGetProperty("jsonrpc", out var jsonrpc));
        Assert.Equal("2.0", jsonrpc.GetString());
        Assert.True(responseJson.TryGetProperty("error", out var error));
        Assert.True(error.TryGetProperty("code", out var code));
        Assert.Equal(-32603, code.GetInt32()); // Internal error (since parsing happens at message router level)
    }

    [Fact]
    public async Task SSE_Endpoint_HandlesToolExecution()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Initialize first
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
        var initContent = new StringContent(initJson, Encoding.UTF8, "application/json");
        await client.PostAsync("/sse", initContent);

        // Execute echo tool
        var toolMessage = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 2,
            @params = new
            {
                name = "echo",
                arguments = new
                {
                    message = "Hello from SSE!"
                }
            }
        };

        var toolJson = JsonSerializer.Serialize(toolMessage);
        var toolContent = new StringContent(toolJson, Encoding.UTF8, "application/json");

        // Act
        var toolResponse = await client.PostAsync("/sse", toolContent);

        // Assert
        toolResponse.EnsureSuccessStatusCode();
        
        var toolResponseContent = await toolResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Tool response: {toolResponseContent}");

        var toolResponseJson = JsonSerializer.Deserialize<JsonElement>(toolResponseContent);
        Assert.True(toolResponseJson.TryGetProperty("result", out var result));
        Assert.True(result.TryGetProperty("content", out var content));
        
        var contentArray = content.EnumerateArray().ToList();
        Assert.True(contentArray.Count > 0);
        
        var firstContent = contentArray[0];
        Assert.True(firstContent.TryGetProperty("type", out var type));
        Assert.Equal("text", type.GetString());
        Assert.True(firstContent.TryGetProperty("text", out var text));
        Assert.Contains("Hello from SSE!", text.GetString());
    }

    [Fact]
    public async Task Health_Endpoint_ReturnsHealthStatus()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Health response: {content}");

        var json = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.True(json.TryGetProperty("status", out var status));
        Assert.Equal("healthy", status.GetString());
        Assert.True(json.TryGetProperty("timestamp", out _));
        Assert.True(json.TryGetProperty("services", out var services));
        Assert.True(services.TryGetProperty("mcpServer", out _));
        Assert.True(services.TryGetProperty("sseTransport", out var sseTransport));
        Assert.Equal("available", sseTransport.GetString());
    }
}