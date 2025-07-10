using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;
using McpServer.Abstractions;
using McpServer.Application.Server;
using McpServer.Application.Handlers;
using McpServer.Application.Messages;
using McpServer.Application.Services;
using McpServer.Application.Middleware;
using McpServer.Domain.Services;
using McpServer.Domain.Security;
using McpServer.Domain.Tools;
using McpServer.Domain.Resources;
using McpServer.Infrastructure.Transport;
using McpServer.Infrastructure.Tools;
using McpServer.Infrastructure.Resources;
using McpServer.Infrastructure.Security;
using McpServer.Infrastructure.Security.OAuth;
using McpServer.Infrastructure.Middleware;
using McpServer.Infrastructure.Services;
using System.Globalization;
using System.Text.Json;
using McpServer.Domain.Protocol;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Monitoring;
using McpServer.Web.Extensions;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File(
        "logs/mcpserver-web-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    Log.Information("Starting MCP Server Web Host");
    
    var builder = WebApplication.CreateBuilder(args);
    
    // Configure Kestrel to use specific port
    builder.WebHost.UseUrls("http://localhost:5080");
    
    // Add Serilog
    builder.Host.UseSerilog();
    
    Log.Information("Configuring services...");
    
    // Add services - but don't use the extension methods that might have circular dependencies
    // Instead, register services directly
    
    // Core services
    builder.Services.AddSingleton<IMessageRouter, MessageRouter>();
    
    // Message handlers
    builder.Services.AddSingleton<IMessageHandler, InitializeHandler>();
    builder.Services.AddSingleton<IMessageHandler, ToolsHandler>();
    builder.Services.AddSingleton<IMessageHandler, ResourcesHandler>();
    builder.Services.AddSingleton<IMessageHandler, PromptsHandler>();
    builder.Services.AddSingleton<IMessageHandler, LoggingHandler>();
    builder.Services.AddSingleton<IMessageHandler, RootsHandler>();
    builder.Services.AddSingleton<IMessageHandler, CompletionHandler>();
    
    // Add notification service
    builder.Services.AddSingleton<INotificationService, NotificationService>();
    
    // Add sampling service
    builder.Services.AddSingleton<ISamplingService, SamplingService>();
    
    // Add logging service
    builder.Services.AddSingleton<ILoggingService, LoggingService>();
    
    // Add root registry
    builder.Services.AddSingleton<IRootRegistry, RootRegistry>();
    
    // Add completion service
    builder.Services.AddSingleton<ICompletionService, CompletionService>();
    
    // Add authentication service
    builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
    
    // Add user repository
    builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
    
    // Add session services
    builder.Services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
    builder.Services.AddSingleton<ISessionService, SessionService>();
    
    // Add session cleanup background service
    builder.Services.Configure<SessionCleanupOptions>(
        builder.Configuration.GetSection("McpServer:SessionCleanup"));
    builder.Services.AddHostedService<SessionCleanupService>();
    
    // Add user profile service
    builder.Services.AddSingleton<IUserProfileService, UserProfileService>();
    
    // Add monitoring services
    builder.Services.AddSingleton<IMetricsService, MetricsService>();
    builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();
    
    // Add OpenTelemetry
    builder.Services.AddOpenTelemetryObservability(builder.Configuration);
    
    // Add MCP Server with explicit dependencies
    builder.Services.AddSingleton<McpServer.Application.Server.McpServer>(provider =>
    {
        Log.Information("Creating McpServer instance...");
        var logger = provider.GetRequiredService<ILogger<McpServer.Application.Server.McpServer>>();
        Log.Information("Got logger for McpServer");
        var messageRouter = provider.GetRequiredService<IMessageRouter>();
        Log.Information("Got message router");
        var notificationService = provider.GetRequiredService<INotificationService>();
        Log.Information("Got notification service");
        var samplingService = provider.GetRequiredService<ISamplingService>();
        Log.Information("Got sampling service");
        
        var serverInfo = new ServerInfo
        {
            Name = "MCP Server",
            Version = "1.0.0"
        };
        
        var capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability { ListChanged = true },
            Resources = new ResourcesCapability { Subscribe = false, ListChanged = true },
            Prompts = new PromptsCapability { ListChanged = true },
            Logging = new LoggingCapability(),
            Roots = new RootsCapability { ListChanged = true },
            Completion = new CompletionCapability()
        };
        
        Log.Information("Creating McpServer instance...");
        return new McpServer.Application.Server.McpServer(logger, messageRouter, notificationService, samplingService, serverInfo, capabilities);
    });
    
    // Register the same instance for all interfaces it implements
    builder.Services.AddSingleton<IMcpServer>(provider => provider.GetRequiredService<McpServer.Application.Server.McpServer>());
    builder.Services.AddSingleton<IToolRegistry>(provider => provider.GetRequiredService<McpServer.Application.Server.McpServer>());
    builder.Services.AddSingleton<IResourceRegistry>(provider => provider.GetRequiredService<McpServer.Application.Server.McpServer>());
    builder.Services.AddSingleton<IPromptRegistry>(provider => provider.GetRequiredService<McpServer.Application.Server.McpServer>());
    
    // Transport
    builder.Services.AddSingleton<ITransportManager, TransportManager>();
    builder.Services.AddTransient<SseTransport>();
    builder.Services.AddTransient<WebSocketTransport>();
    
    // Authentication providers
    builder.Services.AddSingleton<IAuthenticationProvider, ApiKeyAuthenticationProvider>();
    builder.Services.AddSingleton<IAuthenticationProvider, JwtAuthenticationProvider>();
    builder.Services.AddSingleton<IAuthenticationProvider, OAuthAuthenticationProvider>();
    builder.Services.AddSingleton<IAuthenticationProvider, SessionAuthenticationProvider>();
    
    // OAuth providers
    builder.Services.AddSingleton<IOAuthProvider, GoogleOAuthProvider>();
    builder.Services.AddSingleton<IOAuthProvider, MicrosoftOAuthProvider>();
    builder.Services.AddSingleton<IOAuthProvider, GitHubOAuthProvider>();
    
    // Add HttpClient factory for OAuth providers
    builder.Services.AddHttpClient();
    
    // Tools
    builder.Services.AddSingleton<ITool, EchoTool>();
    builder.Services.AddSingleton<ITool, CalculatorTool>();
    builder.Services.AddSingleton<ITool, DateTimeTool>();
    builder.Services.AddSingleton<ITool, AuthenticationDemoTool>();
    
    // Resources
    builder.Services.AddSingleton<IResourceProvider, FileSystemResourceProvider>();
    
    // Configure options
    builder.Services.Configure<SseTransportOptions>(options =>
    {
        options.Enabled = true;
        options.Path = "/sse";
        options.RequireHttps = false; // For development
        options.ConnectionTimeout = TimeSpan.FromMinutes(30);
    });
    
    builder.Services.Configure<WebSocketTransportOptions>(options =>
    {
        options.ReceiveBufferSize = 4096;
        options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        options.ValidateOrigin = false; // For development
        options.MaxMessageSize = 65536;
        options.ConnectionTimeout = TimeSpan.FromMinutes(30);
    });
    
    // Configure authentication options from configuration
    builder.Services.Configure<ApiKeyAuthenticationOptions>(
        builder.Configuration.GetSection("McpServer:ApiKeyAuthentication"));
    builder.Services.Configure<JwtAuthenticationOptions>(
        builder.Configuration.GetSection("McpServer:JwtAuthentication"));
    builder.Services.Configure<AuthenticationMiddlewareOptions>(
        builder.Configuration.GetSection("McpServer:Authentication"));
    builder.Services.Configure<McpServer.Domain.Security.SessionOptions>(
        builder.Configuration.GetSection("McpServer:Sessions"));
    
    // Static arrays for CA1861
    var allowedPaths = new[] { "." };
    var excludePatterns = new[] { ".git", "node_modules", "bin", "obj" };
    
    builder.Services.Configure<FileSystemResourceOptions>(options =>
    {
        options.AllowedPaths = allowedPaths;
        options.RecursiveSearch = true;
        options.ExcludePatterns = excludePatterns;
    });
    
    // Add controllers for OAuth endpoints
    builder.Services.AddControllers();
    
    // Add Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("McpCors", policy =>
        {
            // In development, allow any origin
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });
    
    Log.Information("Building application...");
    var app = builder.Build();
    Log.Information("Application built successfully");
    
    // Initialize services but don't block
    Log.Information("Creating service scope...");
    using (var scope = app.Services.CreateScope())
    {
        Log.Information("Service scope created, resolving IMcpServer...");
        var mcpServer = scope.ServiceProvider.GetRequiredService<IMcpServer>();
        Log.Information("IMcpServer resolved, resolving tools...");
        var tools = scope.ServiceProvider.GetServices<ITool>().ToList();
        Log.Information("Tools resolved, resolving resource providers...");
        var resourceProviders = scope.ServiceProvider.GetServices<IResourceProvider>().ToList();
        
        Log.Information("Found {ToolCount} tools and {ProviderCount} resource providers", 
            tools.Count, resourceProviders.Count);
        
        // Register tools and resources in background to avoid blocking startup
        _ = Task.Run(() =>
        {
            try
            {
                Log.Information("Registering tools in background...");
                foreach (var tool in tools)
                {
                    Log.Debug("Registering tool: {ToolName}", tool.Name);
                    mcpServer.RegisterTool(tool);
                }
                
                Log.Information("Registering resource providers in background...");
                foreach (var provider in resourceProviders)
                {
                    Log.Debug("Registering resource provider: {ProviderType}", provider.GetType().Name);
                    mcpServer.RegisterResourceProvider(provider);
                }
                
                Log.Information("Background registration completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during background registration");
            }
        });
    }
    
    // Configure pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    
    app.UseCors("McpCors");
    
    // Add OpenTelemetry enrichment
    app.UseOpenTelemetryEnrichment();
    
    // Add metrics middleware
    app.UseMetrics();
    
    // Add WebSocket support
    app.UseWebSockets();
    app.UseWebSocketMcp("/ws");
    
    // Map controllers
    app.MapControllers();
    
    // Map endpoints
    app.MapGet("/", () => Results.Ok(new
    {
        name = "MCP Server",
        version = "1.0.0",
        endpoints = new
        {
            sse = "/sse",
            websocket = "/ws",
            health = "/health",
            swagger = app.Environment.IsDevelopment() ? "/swagger" : null
        }
    }));
    
    app.MapGet("/health", (IMcpServer mcpServer) =>
    {
        return Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow,
            services = new
            {
                mcpServer = mcpServer.IsInitialized ? "initialized" : "not initialized",
                sseTransport = "available"
            }
        });
    });
    
    // SSE endpoint
    app.MapPost("/sse", async (HttpContext context, IServiceProvider serviceProvider) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            // Read and parse request
            using var reader = new StreamReader(context.Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            logger.LogDebug("Received request: {Request}", requestBody);
            
            // Get services
            var mcpServer = serviceProvider.GetRequiredService<IMcpServer>();
            var messageRouter = serviceProvider.GetRequiredService<IMessageRouter>();
            
            // Parse JSON-RPC request
            var jsonDoc = JsonDocument.Parse(requestBody);
            var root = jsonDoc.RootElement;
            
            // Extract method and id
            if (!root.TryGetProperty("method", out var methodElement))
            {
                await WriteErrorResponse(context, null, -32600, "Invalid Request");
                return;
            }
            
            var method = methodElement.GetString();
            var id = root.TryGetProperty("id", out var idElement) ? idElement.GetRawText() : null;
            
            // Route the message through the message router
            var response = await messageRouter.RouteMessageAsync(requestBody);
            
            // Write response
            context.Response.ContentType = "application/json";
            if (response != null)
            {
                await context.Response.WriteAsJsonAsync(response);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling SSE request");
            await WriteErrorResponse(context, null, -32603, "Internal error");
        }
    });
    
    Log.Information("Starting application on http://localhost:5080");
    app.Run();
    Log.Information("Application stopped");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

async Task WriteErrorResponse(HttpContext context, string? id, int code, string message)
{
    var response = new
    {
        jsonrpc = "2.0",
        id = id != null ? JsonSerializer.Deserialize<object>(id) : null,
        error = new
        {
            code = code,
            message = message
        }
    };
    
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(response);
}