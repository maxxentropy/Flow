using System.Collections.Concurrent;
using McpServer.Application.Connection;
using McpServer.Application.Services;
using McpServer.Domain.Connection;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Application.Server;

/// <summary>
/// MCP server implementation that supports multiple concurrent connections.
/// </summary>
public class MultiplexingMcpServer : IMcpServer, IDisposable
{
    private readonly ILogger<MultiplexingMcpServer> _logger;
    private readonly IConnectionManager _connectionManager;
    private readonly IConnectionAwareMessageRouter _messageRouter;
    private readonly INotificationService _notificationService;
    private readonly ISamplingService? _samplingService;
    private readonly IToolRegistry _toolRegistry;
    private readonly IResourceRegistry _resourceRegistry;
    private readonly IPromptRegistry _promptRegistry;
    private readonly ConcurrentDictionary<string, List<Action>> _connectionCleanupActions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiplexingMcpServer"/> class.
    /// </summary>
    public MultiplexingMcpServer(
        ILogger<MultiplexingMcpServer> logger,
        IConnectionManager connectionManager,
        IConnectionAwareMessageRouter messageRouter,
        INotificationService notificationService,
        ISamplingService? samplingService,
        IToolRegistry toolRegistry,
        IResourceRegistry resourceRegistry,
        IPromptRegistry promptRegistry,
        ServerInfo serverInfo,
        ServerCapabilities capabilities)
    {
        logger.LogInformation("STARTUP DEBUG: MultiplexingMcpServer constructor called");
        _logger = logger;
        logger.LogInformation("STARTUP DEBUG: Setting connection manager...");
        _connectionManager = connectionManager;
        logger.LogInformation("STARTUP DEBUG: Setting message router...");
        _messageRouter = messageRouter;
        logger.LogInformation("STARTUP DEBUG: Setting notification service...");
        _notificationService = notificationService;
        logger.LogInformation("STARTUP DEBUG: Setting sampling service...");
        _samplingService = samplingService;
        logger.LogInformation("STARTUP DEBUG: Setting registries...");
        _toolRegistry = toolRegistry;
        _resourceRegistry = resourceRegistry;
        _promptRegistry = promptRegistry;
        logger.LogInformation("STARTUP DEBUG: Setting server info and capabilities...");
        ServerInfo = serverInfo;
        Capabilities = capabilities;
        
        // Subscribe to registry events
        if (_toolRegistry is ToolRegistry toolReg)
        {
            toolReg.ToolRegistered += OnToolRegistered;
        }
        if (_resourceRegistry is ResourceRegistry resourceReg)
        {
            resourceReg.ResourceProviderRegistered += OnResourceProviderRegistered;
        }
        if (_promptRegistry is PromptRegistry promptReg)
        {
            promptReg.PromptProviderRegistered += OnPromptProviderRegistered;
        }
        
        // Set up connection event handlers
        logger.LogInformation("STARTUP DEBUG: Setting up connection event handlers...");
        _connectionManager.ConnectionEstablished += OnConnectionEstablished;
        _connectionManager.ConnectionClosed += OnConnectionClosed;
        logger.LogInformation("STARTUP DEBUG: MultiplexingMcpServer constructor completed");
        
        _logger.LogInformation("Multiplexing MCP server initialized");
    }

    /// <inheritdoc/>
    public ServerInfo ServerInfo { get; }

    /// <inheritdoc/>
    public ServerCapabilities Capabilities { get; }

    /// <inheritdoc/>
    public IConnectionManager ConnectionManager => _connectionManager;

    /// <inheritdoc/>
    public async Task StartAsync(ITransport transport, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting MCP server with transport {TransportType}", transport.GetType().Name);
        
        // Accept the connection
        await AcceptConnectionAsync(transport, null, cancellationToken);
        
        _logger.LogInformation("MCP server started successfully");
    }

    /// <inheritdoc/>
    public async Task<IConnection> AcceptConnectionAsync(
        ITransport transport, 
        string? connectionId = null, 
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionManager.AcceptConnectionAsync(transport, connectionId, cancellationToken);
        
        // Set up transport event handlers for this connection
        transport.MessageReceived += (sender, args) => OnMessageReceived(connection.ConnectionId, args);
        
        // Start the transport
        await transport.StartAsync(cancellationToken);
        
        _logger.LogInformation("Connection {ConnectionId} accepted and started", connection.ConnectionId);
        
        return connection;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping MCP server");
        
        await _connectionManager.CloseAllConnectionsAsync("Server shutdown", cancellationToken);
        
        _logger.LogInformation("MCP server stopped");
    }

    /// <inheritdoc/>
    public void RegisterTool(ITool tool)
    {
        _toolRegistry.RegisterTool(tool);
    }

    /// <inheritdoc/>
    public void RegisterResourceProvider(IResourceProvider provider)
    {
        _resourceRegistry.RegisterResourceProvider(provider);
    }

