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
using McpServer.Domain.Validation;
using McpServer.Domain.Validation.FluentValidators;
using McpServer.Domain.RateLimiting;
using McpServer.Web.Extensions;
using McpServer.Web.Middleware;

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
    Log.Information("========== STARTUP DEBUG: Starting MCP Server Web Host ==========");
    
    Log.Information("STARTUP DEBUG: Creating WebApplication builder...");
    var builder = WebApplication.CreateBuilder(args);
    Log.Information("STARTUP DEBUG: WebApplication builder created successfully");
    
    // Configure Kestrel to use specific port
    Log.Information("STARTUP DEBUG: Configuring Kestrel URLs...");
    builder.WebHost.UseUrls("http://localhost:5080");
    Log.Information("STARTUP DEBUG: Kestrel URLs configured to http://localhost:5080");
    
    // Add Serilog
    Log.Information("STARTUP DEBUG: Adding Serilog to host...");
    builder.Host.UseSerilog();
    Log.Information("STARTUP DEBUG: Serilog added to host");
    
    Log.Information("STARTUP DEBUG: Starting service configuration...");
    
    // Add services - but don't use the extension methods that might have circular dependencies
    // Instead, register services directly
    
    Log.Information("STARTUP DEBUG: Registering MessageRouter...");
    // Core services
    builder.Services.AddSingleton<IMessageRouter>(provider =>
    {
        Log.Information("STARTUP DEBUG: Creating MessageRouter instance...");
        Log.Information("STARTUP DEBUG: Resolving ILogger<MessageRouter>...");
        var logger = provider.GetRequiredService<ILogger<MessageRouter>>();
        Log.Information("STARTUP DEBUG: Resolving IEnumerable<IMessageHandler>...");
        var handlers = provider.GetRequiredService<IEnumerable<IMessageHandler>>();
        Log.Information("STARTUP DEBUG: Found {HandlerCount} message handlers", handlers.Count());
        Log.Information("STARTUP DEBUG: Resolving IProgressTracker...");
        var progressTracker = provider.GetRequiredService<IProgressTracker>();
        Log.Information("STARTUP DEBUG: Resolving IErrorResponseBuilder...");
        var errorResponseBuilder = provider.GetRequiredService<IErrorResponseBuilder>();
        Log.Information("STARTUP DEBUG: Resolving ValidationMiddleware...");
        var validationMiddleware = provider.GetService<ValidationMiddleware>();
        Log.Information("STARTUP DEBUG: Resolving RateLimitingMiddleware...");
        var rateLimitingMiddleware = provider.GetService<RateLimitingMiddleware>();
        Log.Information("STARTUP DEBUG: MessageRouter dependencies resolved, creating instance...");
        return new MessageRouter(logger, handlers, progressTracker, errorResponseBuilder, validationMiddleware, rateLimitingMiddleware);
    });
    Log.Information("STARTUP DEBUG: MessageRouter registered");
    
    // Message handlers
    Log.Information("STARTUP DEBUG: Registering message handlers...");
    builder.Services.AddSingleton<IMessageHandler, InitializeHandler>();
    builder.Services.AddSingleton<IMessageHandler, PingHandler>();
    builder.Services.AddSingleton<IMessageHandler, CancelHandler>();
    builder.Services.AddSingleton<IMessageHandler, ToolsHandler>();
    builder.Services.AddSingleton<IMessageHandler, ResourcesHandler>();
    builder.Services.AddSingleton<IMessageHandler, PromptsHandler>();
    builder.Services.AddSingleton<IMessageHandler, LoggingHandler>();
    builder.Services.AddSingleton<IMessageHandler, RootsHandler>();
    builder.Services.AddSingleton<IMessageHandler, CompletionHandler>();
    Log.Information("STARTUP DEBUG: Message handlers registered");
    
    // Add notification service
    builder.Services.AddSingleton<INotificationService, NotificationService>();
    
    // Add sampling service
    builder.Services.AddSingleton<ISamplingService, SamplingService>();
    
    // Add logging service
    builder.Services.AddSingleton<ILoggingService, LoggingService>();
    
    // Add root registry
    builder.Services.AddSingleton<IRootRegistry, RootRegistry>();
    
    // Add completion service (using IServiceProvider to break circular dependency)
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
    
    // Add cancellation manager
    builder.Services.AddSingleton<ICancellationManager, CancellationManager>();
    
    // Add progress tracker
    builder.Services.AddSingleton<IProgressTracker, ProgressTracker>();
    
    // Add error response builder
    builder.Services.AddSingleton<IErrorResponseBuilder, ErrorResponseBuilder>();
    
    // Add validation service
    builder.Services.AddSingleton<IValidationService, ValidationService>();
    builder.Services.AddSingleton<ValidationMiddleware>();
    builder.Services.AddSingleton<ValidationPerformanceMonitor>();
    
    // Add rate limiting
    builder.Services.AddSingleton<IRateLimiter, RateLimiter>();
    builder.Services.AddSingleton<RateLimitingMiddleware>();
    builder.Services.Configure<RateLimitConfiguration>(
        builder.Configuration.GetSection("McpServer:RateLimiting"));
    
    // Add protocol version negotiation
    builder.Services.AddSingleton<IProtocolVersionNegotiator, ProtocolVersionNegotiator>();
    builder.Services.Configure<ProtocolVersionConfiguration>(
        builder.Configuration.GetSection("McpServer:ProtocolVersion"));
    
    // Add OpenTelemetry
    builder.Services.AddOpenTelemetryObservability(builder.Configuration);
    
    // Add MCP Server services
    Log.Information("STARTUP DEBUG: Adding MCP Server services...");
    builder.Services.AddMcpServer();
    Log.Information("STARTUP DEBUG: MCP Server services added");
    
    // Transport
    builder.Services.AddSingleton<ITransportManager, TransportManager>();
    builder.Services.AddTransient<SseTransport>();
    builder.Services.AddTransient<WebSocketTransport>();
    
    // Authentication providers are registered in ServiceCollectionExtensions.cs
    
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
    
    Log.Information("STARTUP DEBUG: Building application...");
    var app = builder.Build();
    Log.Information("STARTUP DEBUG: Application built successfully");
    
    // Initialize services but don't block
    Log.Information("STARTUP DEBUG: Creating service scope...");
    using (var scope = app.Services.CreateScope())
    {
        Log.Information("STARTUP DEBUG: Service scope created, resolving IMcpServer...");
        var mcpServer = scope.ServiceProvider.GetRequiredService<IMcpServer>();
        Log.Information("STARTUP DEBUG: IMcpServer resolved, resolving tools...");
        var tools = scope.ServiceProvider.GetServices<ITool>().ToList();
        Log.Information("STARTUP DEBUG: Tools resolved, resolving resource providers...");
        var resourceProviders = scope.ServiceProvider.GetServices<IResourceProvider>().ToList();
        
        Log.Information("STARTUP DEBUG: Found {ToolCount} tools and {ProviderCount} resource providers", 
            tools.Count, resourceProviders.Count);
        
        // Register tools and resources in background to avoid blocking startup
        Log.Information("STARTUP DEBUG: Starting background registration task...");
        _ = Task.Run(() =>
        {
            try
            {
                Log.Information("STARTUP DEBUG: Registering tools in background...");
                foreach (var tool in tools)
                {
                    Log.Debug("STARTUP DEBUG: Registering tool: {ToolName}", tool.Name);
                    mcpServer.RegisterTool(tool);
                }
                
                Log.Information("STARTUP DEBUG: Registering resource providers in background...");
                foreach (var provider in resourceProviders)
                {
                    Log.Debug("STARTUP DEBUG: Registering resource provider: {ProviderType}", provider.GetType().Name);
                    mcpServer.RegisterResourceProvider(provider);
                }
                
                Log.Information("STARTUP DEBUG: Background registration completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "STARTUP DEBUG: Error during background registration");
            }
        });
        Log.Information("STARTUP DEBUG: Background registration task started");
    }
    Log.Information("STARTUP DEBUG: Service scope disposed");
    
    // Configure pipeline
    Log.Information("STARTUP DEBUG: Configuring middleware pipeline...");
    if (app.Environment.IsDevelopment())
    {
        Log.Information("STARTUP DEBUG: Adding Swagger middleware...");
        app.UseSwagger();
        app.UseSwaggerUI();
        Log.Information("STARTUP DEBUG: Swagger middleware added");
    }
    
    Log.Information("STARTUP DEBUG: Adding CORS middleware...");
    app.UseCors("McpCors");
    Log.Information("STARTUP DEBUG: CORS middleware added");
    
    // Add OpenTelemetry enrichment
    Log.Information("STARTUP DEBUG: Adding OpenTelemetry enrichment...");
    app.UseOpenTelemetryEnrichment();
    Log.Information("STARTUP DEBUG: OpenTelemetry enrichment added");
    
    // Add metrics middleware
    Log.Information("STARTUP DEBUG: Adding metrics middleware...");
    app.UseMetrics();
    Log.Information("STARTUP DEBUG: Metrics middleware added");
    
    // Add rate limiting middleware
    Log.Information("STARTUP DEBUG: Adding rate limiting middleware...");
    app.UseHttpRateLimiting();
    Log.Information("STARTUP DEBUG: Rate limiting middleware added");
    
    // Add WebSocket support
    Log.Information("STARTUP DEBUG: Adding WebSocket support...");
    app.UseWebSockets();
    app.UseWebSocketMcp("/ws");
    Log.Information("STARTUP DEBUG: WebSocket support added");
    
    // Map controllers
    Log.Information("STARTUP DEBUG: Mapping controllers...");
    app.MapControllers();
    Log.Information("STARTUP DEBUG: Controllers mapped");
    
    // Map endpoints
    Log.Information("STARTUP DEBUG: Mapping endpoints...");
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
    Log.Information("STARTUP DEBUG: Root endpoint mapped");
    
    app.MapGet("/health", (IMcpServer mcpServer) =>
    {
        return Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow,
            services = new
            {
                mcpServer = (mcpServer.ServerInfo != null && mcpServer.Capabilities != null) ? "ready" : "not ready",
                sseTransport = "available"
            }
        });
    });
    Log.Information("STARTUP DEBUG: Health endpoint mapped");
    
    // SSE endpoint
    Log.Information("STARTUP DEBUG: Mapping SSE endpoint...");
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
            
            // Get rate limit context from middleware
            var rateLimitContext = context.Items["RateLimitContext"] as RateLimitContext;
            
            // Route the message through the message router with rate limiting
            var response = await messageRouter.RouteMessageAsync(requestBody, rateLimitContext);
            
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
    Log.Information("STARTUP DEBUG: SSE endpoint mapped");
    
    Log.Information("STARTUP DEBUG: All endpoints mapped, starting application on http://localhost:5080");
    Log.Information("STARTUP DEBUG: Calling app.Run()...");
    app.Run();
    Log.Information("STARTUP DEBUG: Application stopped");
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

/// <summary>
/// Program class for testing purposes.
/// </summary>
public partial class Program { }