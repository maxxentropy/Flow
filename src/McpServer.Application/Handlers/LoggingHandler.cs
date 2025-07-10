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
/// Handles logging-related requests.
/// </summary>
public class LoggingHandler : IMessageHandler
{
    private readonly ILogger<LoggingHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private ILoggingService? _loggingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public LoggingHandler(ILogger<LoggingHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public bool CanHandle(Type messageType)
    {
        return messageType == typeof(Messages.LoggingSetLevelRequest);
    }

    /// <inheritdoc/>
    public async Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        using var activity = TracingExtensions.StartHandlerActivity("LoggingHandler", message.GetType().Name);
        
        try
        {
            switch (message)
            {
                case JsonRpcRequest<Messages.LoggingSetLevelRequest> setLevelRequest:
                    activity?.SetTag("logging.operation", "setLevel");
                    if (setLevelRequest.Params == null)
                    {
                        throw new ProtocolException("Logging setLevel request parameters cannot be null");
                    }
                    activity?.SetTag("logging.level", setLevelRequest.Params.Level);
                    return await HandleSetLevelAsync(setLevelRequest.Params, cancellationToken);
                    
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

    private Task<object?> HandleSetLevelAsync(Messages.LoggingSetLevelRequest request, CancellationToken cancellationToken)
    {
        EnsureInitialized();

        _logger.LogInformation("Setting log level to: {LogLevel}", request.Level);

        try
        {
            _loggingService!.SetLogLevel(request.Level);
            
            _logger.LogInformation("Log level successfully set to: {LogLevel}", request.Level);
            
            // Return empty result (void response)
            return Task.FromResult<object?>(new { });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid log level: {LogLevel}", request.Level);
            throw new ProtocolException($"Invalid log level: {request.Level}. Valid levels are: debug, info, notice, warning, error, critical, alert, emergency", ex);
        }
    }

    private void EnsureInitialized()
    {
        // Lazily get the logging service instance
        if (_loggingService == null)
        {
            _loggingService = _serviceProvider.GetService<ILoggingService>() 
                ?? throw new InvalidOperationException("ILoggingService is not registered");
        }
    }
}