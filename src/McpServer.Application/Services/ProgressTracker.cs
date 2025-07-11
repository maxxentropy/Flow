using System.Collections.Concurrent;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using McpServer.Application.Services;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Service for tracking progress of long-running operations.
/// </summary>
public class ProgressTracker : IProgressTracker, IDisposable
{
    private readonly ILogger<ProgressTracker> _logger;
    private readonly INotificationService _notificationService;
    private readonly ConcurrentDictionary<string, ProgressOperation> _activeOperations = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressTracker"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="notificationService">The notification service.</param>
    public ProgressTracker(ILogger<ProgressTracker> logger, INotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;
        
        // Cleanup completed operations every 5 minutes
        _cleanupTimer = new Timer(CleanupCompletedOperations, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <inheritdoc/>
    public string StartOperation(string? customToken = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var token = customToken ?? Guid.NewGuid().ToString();
        var operation = new ProgressOperation(token, DateTimeOffset.UtcNow);
        
        _activeOperations.TryAdd(token, operation);
        _logger.LogDebug("Started progress tracking for operation: {ProgressToken}", token);
        
        return token;
    }

    /// <inheritdoc/>
    public async Task UpdateProgressAsync(string progressToken, double progress, string? message = null, double? total = null)
    {
        if (_disposed || !_activeOperations.TryGetValue(progressToken, out var operation))
        {
            _logger.LogWarning("Attempted to update progress for unknown operation: {ProgressToken}", progressToken);
            return;
        }

        var progressUpdate = new ProgressUpdate
        {
            ProgressToken = progressToken,
            Progress = Math.Clamp(progress, 0, 100),
            Message = message,
            Total = total
        };

        operation.LastUpdate = DateTimeOffset.UtcNow;
        operation.Progress = progressUpdate.Progress;
        operation.Message = message;

        _logger.LogDebug("Updated progress for operation {ProgressToken}: {Progress}% - {Message}", 
            progressToken, progressUpdate.Progress, message ?? "No message");

        // Send progress notification
        try
        {
            var notification = new ProgressNotification
            {
                ProgressParams = new ProgressNotificationParams
                {
                    ProgressToken = progressToken,
                    Progress = progressUpdate.Progress,
                    Total = total,
                    Message = message
                }
            };

            await _notificationService.SendNotificationAsync(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send progress notification for operation: {ProgressToken}", progressToken);
        }
    }

    /// <inheritdoc/>
    public async Task CompleteOperationAsync(string progressToken, string? finalMessage = null)
    {
        if (_disposed)
            return;

        if (_activeOperations.TryRemove(progressToken, out var operation))
        {
            _logger.LogDebug("Completed operation: {ProgressToken}", progressToken);
            
            // Send final progress update
            await UpdateProgressAsync(progressToken, 100, finalMessage ?? "Operation completed");
        }
    }

    /// <inheritdoc/>
    public async Task FailOperationAsync(string progressToken, string errorMessage, Exception? exception = null)
    {
        if (_disposed)
            return;

        if (_activeOperations.TryRemove(progressToken, out var operation))
        {
            _logger.LogError(exception, "Operation failed: {ProgressToken} - {ErrorMessage}", progressToken, errorMessage);
            
            // Send final progress update with error
            await UpdateProgressAsync(progressToken, operation.Progress, $"Operation failed: {errorMessage}");
        }
    }

    /// <inheritdoc/>
    public bool IsOperationActive(string progressToken)
    {
        return !_disposed && _activeOperations.ContainsKey(progressToken);
    }

    /// <inheritdoc/>
    public OperationProgress? GetOperationStatus(string progressToken)
    {
        if (_disposed || !_activeOperations.TryGetValue(progressToken, out var operation))
            return null;

        return new OperationProgress
        {
            ProgressToken = progressToken,
            Progress = operation.Progress,
            Message = operation.Message,
            StartTime = operation.StartTime,
            LastUpdate = operation.LastUpdate
        };
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetActiveOperations()
    {
        return _disposed ? Enumerable.Empty<string>() : _activeOperations.Keys.ToList();
    }

    private void CleanupCompletedOperations(object? state)
    {
        if (_disposed)
            return;

        var cutoff = DateTimeOffset.UtcNow.AddHours(-1); // Remove operations older than 1 hour
        var expiredOperations = new List<string>();

        foreach (var kvp in _activeOperations)
        {
            if (kvp.Value.LastUpdate < cutoff)
            {
                expiredOperations.Add(kvp.Key);
            }
        }

        foreach (var token in expiredOperations)
        {
            if (_activeOperations.TryRemove(token, out _))
            {
                _logger.LogDebug("Cleaned up expired operation: {ProgressToken}", token);
            }
        }

        if (expiredOperations.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired operations", expiredOperations.Count);
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cleanupTimer?.Dispose();
                _activeOperations.Clear();
            }
            
            _disposed = true;
        }
    }

    private class ProgressOperation
    {
        public ProgressOperation(string token, DateTimeOffset startTime)
        {
            Token = token;
            StartTime = startTime;
            LastUpdate = startTime;
        }

        public string Token { get; }
        public DateTimeOffset StartTime { get; }
        public DateTimeOffset LastUpdate { get; set; }
        public double Progress { get; set; }
        public string? Message { get; set; }
    }
}

/// <summary>
/// Interface for progress tracking.
/// </summary>
public interface IProgressTracker
{
    /// <summary>
    /// Starts tracking a new operation.
    /// </summary>
    /// <param name="customToken">Optional custom token, otherwise generates a new one.</param>
    /// <returns>The progress token for the operation.</returns>
    string StartOperation(string? customToken = null);

    /// <summary>
    /// Updates the progress of an operation.
    /// </summary>
    /// <param name="progressToken">The progress token.</param>
    /// <param name="progress">The progress percentage (0-100).</param>
    /// <param name="message">Optional progress message.</param>
    /// <param name="total">Optional total amount of work.</param>
    Task UpdateProgressAsync(string progressToken, double progress, string? message = null, double? total = null);

    /// <summary>
    /// Marks an operation as completed.
    /// </summary>
    /// <param name="progressToken">The progress token.</param>
    /// <param name="finalMessage">Optional final message.</param>
    Task CompleteOperationAsync(string progressToken, string? finalMessage = null);

    /// <summary>
    /// Marks an operation as failed.
    /// </summary>
    /// <param name="progressToken">The progress token.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="exception">Optional exception details.</param>
    Task FailOperationAsync(string progressToken, string errorMessage, Exception? exception = null);

    /// <summary>
    /// Checks if an operation is currently active.
    /// </summary>
    /// <param name="progressToken">The progress token.</param>
    /// <returns>True if the operation is active.</returns>
    bool IsOperationActive(string progressToken);

    /// <summary>
    /// Gets the current status of an operation.
    /// </summary>
    /// <param name="progressToken">The progress token.</param>
    /// <returns>The operation progress or null if not found.</returns>
    OperationProgress? GetOperationStatus(string progressToken);

    /// <summary>
    /// Gets all active operation tokens.
    /// </summary>
    /// <returns>The list of active operation tokens.</returns>
    IEnumerable<string> GetActiveOperations();
}

/// <summary>
/// Information about an operation's progress.
/// </summary>
public record OperationProgress
{
    /// <summary>
    /// Gets the progress token.
    /// </summary>
    public required string ProgressToken { get; init; }
    
    /// <summary>
    /// Gets the current progress percentage.
    /// </summary>
    public required double Progress { get; init; }
    
    /// <summary>
    /// Gets the current progress message.
    /// </summary>
    public string? Message { get; init; }
    
    /// <summary>
    /// Gets when the operation started.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }
    
    /// <summary>
    /// Gets when the operation was last updated.
    /// </summary>
    public required DateTimeOffset LastUpdate { get; init; }
}