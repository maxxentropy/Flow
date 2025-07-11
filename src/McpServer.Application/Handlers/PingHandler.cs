using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles ping requests for connection health checks.
/// </summary>
public class PingHandler : IMessageHandler
{
    private readonly ILogger<PingHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PingHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PingHandler(ILogger<PingHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanHandle(Type messageType)
    {
        return messageType == typeof(PingRequest);
    }

    /// <inheritdoc/>
    public async Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        if (message is not PingRequest pingRequest)
        {
            throw new ArgumentException("Invalid message type", nameof(message));
        }

        _logger.LogDebug("Received ping request with ID: {RequestId}", pingRequest.Id);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var response = new PongResponse
        {
            Timestamp = now,
            PingTimestamp = pingRequest.Params?.Timestamp
        };

        _logger.LogDebug("Sending pong response with timestamp: {Timestamp}", response.Timestamp);

        return await Task.FromResult(response);
    }
}