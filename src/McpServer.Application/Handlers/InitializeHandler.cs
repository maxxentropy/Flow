using System.Diagnostics;
using McpServer.Application.Messages;
using McpServer.Application.Server;
using McpServer.Application.Services;
using McpServer.Application.Tracing;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol;
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
    private readonly IProtocolVersionNegotiator _versionNegotiator;
    private IMcpServer? _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitializeHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="versionNegotiator">The protocol version negotiator.</param>
    public InitializeHandler(
        ILogger<InitializeHandler> logger, 
        IServiceProvider serviceProvider,
        IProtocolVersionNegotiator versionNegotiator)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _versionNegotiator = versionNegotiator;
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

            // Note: Connection-level initialization is handled by ConnectionAwareMessageRouter
            // The router prevents duplicate initialization requests per connection

            // Negotiate protocol version
            ProtocolVersion negotiatedVersion;
            try
            {
                negotiatedVersion = _versionNegotiator.NegotiateVersion(request.Params!.ProtocolVersion);
                _logger.LogInformation("Protocol version negotiated: {NegotiatedVersion} (client requested: {ClientVersion})",
                    negotiatedVersion, request.Params.ProtocolVersion);
            }
            catch (ProtocolVersionException ex)
            {
                _logger.LogWarning(ex, "Protocol version negotiation failed");
                throw new ProtocolException(ex.Message);
            }

            // Update sampling service with client capabilities
            var samplingService = _serviceProvider.GetService<ISamplingService>();
            if (samplingService != null)
            {
                samplingService.SetClientCapabilities(request.Params.Capabilities);
            }

            // Build response with negotiated version
            var response = new InitializeResponse
            {
                ProtocolVersion = negotiatedVersion.ToString(),
                ServerInfo = _server.ServerInfo,
                Capabilities = _server.Capabilities
            };

            // Note: Connection is marked as initialized by ConnectionAwareMessageRouter
            // after successful completion of this handler

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