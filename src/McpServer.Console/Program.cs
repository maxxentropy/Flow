using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using McpServer.Abstractions;
using McpServer.Application.Server;
using McpServer.Domain.Tools;
using McpServer.Domain.Resources;
using McpServer.Infrastructure.Transport;
using System.Globalization;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File(
        "logs/mcpserver-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    Log.Information("Starting MCP Server Console Host");
    
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            // Add MCP server services
            services.AddMcpServer();
            services.AddMcpTools();
            services.AddMcpResources();
            services.ConfigureMcpServer(context.Configuration);
            
            // Add hosted service
            services.AddHostedService<McpServer.Console.McpServerHostedService>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

namespace McpServer.Console
{
    /// <summary>
    /// Hosted service for running the MCP server.
    /// </summary>
    public class McpServerHostedService : IHostedService
{
    private readonly Microsoft.Extensions.Logging.ILogger<McpServerHostedService> _logger;
    private readonly IMcpServer _mcpServer;
    private readonly ITransportManager _transportManager;
    private readonly IEnumerable<ITool> _tools;
    private readonly IEnumerable<IResourceProvider> _resourceProviders;

    public McpServerHostedService(
        Microsoft.Extensions.Logging.ILogger<McpServerHostedService> logger,
        IMcpServer mcpServer,
        ITransportManager transportManager,
        IEnumerable<ITool> tools,
        IEnumerable<IResourceProvider> resourceProviders)
    {
        _logger = logger;
        _mcpServer = mcpServer;
        _transportManager = transportManager;
        _tools = tools;
        _resourceProviders = resourceProviders;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MCP Server Hosted Service");
        
        // Register tools
        foreach (var tool in _tools)
        {
            _mcpServer.RegisterTool(tool);
        }
        
        // Register resource providers
        foreach (var provider in _resourceProviders)
        {
            _mcpServer.RegisterResourceProvider(provider);
        }
        
        // Start stdio transport
        await _transportManager.StartAsync(TransportType.Stdio, cancellationToken);
        
        _logger.LogInformation("MCP Server is running on stdio transport");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping MCP Server Hosted Service");
        await _transportManager.StopAsync(cancellationToken);
    }
}
}