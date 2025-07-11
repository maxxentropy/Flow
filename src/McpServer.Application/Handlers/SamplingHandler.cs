using McpServer.Application.Services;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles sampling-related requests.
/// </summary>
public class SamplingHandler : IMessageHandler
{
    private readonly ILogger<SamplingHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private ISamplingService? _samplingService;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public SamplingHandler(ILogger<SamplingHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    /// <inheritdoc/>
    public bool CanHandle(Type messageType) => messageType == typeof(CreateMessageRequest);
    
    /// <inheritdoc/>
    public async Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        if (message is not JsonRpcRequest<CreateMessageRequest> request)
        {
            throw new ArgumentException("Invalid message type", nameof(message));
        }
        
        if (request.Params == null)
        {
            throw new ProtocolException("Create message request parameters cannot be null");
        }
        
        _logger.LogDebug("Handling sampling create message request: {Messages} messages", 
            request.Params.Messages.Count);
        
        // Get the sampling service
        _samplingService ??= _serviceProvider.GetService<ISamplingService>();
        
        if (_samplingService == null)
        {
            throw new ProtocolException("Sampling is not supported by this server");
        }
        
        try
        {
            // Validate the request
            if (request.Params.Messages.Count == 0)
            {
                throw new ArgumentException("At least one message is required");
            }
            
            // Create the sampling message
            var result = await _samplingService.CreateMessageAsync(request.Params, cancellationToken);
            
            _logger.LogInformation("Successfully created sampling message with model {Model}", result.Model ?? "Unknown");
            
            return result;
        }
        catch (Exception ex) when (ex is not McpException)
        {
            _logger.LogError(ex, "Error creating sampling message");
            throw new ProtocolException("Failed to create sampling message", ex);
        }
    }
}