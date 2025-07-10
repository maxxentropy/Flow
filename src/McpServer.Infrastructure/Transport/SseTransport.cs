using Microsoft.AspNetCore.Http;

namespace McpServer.Infrastructure.Transport;

/// <summary>
/// Server-Sent Events (SSE) transport implementation for MCP communication.
/// </summary>
public class SseTransport : ITransport, IDisposable
{
    private readonly ILogger<SseTransport> _logger;
    private readonly IOptions<SseTransportOptions> _options;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private HttpContext? _httpContext;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseTransport"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The transport options.</param>
    public SseTransport(ILogger<SseTransport> logger, IOptions<SseTransportOptions> options)
    {
        _logger = logger;
        _options = options;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc/>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <inheritdoc/>
    public event EventHandler<DisconnectedEventArgs>? Disconnected;

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Handles an SSE connection from an HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task HandleConnectionAsync(HttpContext context, CancellationToken cancellationToken)
    {
        _httpContext = context;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Validate API key if configured
            if (!string.IsNullOrEmpty(_options.Value.ApiKey))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader == null || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal) || 
                    authHeader.Substring(7) != _options.Value.ApiKey)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized", cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            // Set SSE headers
            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";
            context.Response.Headers["X-Accel-Buffering"] = "no"; // Disable Nginx buffering

            // CORS is handled by middleware, no need to set headers here

            await StartAsync(_cancellationTokenSource.Token).ConfigureAwait(false);

            // Send initial ping
            await SendPingAsync(cancellationToken).ConfigureAwait(false);

            // Start reading messages from request body
            await ReadMessagesAsync(context, _cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSE connection cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling SSE connection");
            OnDisconnected("Connection error", ex);
        }
        finally
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
        {
            throw new InvalidOperationException("Transport is already started");
        }

        _logger.LogInformation("Starting SSE transport");
        _isConnected = true;
        _logger.LogInformation("SSE transport started");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Stopping SSE transport");
        _isConnected = false;
        _cancellationTokenSource?.Cancel();
        _logger.LogInformation("SSE transport stopped");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _httpContext?.Response == null)
        {
            throw new InvalidOperationException("Transport is not connected");
        }

        await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            var data = $"data: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(data);

            await _httpContext.Response.Body.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Sent SSE message: {Message}", json);
        }
        finally
        {
            _writeSemaphore.Release();
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
                StopAsync().GetAwaiter().GetResult();
                _writeSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();
            }

            _disposed = true;
        }
    }

    private async Task ReadMessagesAsync(HttpContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var messageBuilder = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                
                if (line == null)
                {
                    // EOF reached
                    _logger.LogInformation("SSE client disconnected");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    // Empty line indicates end of message
                    var message = messageBuilder.ToString().Trim();
                    if (!string.IsNullOrEmpty(message))
                    {
                        _logger.LogDebug("Received SSE message: {Message}", message);
                        OnMessageReceived(message);
                    }
                    messageBuilder.Clear();
                }
                else
                {
                    messageBuilder.AppendLine(line);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading SSE messages");
            throw;
        }
    }

    private async Task SendPingAsync(CancellationToken cancellationToken)
    {
        if (_httpContext?.Response == null)
        {
            return;
        }

        try
        {
            var ping = ": ping\n\n";
            var bytes = Encoding.UTF8.GetBytes(ping);
            await _httpContext.Response.Body.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ping");
        }
    }

    private void OnMessageReceived(string message)
    {
        try
        {
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message received handler");
        }
    }

    private void OnDisconnected(string? reason, Exception? exception)
    {
        try
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(reason, exception));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in disconnected handler");
        }
    }
}

/// <summary>
/// Configuration options for the SSE transport.
/// </summary>
public class SseTransportOptions
{
    /// <summary>
    /// Gets or sets whether the SSE transport is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the path for the SSE endpoint.
    /// </summary>
    public string Path { get; set; } = "/sse";

    /// <summary>
    /// Gets or sets the allowed origins for CORS.
    /// </summary>
    public string[]? AllowedOrigins { get; set; }

    /// <summary>
    /// Gets or sets whether HTTPS is required.
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the API key for authentication.
    /// </summary>
    public string? ApiKey { get; set; }
}