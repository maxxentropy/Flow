using McpServer.Application.Server;
using McpServer.Application.Services;
using McpServer.Domain.Connection;
using McpServer.Domain.Protocol.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles the initialized notification from clients.
/// </summary>
public class InitializedHandler : IMessageHandler
{
    private readonly ILogger<InitializedHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IMcpServer? _server;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="InitializedHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public InitializedHandler(ILogger<InitializedHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    /// <inheritdoc/>
    public bool CanHandle(Type messageType) => messageType == typeof(InitializedNotification);
    
    /// <inheritdoc/>
    public Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        var notification = (InitializedNotification)message;
        
        _logger.LogInformation("Received initialized notification from client");
        
        // For connection-aware servers, mark the connection as fully ready
        _server ??= _serviceProvider.GetRequiredService<IMcpServer>();
        
        if (_server is MultiplexingMcpServer multiplexingServer)
        {
            // In a real implementation, we'd need the connection context
            // For now, log that the client is ready
            _logger.LogInformation("Client initialization complete and ready for requests");
        }
        
        // Notifications don't have responses
        return Task.FromResult((object?)null);
    }
}