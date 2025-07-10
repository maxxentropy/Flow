using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.Infrastructure.Transport;

/// <summary>
/// Transport types supported by the MCP server.
/// </summary>
public enum TransportType
{
    /// <summary>
    /// Standard input/output transport.
    /// </summary>
    Stdio,
    
    /// <summary>
    /// Server-Sent Events transport.
    /// </summary>
    ServerSentEvents,
    
    /// <summary>
    /// WebSocket transport (future implementation).
    /// </summary>
    WebSocket
}

/// <summary>
/// Manages multiple transport instances.
/// </summary>
public interface ITransportManager
{
    /// <summary>
    /// Starts a specific transport type.
    /// </summary>
    /// <param name="transportType">The type of transport to start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the start operation.</returns>
    Task StartAsync(TransportType transportType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts all enabled transports.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the start operation.</returns>
    Task StartAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all running transports.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the stop operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a specific transport is running.
    /// </summary>
    /// <param name="transportType">The type of transport.</param>
    /// <returns>True if the transport is running; otherwise, false.</returns>
    bool IsRunning(TransportType transportType);

    /// <summary>
    /// Gets a specific transport instance.
    /// </summary>
    /// <param name="transportType">The type of transport.</param>
    /// <returns>The transport instance if running; otherwise, null.</returns>
    ITransport? GetTransport(TransportType transportType);
}

/// <summary>
/// Default implementation of the transport manager.
/// </summary>
public class TransportManager : ITransportManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TransportManager> _logger;
    private readonly IMcpServer _mcpServer;
    private readonly ConcurrentDictionary<TransportType, ITransport> _activeTransports = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportManager"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="mcpServer">The MCP server.</param>
    public TransportManager(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<TransportManager> logger,
        IMcpServer mcpServer)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _mcpServer = mcpServer;
    }

    /// <inheritdoc/>
    public async Task StartAsync(TransportType transportType, CancellationToken cancellationToken = default)
    {
        if (_activeTransports.ContainsKey(transportType))
        {
            _logger.LogWarning("Transport {TransportType} is already running", transportType);
            return;
        }

        var transport = CreateTransport(transportType);
        if (transport == null)
        {
            _logger.LogWarning("Transport {TransportType} is not available or disabled", transportType);
            return;
        }

        _logger.LogInformation("Starting transport: {TransportType}", transportType);

        try
        {
            await _mcpServer.StartAsync(transport, cancellationToken).ConfigureAwait(false);
            _activeTransports[transportType] = transport;
            _logger.LogInformation("Transport {TransportType} started successfully", transportType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start transport {TransportType}", transportType);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        if (_configuration.GetValue<bool>("McpServer:Transport:Stdio:Enabled"))
        {
            tasks.Add(StartAsync(TransportType.Stdio, cancellationToken));
        }

        if (_configuration.GetValue<bool>("McpServer:Transport:Sse:Enabled"))
        {
            tasks.Add(StartAsync(TransportType.ServerSentEvents, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping all transports");

        var tasks = _activeTransports.Values.Select(transport => 
            StopTransportAsync(transport, cancellationToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _activeTransports.Clear();
        _logger.LogInformation("All transports stopped");
    }

    /// <inheritdoc/>
    public bool IsRunning(TransportType transportType)
    {
        return _activeTransports.TryGetValue(transportType, out var transport) && 
               transport.IsConnected;
    }

    /// <inheritdoc/>
    public ITransport? GetTransport(TransportType transportType)
    {
        return _activeTransports.TryGetValue(transportType, out var transport) ? transport : null;
    }

    private ITransport? CreateTransport(TransportType transportType)
    {
        return transportType switch
        {
            TransportType.Stdio => _serviceProvider.GetService<StdioTransport>(),
            TransportType.ServerSentEvents => _serviceProvider.GetService<SseTransport>(),
            TransportType.WebSocket => _serviceProvider.GetService<WebSocketTransport>(),
            _ => null
        };
    }

    private async Task StopTransportAsync(ITransport transport, CancellationToken cancellationToken)
    {
        try
        {
            await transport.StopAsync(cancellationToken).ConfigureAwait(false);
            
            if (transport is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping transport {TransportType}", transport.GetType().Name);
        }
    }
}