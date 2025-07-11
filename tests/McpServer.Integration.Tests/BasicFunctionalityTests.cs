using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using McpServer.Web;

namespace McpServer.Integration.Tests;

/// <summary>
/// Basic functionality integration tests using the web application.
/// </summary>
public class BasicFunctionalityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public BasicFunctionalityTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Application_Should_Start()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Root response: {content}");

        var json = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.True(json.TryGetProperty("name", out var name));
        Assert.Equal("MCP Server", name.GetString());
    }

    [Fact]
    public async Task Health_Endpoint_Should_ReturnHealthy()
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
    }

    [Fact]
    public async Task SSE_Endpoint_Should_HandlePing()
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
    }

    [Fact]
    public async Task SSE_Endpoint_Should_HandleInitializeSequence()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Step 1: Initialize
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

        var initResponseJson = JsonSerializer.Deserialize<JsonElement>(initResponseContent);
        Assert.True(initResponseJson.TryGetProperty("result", out var initResult));
        Assert.True(initResult.TryGetProperty("protocolVersion", out var protocolVersion));
        Assert.Equal("0.1.0", protocolVersion.GetString());

        // Step 2: List tools
        var toolsMessage = new
        {
            jsonrpc = "2.0",
            method = "tools/list",
            id = 2
        };

        var toolsJson = JsonSerializer.Serialize(toolsMessage);
        var toolsContent = new StringContent(toolsJson, Encoding.UTF8, "application/json");

        var toolsResponse = await client.PostAsync("/sse", toolsContent);
        toolsResponse.EnsureSuccessStatusCode();
        
        var toolsResponseContent = await toolsResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Tools response: {toolsResponseContent}");

        var toolsResponseJson = JsonSerializer.Deserialize<JsonElement>(toolsResponseContent);
        Assert.True(toolsResponseJson.TryGetProperty("result", out var toolsResult));
        Assert.True(toolsResult.TryGetProperty("tools", out var tools));
        
        var toolsArray = tools.EnumerateArray().ToList();
        Assert.True(toolsArray.Count > 0);
        
        var toolNames = toolsArray.Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("echo", toolNames);

        // Step 3: Execute tool
        var executeMessage = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = 3,
            @params = new
            {
                name = "echo",
                arguments = new
                {
                    message = "Hello Integration Test!"
                }
            }
        };

        var executeJson = JsonSerializer.Serialize(executeMessage);
        var executeContent = new StringContent(executeJson, Encoding.UTF8, "application/json");

        var executeResponse = await client.PostAsync("/sse", executeContent);
        executeResponse.EnsureSuccessStatusCode();
        
        var executeResponseContent = await executeResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Execute response: {executeResponseContent}");

        var executeResponseJson = JsonSerializer.Deserialize<JsonElement>(executeResponseContent);
        Assert.True(executeResponseJson.TryGetProperty("result", out var executeResult));
        Assert.True(executeResult.TryGetProperty("content", out var content));
        
        var contentArray = content.EnumerateArray().ToList();
        Assert.True(contentArray.Count > 0);
        
        var firstContent = contentArray[0];
        Assert.True(firstContent.TryGetProperty("type", out var type));
        Assert.Equal("text", type.GetString());
        Assert.True(firstContent.TryGetProperty("text", out var text));
        Assert.Contains("Hello Integration Test!", text.GetString());
    }

    [Fact]
    public async Task Application_Should_HandleConcurrentRequests()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tasks = new List<Task>();

        // Act - Send multiple ping requests concurrently
        for (int i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                var pingMessage = new
                {
                    jsonrpc = "2.0",
                    method = "ping",
                    id = taskId
                };

                var json = JsonSerializer.Serialize(pingMessage);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/sse", content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                Assert.True(responseJson.TryGetProperty("id", out var id));
                Assert.Equal(taskId, id.GetInt32());
            }));
        }

        // Assert - All requests should complete successfully
        await Task.WhenAll(tasks);
        _output.WriteLine("All concurrent requests completed successfully");
    }
}