using McpServer.Application.Services;
using McpServer.Domain.Transport;

namespace McpServer.Application.Server;

/// <summary>
/// Implementation of the MCP server.
/// </summary>
public class McpServer : IMcpServer, IToolRegistry, IResourceRegistry, IPromptRegistry
{
    private readonly ILogger<McpServer> _logger;
    private readonly IMessageRouter _messageRouter;
    private readonly INotificationService _notificationService;
    private readonly Dictionary<string, ITool> _tools = new();
    private readonly List<IResourceProvider> _resourceProviders = new();
    private readonly List<IPromptProvider> _promptProviders = new();
    private ITransport? _transport;
    private bool _isInitialized;

    private readonly ISamplingService? _samplingService;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="McpServer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="messageRouter">The message router.</param>
    /// <param name="notificationService">The notification service.</param>
    /// <param name="serverInfo">The server information.</param>
    /// <param name="capabilities">The server capabilities.</param>
    public McpServer(
        ILogger<McpServer> logger,
        IMessageRouter messageRouter,
        INotificationService notificationService,
        ServerInfo serverInfo,
        ServerCapabilities capabilities)
    {
        _logger = logger;
        _messageRouter = messageRouter;
        _notificationService = notificationService;
        ServerInfo = serverInfo;
        Capabilities = capabilities;
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="McpServer"/> class with sampling support.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="messageRouter">The message router.</param>
    /// <param name="notificationService">The notification service.</param>
    /// <param name="samplingService">The sampling service.</param>
    /// <param name="serverInfo">The server information.</param>
    /// <param name="capabilities">The server capabilities.</param>
    public McpServer(
        ILogger<McpServer> logger,
        IMessageRouter messageRouter,
        INotificationService notificationService,
        ISamplingService samplingService,
        ServerInfo serverInfo,
        ServerCapabilities capabilities) : this(logger, messageRouter, notificationService, serverInfo, capabilities)
    {
        _samplingService = samplingService;
    }

    /// <inheritdoc/>
    public ServerInfo ServerInfo { get; }

    /// <inheritdoc/>
    public ServerCapabilities Capabilities { get; }

    /// <inheritdoc/>
    public bool IsInitialized => _isInitialized;

    /// <inheritdoc/>
    public async Task StartAsync(ITransport transport, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting MCP server with transport {TransportType}", transport.GetType().Name);
        
        _transport = transport;
        _transport.MessageReceived += OnMessageReceived;
        _transport.Disconnected += OnDisconnected;
        
        // Set transport in notification service if it supports it
        if (_notificationService is NotificationService notificationService)
        {
            notificationService.SetTransport(transport);
        }
        
        // Set transport in sampling service if it supports it
        if (_samplingService is SamplingService samplingService)
        {
            samplingService.SetTransport(transport);
        }
        
        await _transport.StartAsync(cancellationToken).ConfigureAwait(false);
        
        _logger.LogInformation("MCP server started successfully");
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping MCP server");
        
        if (_transport != null)
        {
            _transport.MessageReceived -= OnMessageReceived;
            _transport.Disconnected -= OnDisconnected;
            await _transport.StopAsync(cancellationToken).ConfigureAwait(false);
            _transport = null;
        }
        
        _isInitialized = false;
        _logger.LogInformation("MCP server stopped");
    }

    /// <inheritdoc/>
    public void RegisterTool(ITool tool)
    {
        if (_tools.ContainsKey(tool.Name))
        {
            throw new InvalidOperationException($"Tool '{tool.Name}' is already registered");
        }
        
        _tools[tool.Name] = tool;
        _logger.LogInformation("Registered tool: {ToolName}", tool.Name);
        
        // Send notification if server is running and capabilities support it
        if (_transport != null && Capabilities.Tools?.ListChanged == true)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.NotifyToolsUpdatedAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send tools updated notification");
                }
            });
        }
    }

    /// <inheritdoc/>
    public void RegisterResourceProvider(IResourceProvider provider)
    {
        _resourceProviders.Add(provider);
        _logger.LogInformation("Registered resource provider: {ProviderType}", provider.GetType().Name);
        
        // Send notification if server is running and capabilities support it
        if (_transport != null && Capabilities.Resources?.ListChanged == true)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.NotifyResourcesUpdatedAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send resources updated notification");
                }
            });
        }
    }

    /// <inheritdoc/>
    public void RegisterPromptProvider(IPromptProvider provider)
    {
        _promptProviders.Add(provider);
        _logger.LogInformation("Registered prompt provider: {ProviderType}", provider.GetType().Name);
        
        // Send notification if server is running and capabilities support it
        if (_transport != null && Capabilities.Prompts?.ListChanged == true)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.NotifyPromptsUpdatedAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send prompts updated notification");
                }
            });
        }
    }

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    /// <returns>The collection of registered tools.</returns>
    public IReadOnlyDictionary<string, ITool> GetTools() => _tools;
    
    /// <inheritdoc/>
    public ITool? GetTool(string name) => _tools.TryGetValue(name, out var tool) ? tool : null;

    /// <summary>
    /// Gets all registered resource providers.
    /// </summary>
    /// <returns>The collection of registered resource providers.</returns>
    public IReadOnlyCollection<IResourceProvider> GetResourceProviders() => _resourceProviders;

    /// <summary>
    /// Gets all registered prompt providers.
    /// </summary>
    /// <returns>The collection of registered prompt providers.</returns>
    public IReadOnlyCollection<IPromptProvider> GetPromptProviders() => _promptProviders;

    /// <summary>
    /// Sets the initialization state.
    /// </summary>
    /// <param name="initialized">The initialization state.</param>
    public void SetInitialized(bool initialized)
    {
        _isInitialized = initialized;
    }

    private async void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Received message: {Message}", e.Message);
            
            var response = await _messageRouter.RouteMessageAsync(e.Message).ConfigureAwait(false);
            
            if (response != null && _transport != null)
            {
                await _transport.SendMessageAsync(response).ConfigureAwait(false);
                _logger.LogDebug("Sent response: {Response}", JsonSerializer.Serialize(response));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message");
            
            // Try to extract request ID for error response
            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(e.Message);
                if (request?.Id != null && _transport != null)
                {
                    var errorResponse = new JsonRpcResponse
                    {
                        Jsonrpc = "2.0",
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.InternalError,
                            Message = "Internal server error",
                            Data = ex.Message
                        },
                        Id = request.Id
                    };
                    
                    await _transport.SendMessageAsync(errorResponse).ConfigureAwait(false);
                }
            }
            catch
            {
                // If we can't send an error response, just log it
                _logger.LogError("Failed to send error response");
            }
        }
    }

    private void OnDisconnected(object? sender, DisconnectedEventArgs e)
    {
        _logger.LogInformation("Transport disconnected: {Reason}", e.Reason ?? "Unknown");
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Transport disconnection error");
        }
        
        _isInitialized = false;
    }
}