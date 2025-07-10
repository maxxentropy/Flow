namespace McpServer.Infrastructure.Transport;

/// <summary>
/// Stdio transport implementation for MCP communication.
/// </summary>
public class StdioTransport : ITransport, IDisposable
{
    private readonly ILogger<StdioTransport> _logger;
    private readonly IOptions<StdioTransportOptions> _options;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _readTask;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioTransport"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The transport options.</param>
    public StdioTransport(ILogger<StdioTransport> logger, IOptions<StdioTransportOptions> options)
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

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
        {
            throw new InvalidOperationException("Transport is already started");
        }

        _logger.LogInformation("Starting stdio transport");

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isConnected = true;

        // Start reading from stdin
        _readTask = Task.Run(() => ReadLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

        _logger.LogInformation("Stdio transport started");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            return;
        }

        _logger.LogInformation("Stopping stdio transport");

        _isConnected = false;
        _cancellationTokenSource?.Cancel();

        if (_readTask != null)
        {
            try
            {
                await _readTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _logger.LogInformation("Stdio transport stopped");
    }

    /// <inheritdoc/>
    public async Task SendMessageAsync(object message, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("Transport is not connected");
        }

        await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");

            using var stdout = Console.OpenStandardOutput();
            await stdout.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stdout.FlushAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Sent message: {Message}", json);
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

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[_options.Value.BufferSize];
        var messageBuilder = new StringBuilder();

        try
        {
            using var stdin = Console.OpenStandardInput();
            
            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                try
                {
                    // Read from stdin
                    var bytesRead = await stdin.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    
                    if (bytesRead == 0)
                    {
                        // EOF reached
                        _logger.LogInformation("Stdin closed, disconnecting");
                        break;
                    }

                    var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    // Process each character
                    foreach (var ch in text)
                    {
                        if (ch == '\n')
                        {
                            // Complete message received
                            var message = messageBuilder.ToString().Trim();
                            if (!string.IsNullOrEmpty(message))
                            {
                                _logger.LogDebug("Received message: {Message}", message);
                                OnMessageReceived(message);
                            }
                            messageBuilder.Clear();
                        }
                        else if (ch != '\r')
                        {
                            messageBuilder.Append(ch);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading from stdin");
                    OnDisconnected("Read error", ex);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in read loop");
            OnDisconnected("Fatal error", ex);
        }
        finally
        {
            _isConnected = false;
            OnDisconnected("Transport closed", null);
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
/// Configuration options for the stdio transport.
/// </summary>
public class StdioTransportOptions
{
    /// <summary>
    /// Gets or sets the buffer size for reading from stdin.
    /// </summary>
    public int BufferSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the timeout for read operations.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}