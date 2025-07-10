using System.Diagnostics;
using McpServer.Application.Messages;
using McpServer.Application.Server;
using McpServer.Application.Services;
using McpServer.Application.Tracing;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles the initialize request.
/// </summary>
public class InitializeHandler : IMessageHandler
{
    private readonly ILogger<InitializeHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IMcpServer? _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitializeHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public InitializeHandler(ILogger<InitializeHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public bool CanHandle(Type messageType)
    {
        return messageType == typeof(InitializeRequest);
    }

    /// <inheritdoc/>
    public Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        using var activity = TracingExtensions.StartHandlerActivity("InitializeHandler", message.GetType().Name);
        
        try
        {
            if (message is not JsonRpcRequest<InitializeRequest> request)
            {
                throw new ArgumentException("Invalid message type", nameof(message));
            }

            if (request.Params == null)
            {
                throw new ProtocolException("Initialize request parameters cannot be null");
            }

            activity?.SetTag("initialize.client.name", request.Params.ClientInfo.Name);
            activity?.SetTag("initialize.client.version", request.Params.ClientInfo.Version);
            activity?.SetTag("initialize.protocol_version", request.Params.ProtocolVersion);

            _logger.LogInformation("Handling initialize request from client: {ClientName} v{ClientVersion}", 
                request.Params.ClientInfo.Name, 
                request.Params.ClientInfo.Version);

            // Lazily get the server instance
            _server ??= _serviceProvider.GetRequiredService<IMcpServer>();

            // Check if already initialized
            if (_server.IsInitialized)
            {
                throw new ProtocolException("Server is already initialized");
            }

            // Validate protocol version
            if (request.Params!.ProtocolVersion != "0.1.0")
            {
                throw new ProtocolException($"Unsupported protocol version: {request.Params.ProtocolVersion}");
            }

            // Update sampling service with client capabilities
            var samplingService = _serviceProvider.GetService<ISamplingService>();
            if (samplingService != null)
            {
                samplingService.SetClientCapabilities(request.Params.Capabilities);
            }

            // Build response
            var response = new InitializeResponse
            {
                ProtocolVersion = "0.1.0",
                ServerInfo = _server.ServerInfo,
                Capabilities = _server.Capabilities
            };

            // Mark server as initialized
            _server.SetInitialized(true);

            _logger.LogInformation("Server initialized successfully");

            return Task.FromResult<object?>(response);
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }
    }
}