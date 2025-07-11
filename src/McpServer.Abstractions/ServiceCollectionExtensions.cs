using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using McpServer.Application.Server;
using McpServer.Application.Handlers;
using McpServer.Application.Services;
using McpServer.Application.Middleware;
using McpServer.Application.Tools;
using McpServer.Domain.Services;
using McpServer.Domain.Security;
using McpServer.Domain.Tools;
using McpServer.Domain.Resources;
using McpServer.Domain.Prompts;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Validation;
using McpServer.Domain.Protocol;
using McpServer.Domain.Connection;
using McpServer.Application.Connection;
using McpServer.Infrastructure.Transport;
using McpServer.Infrastructure.Tools;
using McpServer.Infrastructure.Resources;
using McpServer.Infrastructure.Security;
using McpServer.Infrastructure.IO;
using McpServer.Domain.IO;
using McpServer.Application.Caching;
using McpServer.Application.HighAvailability;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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
        
        // Add registries as standalone services
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<IResourceRegistry, ResourceRegistry>();
        services.AddSingleton<IPromptRegistry, PromptRegistry>();
        
        // Add completion service (no longer needs lazy loading)
        services.AddSingleton<ICompletionService, CompletionService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IHeartbeatService, HeartbeatService>();
        services.AddSingleton<ICancellationManager, CancellationManager>();
        services.AddSingleton<IProgressTracker, ProgressTracker>();
        services.AddSingleton<IErrorResponseBuilder, ErrorResponseBuilder>();
        
        // Validation services
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<ValidationMiddleware>();
        services.AddSingleton<ValidatedToolFactory>();
        
        // Protocol version negotiation
        services.AddSingleton<IProtocolVersionNegotiator, ProtocolVersionNegotiator>();
        
        // Connection management
        services.AddSingleton<IConnectionManager, ConnectionManager>();
        // NOTE: Removed hosted service registration to prevent deadlock during startup
        // The ConnectionManager will be initialized when first accessed by MultiplexingMcpServer
        // services.AddHostedService(provider => provider.GetRequiredService<IConnectionManager>() as ConnectionManager ?? throw new InvalidOperationException());
        services.AddSingleton<IConnectionAwareMessageRouter, ConnectionAwareMessageRouter>();
        
        services.AddSingleton<IMcpServer>(provider =>
        {
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
            
            // Always use MultiplexingMcpServer (supports both single and multiple connections)
            var logger = provider.GetRequiredService<ILogger<MultiplexingMcpServer>>();
            var connectionManager = provider.GetRequiredService<IConnectionManager>();
            var connectionAwareRouter = provider.GetRequiredService<IConnectionAwareMessageRouter>();
            var notificationService = provider.GetRequiredService<INotificationService>();
            var samplingService = provider.GetService<ISamplingService>();
            var toolRegistry = provider.GetRequiredService<IToolRegistry>();
            var resourceRegistry = provider.GetRequiredService<IResourceRegistry>();
            var promptRegistry = provider.GetRequiredService<IPromptRegistry>();
            
            return new MultiplexingMcpServer(logger, connectionManager, connectionAwareRouter, notificationService, samplingService, toolRegistry, resourceRegistry, promptRegistry, serverInfo, capabilities);
        });
        
        // Note: IToolRegistry, IResourceRegistry, and IPromptRegistry are now registered as standalone services above

        services.AddSingleton<IMessageRouter>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<MessageRouter>>();
            var handlers = provider.GetServices<IMessageHandler>();
            var progressTracker = provider.GetRequiredService<IProgressTracker>();
            var errorResponseBuilder = provider.GetRequiredService<IErrorResponseBuilder>();
            var validationMiddleware = provider.GetRequiredService<ValidationMiddleware>();
            
            return new MessageRouter(logger, handlers, progressTracker, errorResponseBuilder, validationMiddleware);
        });

        // Message handlers
        services.AddSingleton<IMessageHandler, InitializeHandler>();
        services.AddSingleton<IMessageHandler, InitializedHandler>();
        services.AddSingleton<IMessageHandler, PingHandler>();
        services.AddSingleton<IMessageHandler, CancelHandler>();
        services.AddSingleton<IMessageHandler, ToolsHandler>();
        services.AddSingleton<IMessageHandler, ResourcesHandler>();
        services.AddSingleton<IMessageHandler, PromptsHandler>();
        services.AddSingleton<IMessageHandler, LoggingHandler>();
        services.AddSingleton<IMessageHandler, RootsHandler>();
        services.AddSingleton<IMessageHandler, CompletionHandler>();
        services.AddSingleton<IMessageHandler, SamplingHandler>();

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
        
        // IO abstractions
        services.AddSingleton<IFileSystem, FileSystem>();
        
        // Caching services
        services.AddMemoryCache(); // Built-in ASP.NET Core memory cache
        services.AddSingleton<ICacheService, MemoryCacheService>();
        
        // Cache configuration
        services.Configure<ToolCacheOptions>(options => { });
        services.Configure<ResourceCacheOptions>(options => { });
        
        // High availability services
        services.AddSingleton<IHealthCheckService, McpServer.Application.HighAvailability.HealthCheckService>();
        services.AddSingleton<ICircuitBreakerFactory, CircuitBreakerFactory>();
        services.AddSingleton<IRetryPolicyFactory, RetryPolicyFactory>();
        
        // Load balancers
        services.AddTransient<ILoadBalancer<object>, RoundRobinLoadBalancer<object>>();
        services.AddTransient<ILoadBalancer<object>, PriorityLoadBalancer<object>>();
        services.AddTransient<ILoadBalancer<object>, LeastConnectionsLoadBalancer<object>>();
        
        // Failover manager (registered as transient since it's generic)
        services.AddTransient(typeof(IFailoverManager<>), typeof(FailoverManager<>));
        
        // High availability configuration
        services.Configure<McpServer.Application.HighAvailability.HealthCheckServiceOptions>(options => { });
        services.Configure<CircuitBreakerOptions>(options => { });
        services.Configure<RetryPolicyOptions>(options => { });
        services.Configure<FailoverOptions>(options => { });
        
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
        services.AddSingleton<ITool, DataProcessingTool>();
        
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
        services.AddSingleton<IResourceProvider, DatabaseSchemaResourceProvider>();
        services.AddSingleton<IResourceProvider, RestApiResourceProvider>();
        
        services.Configure<FileSystemResourceOptions>(options =>
        {
            options.AllowedPaths = DefaultAllowedPaths;
            options.RecursiveSearch = true;
            options.ExcludePatterns = DefaultExcludePatterns;
        });
        
        services.Configure<DatabaseSchemaResourceOptions>(options =>
        {
            options.Databases = new List<string> { "customers", "analytics" };
            options.IncludeSystemTables = false;
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
        
        // Protocol version configuration
        var versionSection = configuration.GetSection("McpServer:ProtocolVersion");
        services.Configure<ProtocolVersionConfiguration>(options => versionSection.Bind(options));
        
        // Connection manager configuration
        var connectionSection = configuration.GetSection("McpServer:ConnectionManager");
        services.Configure<ConnectionManagerOptions>(options => connectionSection.Bind(options));
        
        // Cache configuration
        var cacheSection = configuration.GetSection("McpServer:Cache");
        services.Configure<MemoryCacheOptions>(options => cacheSection.GetSection("Memory").Bind(options));
        services.Configure<ToolCacheOptions>(options => cacheSection.GetSection("Tools").Bind(options));
        services.Configure<ResourceCacheOptions>(options => cacheSection.GetSection("Resources").Bind(options));
        services.Configure<CacheWarmupOptions>(options => cacheSection.GetSection("Warmup").Bind(options));
        
        // High availability configuration
        var haSection = configuration.GetSection("McpServer:HighAvailability");
        services.Configure<McpServer.Application.HighAvailability.HealthCheckServiceOptions>(options => haSection.GetSection("HealthChecks").Bind(options));
        services.Configure<CircuitBreakerOptions>(options => haSection.GetSection("CircuitBreaker").Bind(options));
        services.Configure<RetryPolicyOptions>(options => haSection.GetSection("RetryPolicy").Bind(options));
        services.Configure<FailoverOptions>(options => haSection.GetSection("Failover").Bind(options));
        
        return services;
    }
    
    /// <summary>
    /// Adds Redis distributed caching to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpRedisCache(this IServiceCollection services, string connectionString)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
        });
        
        // Replace memory cache with distributed cache
        services.AddSingleton<ICacheService, DistributedCacheService>();
        services.Configure<DistributedCacheOptions>(options => { });
        
        return services;
    }
    
    /// <summary>
    /// Adds cache warming strategies to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCacheWarming(this IServiceCollection services)
    {
        services.AddSingleton<ICacheWarmupService, CacheWarmupService>();
        services.AddHostedService<CacheWarmupHostedService>();
        
        return services;
    }
    
    /// <summary>
    /// Adds comprehensive health checks to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<MemoryHealthCheck>("memory", tags: new[] { "system", "memory" })
            .AddCheck<StartupHealthCheck>("startup", tags: new[] { "system", "startup" });
        
        return services;
    }
    
    /// <summary>
    /// Adds high availability features including circuit breakers, retry policies, and failover.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHighAvailability(this IServiceCollection services)
    {
        services.AddSingleton<IHealthCheckService, McpServer.Application.HighAvailability.HealthCheckService>();
        services.AddSingleton<ICircuitBreakerFactory, CircuitBreakerFactory>();
        services.AddSingleton<IRetryPolicyFactory, RetryPolicyFactory>();
        services.AddTransient(typeof(IFailoverManager<>), typeof(FailoverManager<>));
        
        // Add default load balancers
        services.AddTransient<ILoadBalancer<object>, PriorityLoadBalancer<object>>();
        
        return services;
    }
}