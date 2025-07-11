using System.Collections.Concurrent;
using System.Text.Json;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Service for managing heartbeat (ping/pong) functionality across transports.
/// </summary>
public class HeartbeatService : IHeartbeatService, IDisposable
{
    private readonly ILogger<HeartbeatService> _logger;
    private readonly ConcurrentDictionary<string, TransportHeartbeat> _heartbeats = new();
    private readonly Timer _heartbeatTimer;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeartbeatService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public HeartbeatService(ILogger<HeartbeatService> logger)
    {
        _logger = logger;
        _heartbeatTimer = new Timer(SendHeartbeats, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <inheritdoc/>
    public void RegisterTransport(ITransport transport, HeartbeatOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var transportId = transport.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var heartbeat = new TransportHeartbeat(transport, options);
        
        _heartbeats.TryAdd(transportId, heartbeat);
        
        transport.Disconnected += (sender, args) =>
        {
            _heartbeats.TryRemove(transportId, out _);
        };

        _logger.LogDebug("Registered transport for heartbeat monitoring: {TransportId}", transportId);
    }

    /// <inheritdoc/>
    public void UnregisterTransport(ITransport transport)
    {
        var transportId = transport.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_heartbeats.TryRemove(transportId, out var heartbeat))
        {
            _logger.LogDebug("Unregistered transport from heartbeat monitoring: {TransportId}", transportId);
        }
    }

    /// <inheritdoc/>
    public void RecordPong(ITransport transport, long timestamp)
    {
        var transportId = transport.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_heartbeats.TryGetValue(transportId, out var heartbeat))
        {
            lock (_lock)
            {
                heartbeat.LastPongReceived = DateTimeOffset.UtcNow;
                heartbeat.LastPongTimestamp = timestamp;
            }
            
            _logger.LogDebug("Recorded pong from transport {TransportId} at {Timestamp}", transportId, timestamp);
        }
    }

    private async void SendHeartbeats(object? state)
    {
        if (_disposed)
            return;

        var now = DateTimeOffset.UtcNow;
        
        foreach (var kvp in _heartbeats)
        {
            var transportId = kvp.Key;
            var heartbeat = kvp.Value;
            
            try
            {
                // Check if heartbeat is enabled for this transport
                if (!heartbeat.Options.Enabled)
                    continue;

                // Check if it's time to send a ping
                if (now - heartbeat.LastPingSent >= heartbeat.Options.Interval)
                {
                    await SendPing(transportId, heartbeat);
                }

                // Check for timeout
                if (heartbeat.Options.Timeout > TimeSpan.Zero &&
                    now - heartbeat.LastPongReceived > heartbeat.Options.Timeout)
                {
                    _logger.LogWarning("Transport {TransportId} heartbeat timeout detected", transportId);
                    // Could trigger disconnect here if desired
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing heartbeat for transport {TransportId}", transportId);
            }
        }
    }

    private async Task SendPing(string transportId, TransportHeartbeat heartbeat)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var pingRequest = new PingRequest
            {
                Id = Guid.NewGuid().ToString(),
                Params = new PingParams { Timestamp = timestamp }
            };

            await heartbeat.Transport.SendMessageAsync(pingRequest);
            
            lock (_lock)
            {
                heartbeat.LastPingSent = DateTimeOffset.UtcNow;
                heartbeat.LastPingTimestamp = timestamp;
            }

            _logger.LogDebug("Sent ping to transport {TransportId} with timestamp {Timestamp}", transportId, timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ping to transport {TransportId}", transportId);
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
                _heartbeatTimer?.Dispose();
                _heartbeats.Clear();
            }
            
            _disposed = true;
        }
    }

    private class TransportHeartbeat
    {
        public TransportHeartbeat(ITransport transport, HeartbeatOptions options)
        {
            Transport = transport;
            Options = options;
            LastPingSent = DateTimeOffset.UtcNow;
            LastPongReceived = DateTimeOffset.UtcNow;
        }

        public ITransport Transport { get; }
        public HeartbeatOptions Options { get; }
        public DateTimeOffset LastPingSent { get; set; }
        public DateTimeOffset LastPongReceived { get; set; }
        public long? LastPingTimestamp { get; set; }
        public long? LastPongTimestamp { get; set; }
    }
}

/// <summary>
/// Interface for heartbeat service.
/// </summary>
public interface IHeartbeatService
{
    /// <summary>
    /// Registers a transport for heartbeat monitoring.
    /// </summary>
    /// <param name="transport">The transport to register.</param>
    /// <param name="options">The heartbeat options.</param>
    void RegisterTransport(ITransport transport, HeartbeatOptions options);

    /// <summary>
    /// Unregisters a transport from heartbeat monitoring.
    /// </summary>
    /// <param name="transport">The transport to unregister.</param>
    void UnregisterTransport(ITransport transport);

    /// <summary>
    /// Records a pong response from a transport.
    /// </summary>
    /// <param name="transport">The transport that sent the pong.</param>
    /// <param name="timestamp">The timestamp from the pong response.</param>
    void RecordPong(ITransport transport, long timestamp);
}

/// <summary>
/// Options for heartbeat configuration.
/// </summary>
public class HeartbeatOptions
{
    /// <summary>
    /// Gets or sets whether heartbeat is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval between ping messages.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout for receiving pong responses.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}