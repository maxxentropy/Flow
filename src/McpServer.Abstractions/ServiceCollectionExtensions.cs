using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using McpServer.Application.Server;
using McpServer.Application.Handlers;
using McpServer.Application.Services;
using McpServer.Application.Middleware;
using McpServer.Domain.Services;
using McpServer.Domain.Security;
using McpServer.Domain.Tools;
using McpServer.Domain.Resources;
using McpServer.Domain.Prompts;
using McpServer.Domain.Protocol.Messages;
using McpServer.Infrastructure.Transport;
using McpServer.Infrastructure.Tools;
using McpServer.Infrastructure.Resources;
using McpServer.Infrastructure.Security;

namespace McpServer.Abstractions;

/// <summary>
/// Extension methods for configuring MCP server services.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly string[] DefaultAllowedPaths = new[] { "." };
    private static readonly string[] DefaultExcludePatterns = new[] { ".git", "node_modules", "bin", "obj" };
    /// <summary>
    /// Adds MCP server services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpServer(this IServiceCollection services)
    {
        // Core server services
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ISamplingService, SamplingService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IRootRegistry, RootRegistry>();
        services.AddSingleton<ICompletionService, CompletionService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        
        services.AddSingleton(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<Application.Server.McpServer>>();
            var messageRouter = provider.GetRequiredService<IMessageRouter>();
            var notificationService = provider.GetRequiredService<INotificationService>();
            var samplingService = provider.GetRequiredService<ISamplingService>();
            var configuration = provider.GetRequiredService<IConfiguration>();
            
            var serverInfo = new ServerInfo
            {
                Name = configuration["McpServer:Name"] ?? "MCP Server",
                Version = configuration["McpServer:Version"] ?? "1.0.0"
            };
            
            var capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = true },
                Resources = new ResourcesCapability { Subscribe = true, ListChanged = true },
                Prompts = new PromptsCapability { ListChanged = true },
                Logging = new LoggingCapability(),
                Roots = new RootsCapability { ListChanged = true },
                Completion = new CompletionCapability()
            };
            
            return new Application.Server.McpServer(logger, messageRouter, notificationService, samplingService, serverInfo, capabilities);
        });
        
        // Register the segregated interfaces
        services.AddSingleton<IMcpServer>(provider => provider.GetRequiredService<Application.Server.McpServer>());
        services.AddSingleton<IToolRegistry>(provider => provider.GetRequiredService<Application.Server.McpServer>());
        services.AddSingleton<IResourceRegistry>(provider => provider.GetRequiredService<Application.Server.McpServer>());
        services.AddSingleton<IPromptRegistry>(provider => provider.GetRequiredService<Application.Server.McpServer>());

        services.AddSingleton<IMessageRouter, MessageRouter>();

        // Message handlers
        services.AddSingleton<IMessageHandler, InitializeHandler>();
        services.AddSingleton<IMessageHandler, ToolsHandler>();
        services.AddSingleton<IMessageHandler, ResourcesHandler>();
        services.AddSingleton<IMessageHandler, PromptsHandler>();
        services.AddSingleton<IMessageHandler, LoggingHandler>();
        services.AddSingleton<IMessageHandler, RootsHandler>();
        services.AddSingleton<IMessageHandler, CompletionHandler>();

        // Transport services
        services.AddSingleton<ITransportManager, TransportManager>();
        services.AddTransient<StdioTransport>();
        services.AddTransient<SseTransport>();
        services.AddTransient<WebSocketTransport>();
        
        // Configure transport options
        services.Configure<StdioTransportOptions>(options =>
        {
            options.BufferSize = 4096;
            options.Timeout = TimeSpan.FromMinutes(5);
        });
        
        services.Configure<SseTransportOptions>(options =>
        {
            options.Enabled = true;
            options.Path = "/sse";
            options.RequireHttps = true;
            options.ConnectionTimeout = TimeSpan.FromMinutes(30);
        });
        
        services.Configure<WebSocketTransportOptions>(options =>
        {
            options.ReceiveBufferSize = 4096;
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            options.ValidateOrigin = true;
            options.MaxMessageSize = 65536;
            options.ConnectionTimeout = TimeSpan.FromMinutes(30);
        });
        
        // Authentication services
        services.AddSingleton<IAuthenticationProvider, ApiKeyAuthenticationProvider>();
        services.AddSingleton<IAuthenticationProvider, JwtAuthenticationProvider>();
        services.AddSingleton<IMessageMiddleware, AuthenticationMiddleware>();
        
        // Configure authentication options
        services.Configure<ApiKeyAuthenticationOptions>(options => { });
        services.Configure<JwtAuthenticationOptions>(options => { });
        services.Configure<AuthenticationMiddlewareOptions>(options => { });

        return services;
    }

    /// <summary>
    /// Adds default MCP tools to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpTools(this IServiceCollection services)
    {
        services.AddSingleton<ITool, EchoTool>();
        services.AddSingleton<ITool, CalculatorTool>();
        services.AddSingleton<ITool, DateTimeTool>();
        services.AddSingleton<ITool, AiAssistantTool>();
        services.AddSingleton<ITool, LoggingDemoTool>();
        services.AddSingleton<ITool, RootsDemoTool>();
        services.AddSingleton<ITool, CompletionDemoTool>();
        services.AddSingleton<ITool, AuthenticationDemoTool>();
        
        return services;
    }

    /// <summary>
    /// Adds default MCP resource providers to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpResources(this IServiceCollection services)
    {
        services.AddSingleton<IResourceProvider, FileSystemResourceProvider>();
        
        services.Configure<FileSystemResourceOptions>(options =>
        {
            options.AllowedPaths = DefaultAllowedPaths;
            options.RecursiveSearch = true;
            options.ExcludePatterns = DefaultExcludePatterns;
        });
        
        return services;
    }

    /// <summary>
    /// Configures MCP server from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureMcpServer(this IServiceCollection services, IConfiguration configuration)
    {
        var stdioSection = configuration.GetSection("McpServer:Transport:Stdio");
        services.Configure<StdioTransportOptions>(options => stdioSection.Bind(options));
        
        var sseSection = configuration.GetSection("McpServer:Transport:Sse");
        services.Configure<SseTransportOptions>(options => sseSection.Bind(options));
        
        var wsSection = configuration.GetSection("McpServer:Transport:WebSocket");
        services.Configure<WebSocketTransportOptions>(options => wsSection.Bind(options));
        
        var fsSection = configuration.GetSection("McpServer:Resources:FileSystem");
        services.Configure<FileSystemResourceOptions>(options => fsSection.Bind(options));
        
        return services;
    }
}