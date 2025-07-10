using System.Diagnostics;
using McpServer.Application.Messages;
using McpServer.Application.Services;
using McpServer.Application.Tracing;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles roots-related requests.
/// </summary>
public class RootsHandler : IMessageHandler
{
    private readonly ILogger<RootsHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IRootRegistry? _rootRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="RootsHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public RootsHandler(ILogger<RootsHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public bool CanHandle(Type messageType)
    {
        return messageType == typeof(RootsListRequest);
    }

    /// <inheritdoc/>
    public async Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        using var activity = TracingExtensions.StartHandlerActivity("RootsHandler", message.GetType().Name);
        
        try
        {
            activity?.SetTag("roots.operation", "list");
            
            switch (message)
            {
                case JsonRpcRequest<RootsListRequest> listRequest:
                    return await HandleListAsync(cancellationToken);
                    
                default:
                    throw new ArgumentException("Invalid message type", nameof(message));
            }
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }
    }

    private Task<object?> HandleListAsync(CancellationToken cancellationToken)
    {
        EnsureInitialized();

        _logger.LogInformation("Handling roots/list request");

        try
        {
            var roots = _rootRegistry!.Roots;
            
            _logger.LogInformation("Returning {RootCount} roots", roots.Count);
            
            // Add root count to the current activity
            Activity.Current?.SetTag("roots.count", roots.Count);
            
            var response = new RootsListResponse
            {
                Roots = roots.ToList()
            };

            return Task.FromResult<object?>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling roots/list request");
            throw new ProtocolException("Failed to retrieve roots list", ex);
        }
    }

    private void EnsureInitialized()
    {
        // Lazily get the root registry service instance
        if (_rootRegistry == null)
        {
            _rootRegistry = _serviceProvider.GetService<IRootRegistry>() 
                ?? throw new InvalidOperationException("IRootRegistry is not registered");
        }
    }
}