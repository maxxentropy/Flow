using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Handlers;

/// <summary>
/// Handles cancel requests for ongoing operations.
/// </summary>
public class CancelHandler : IMessageHandler
{
    private readonly ILogger<CancelHandler> _logger;
    private readonly ICancellationManager _cancellationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationManager">The cancellation manager.</param>
    public CancelHandler(ILogger<CancelHandler> logger, ICancellationManager cancellationManager)
    {
        _logger = logger;
        _cancellationManager = cancellationManager;
    }

    /// <inheritdoc/>
    public bool CanHandle(Type messageType)
    {
        return messageType == typeof(CancelRequest);
    }

    /// <inheritdoc/>
    public async Task<object?> HandleMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        if (message is not CancelRequest cancelRequest)
        {
            throw new ArgumentException("Invalid message type", nameof(message));
        }

        _logger.LogDebug("Received cancel request for operation: {RequestId}", cancelRequest.Params.RequestId);

        try
        {
            var requestIdString = cancelRequest.Params.RequestId.ToString();
            if (string.IsNullOrEmpty(requestIdString))
            {
                _logger.LogWarning("Cancel request has invalid or empty request ID");
                return new { success = false, error = "Invalid request ID" };
            }

            var cancelled = await _cancellationManager.CancelRequestAsync(requestIdString, cancelRequest.Params.Reason);
            
            if (cancelled)
            {
                _logger.LogInformation("Successfully cancelled operation: {RequestId}", requestIdString);
                return new { success = true };
            }
            else
            {
                _logger.LogWarning("Could not cancel operation: {RequestId} (not found or already completed)", requestIdString);
                return new { success = false, error = "Request not found or already completed" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling operation: {RequestId}", cancelRequest.Params.RequestId);
            return new { success = false, error = "Internal error" };
        }
    }
}

/// <summary>
/// Interface for managing request cancellation.
/// </summary>
public interface ICancellationManager
{
    /// <summary>
    /// Registers a cancellation token for a request.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="cancellationTokenSource">The cancellation token source.</param>
    void RegisterRequest(string requestId, CancellationTokenSource cancellationTokenSource);

    /// <summary>
    /// Cancels a request by ID.
    /// </summary>
    /// <param name="requestId">The request ID to cancel.</param>
    /// <param name="reason">The reason for cancellation.</param>
    /// <returns>True if the request was cancelled, false if not found.</returns>
    Task<bool> CancelRequestAsync(string requestId, string? reason = null);

    /// <summary>
    /// Unregisters a request when it completes.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    void UnregisterRequest(string requestId);

    /// <summary>
    /// Gets the cancellation token for a request.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <returns>The cancellation token if found, otherwise the default cancellation token.</returns>
    CancellationToken GetCancellationToken(string requestId);
}