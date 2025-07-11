using System.Collections.Concurrent;
using McpServer.Application.Handlers;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Manages request cancellation tokens and operations.
/// </summary>
public class CancellationManager : ICancellationManager, IDisposable
{
    private readonly ILogger<CancellationManager> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeRequests = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancellationManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public CancellationManager(ILogger<CancellationManager> logger)
    {
        _logger = logger;
        
        // Cleanup completed requests every minute
        _cleanupTimer = new Timer(CleanupCompletedRequests, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <inheritdoc/>
    public void RegisterRequest(string requestId, CancellationTokenSource cancellationTokenSource)
    {
        if (_disposed)
            return;

        _activeRequests.TryAdd(requestId, cancellationTokenSource);
        _logger.LogDebug("Registered cancellation token for request: {RequestId}", requestId);

        // Auto-cleanup when the token is cancelled or completed
        cancellationTokenSource.Token.Register(() =>
        {
            if (_activeRequests.TryRemove(requestId, out var removedSource))
            {
                _logger.LogDebug("Auto-unregistered completed request: {RequestId}", requestId);
            }
        });
    }

    /// <inheritdoc/>
    public async Task<bool> CancelRequestAsync(string requestId, string? reason = null)
    {
        if (_disposed)
            return false;

        if (_activeRequests.TryGetValue(requestId, out var cancellationTokenSource))
        {
            if (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.LogInformation("Cancelling request {RequestId} with reason: {Reason}", requestId, reason ?? "No reason provided");
                
                try
                {
                    await cancellationTokenSource.CancelAsync();
                    return true;
                }
                catch (ObjectDisposedException)
                {
                    // Token source was already disposed
                    _logger.LogDebug("Cancellation token source for request {RequestId} was already disposed", requestId);
                    return false;
                }
            }
            else
            {
                _logger.LogDebug("Request {RequestId} is already cancelled", requestId);
                return false;
            }
        }

        _logger.LogDebug("Request {RequestId} not found for cancellation", requestId);
        return false;
    }

    /// <inheritdoc/>
    public void UnregisterRequest(string requestId)
    {
        if (_disposed)
            return;

        if (_activeRequests.TryRemove(requestId, out var cancellationTokenSource))
        {
            _logger.LogDebug("Unregistered request: {RequestId}", requestId);
            
            // Dispose the cancellation token source if it's not already disposed
            if (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    cancellationTokenSource.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
            }
        }
    }

    /// <inheritdoc/>
    public CancellationToken GetCancellationToken(string requestId)
    {
        if (_disposed)
            return CancellationToken.None;

        return _activeRequests.TryGetValue(requestId, out var cancellationTokenSource) 
            ? cancellationTokenSource.Token 
            : CancellationToken.None;
    }

    /// <summary>
    /// Gets the count of active requests being tracked.
    /// </summary>
    public int ActiveRequestCount => _activeRequests.Count;

    private void CleanupCompletedRequests(object? state)
    {
        if (_disposed)
            return;

        var completedRequests = new List<string>();
        
        foreach (var kvp in _activeRequests)
        {
            var requestId = kvp.Key;
            var cancellationTokenSource = kvp.Value;
            
            // Remove completed or disposed token sources
            if (cancellationTokenSource.Token.IsCancellationRequested || 
                cancellationTokenSource.Token.CanBeCanceled == false)
            {
                completedRequests.Add(requestId);
            }
        }

        foreach (var requestId in completedRequests)
        {
            if (_activeRequests.TryRemove(requestId, out var tokenSource))
            {
                try
                {
                    tokenSource.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
                
                _logger.LogDebug("Cleaned up completed request: {RequestId}", requestId);
            }
        }

        if (completedRequests.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} completed requests", completedRequests.Count);
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
                
                // Cancel and dispose all active requests
                foreach (var kvp in _activeRequests)
                {
                    try
                    {
                        kvp.Value.Cancel();
                        kvp.Value.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed, ignore
                    }
                }
                
                _activeRequests.Clear();
            }
            
            _disposed = true;
        }
    }
}