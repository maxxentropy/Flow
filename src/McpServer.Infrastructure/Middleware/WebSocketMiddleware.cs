using System.Net.WebSockets;
using McpServer.Application.Server;
using McpServer.Infrastructure.Transport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == _path)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                _logger.LogInformation("WebSocket upgrade request received from {RemoteIp}", 
                    context.Connection.RemoteIpAddress);

                try
                {
                    // Validate origin if configured
                    if (_options.Value.ValidateOrigin && _options.Value.AllowedOrigins.Count > 0)
                    {
                        var origin = context.Request.Headers["Origin"].ToString();
                        if (!string.IsNullOrEmpty(origin) && !_options.Value.AllowedOrigins.Contains(origin))
                        {
                            _logger.LogWarning("Rejected WebSocket connection from unauthorized origin: {Origin}", origin);
                            context.Response.StatusCode = 403;
                            await context.Response.WriteAsync("Forbidden: Origin not allowed");
                            return;
                        }
                    }

                    // Accept the WebSocket connection
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync(_options.Value.SubProtocol);
                    
                    // Generate a unique connection ID
                    var connectionId = Guid.NewGuid().ToString("N")[..8];
                    
                    // Get services from DI container
                    var messageRouter = context.RequestServices.GetRequiredService<IMessageRouter>();
                    var handlerLogger = context.RequestServices.GetRequiredService<ILogger<WebSocketHandler>>();
                    
                    // Create and start the WebSocket handler
                    using var handler = new WebSocketHandler(
                        handlerLogger,
                        messageRouter,
                        _options,
                        webSocket,
                        connectionId,
                        context.RequestAborted);
                    
                    await handler.HandleConnectionAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling WebSocket connection");
                    
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Internal server error");
                    }
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