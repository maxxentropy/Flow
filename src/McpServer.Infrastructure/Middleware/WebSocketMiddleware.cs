using System.Net.WebSockets;
using McpServer.Application.Server;
using McpServer.Infrastructure.Transport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Infrastructure.Middleware;

/// <summary>
/// Middleware for handling WebSocket connections for MCP communication.
/// </summary>
public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketMiddleware> _logger;
    private readonly IOptions<WebSocketTransportOptions> _options;
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The WebSocket transport options.</param>
    /// <param name="path">The path to handle WebSocket requests on.</param>
    public WebSocketMiddleware(
        RequestDelegate next,
        ILogger<WebSocketMiddleware> logger,
        IOptions<WebSocketTransportOptions> options,
        string path = "/ws")
    {
        _next = next;
        _logger = logger;
        _options = options;
        _path = path;
    }

    /// <summary>
    /// Processes HTTP requests and upgrades WebSocket requests.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="mcpServer">The MCP server instance.</param>
    /// <param name="transportFactory">Factory function to create WebSocket transport.</param>
    public async Task InvokeAsync(
        HttpContext context,
        IMcpServer mcpServer,
        Func<WebSocketTransport> transportFactory)
    {
        if (context.Request.Path == _path)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                _logger.LogInformation("WebSocket upgrade request received from {RemoteIp}", 
                    context.Connection.RemoteIpAddress);

                try
                {
                    // Create a new transport instance for this connection
                    var transport = transportFactory();
                    
                    // Accept the WebSocket connection
                    await transport.AcceptWebSocketAsync(context);
                    
                    // Start the MCP server with this transport
                    await mcpServer.StartAsync(transport);
                    
                    // Keep the connection alive until it's closed
                    while (transport.IsConnected)
                    {
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling WebSocket connection");
                    context.Response.StatusCode = 500;
                }
            }
            else
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("WebSocket connections only");
            }
        }
        else
        {
            await _next(context);
        }
    }
}

/// <summary>
/// Extension methods for adding WebSocket middleware.
/// </summary>
public static class WebSocketMiddlewareExtensions
{
    /// <summary>
    /// Adds WebSocket middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="path">The path to handle WebSocket requests on.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseWebSocketMcp(this IApplicationBuilder builder, string path = "/ws")
    {
        return builder.UseMiddleware<WebSocketMiddleware>(path);
    }
}