    /// <inheritdoc/>
    public void RegisterPromptProvider(IPromptProvider provider)
    {
        _promptRegistry.RegisterPromptProvider(provider);
    }

    private void OnToolRegistered(object? sender, ToolEventArgs e)
    {
        // Send notification to all connections if capabilities support it
        if (Capabilities.Tools?.ListChanged == true)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _messageRouter.BroadcastNotificationAsync(new
                    {
                        jsonrpc = "2.0",
                        method = "tools/list_changed"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast tools updated notification");
                }
            });
        }
    }

    private void OnResourceProviderRegistered(object? sender, ResourceProviderEventArgs e)
    {
        // Send notification to all connections if capabilities support it
        if (Capabilities.Resources?.ListChanged == true)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _messageRouter.BroadcastNotificationAsync(new
                    {
                        jsonrpc = "2.0",
                        method = "resources/list_changed"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast resources updated notification");
                }
            });
        }
    }

    private void OnPromptProviderRegistered(object? sender, PromptProviderEventArgs e)
    {
        // Send notification to all connections if capabilities support it
        if (Capabilities.Prompts?.ListChanged == true)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _messageRouter.BroadcastNotificationAsync(new
                    {
                        jsonrpc = "2.0",
                        method = "prompts/list_changed"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast prompts updated notification");
                }
            });
        }
    }

    private async void OnMessageReceived(string connectionId, MessageReceivedEventArgs e)
    {
        try
        {
            _logger.LogTrace("Message received from connection {ConnectionId}", connectionId);
            
            var response = await _messageRouter.RouteMessageAsync(connectionId, e.Message);
            
            if (response != null)
            {
                var connection = _connectionManager.GetConnection(connectionId);
                if (connection != null)
                {
                    await connection.SendAsync(response);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from connection {ConnectionId}", connectionId);
        }
    }

    private void OnConnectionEstablished(object? sender, ConnectionEventArgs e)
    {
        _logger.LogInformation("Connection {ConnectionId} established", e.Connection.ConnectionId);
        
        // Update notification service if it supports connection-specific notifications
        if (_notificationService is IConnectionAwareNotificationService connectionAwareService)
        {
            connectionAwareService.AddConnection(e.Connection);
        }
        
        // Update sampling service if it supports connection-specific sampling
        if (_samplingService is IConnectionAwareSamplingService connectionAwareSampling)
        {
            connectionAwareSampling.AddConnection(e.Connection);
        }
    }

    private void OnConnectionClosed(object? sender, ConnectionEventArgs e)
    {
        _logger.LogInformation("Connection {ConnectionId} closed. Reason: {Reason}", 
            e.Connection.ConnectionId, e.Reason ?? "None");
        
        // Update notification service
        if (_notificationService is IConnectionAwareNotificationService connectionAwareService)
        {
            connectionAwareService.RemoveConnection(e.Connection.ConnectionId);
        }
        
        // Update sampling service
        if (_samplingService is IConnectionAwareSamplingService connectionAwareSampling)
        {
            connectionAwareSampling.RemoveConnection(e.Connection.ConnectionId);
        }
    }

    /// <summary>
    /// Disposes the server resources.
    /// </summary>
    public void Dispose()
    {
        // Unsubscribe from registry events
        if (_toolRegistry is ToolRegistry toolReg)
        {
            toolReg.ToolRegistered -= OnToolRegistered;
        }
        if (_resourceRegistry is ResourceRegistry resourceReg)
        {
            resourceReg.ResourceProviderRegistered -= OnResourceProviderRegistered;
        }
        if (_promptRegistry is PromptRegistry promptReg)
        {
            promptReg.PromptProviderRegistered -= OnPromptProviderRegistered;
        }
        
        // Unsubscribe from connection events
        _connectionManager.ConnectionEstablished -= OnConnectionEstablished;
        _connectionManager.ConnectionClosed -= OnConnectionClosed;
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Interface for notification services that are aware of connections.
/// </summary>
public interface IConnectionAwareNotificationService : INotificationService
{
    /// <summary>
    /// Adds a connection to the notification service.
    /// </summary>
    /// <param name="connection">The connection to add.</param>
    void AddConnection(IConnection connection);
    
    /// <summary>
    /// Removes a connection from the notification service.
    /// </summary>
    /// <param name="connectionId">The connection ID to remove.</param>
    void RemoveConnection(string connectionId);
}

/// <summary>
/// Interface for sampling services that are aware of connections.
/// </summary>
public interface IConnectionAwareSamplingService : ISamplingService
{
    /// <summary>
    /// Adds a connection to the sampling service.
    /// </summary>
    /// <param name="connection">The connection to add.</param>
    void AddConnection(IConnection connection);
    
    /// <summary>
    /// Removes a connection from the sampling service.
    /// </summary>
    /// <param name="connectionId">The connection ID to remove.</param>
    void RemoveConnection(string connectionId);
}