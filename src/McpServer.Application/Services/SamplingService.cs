using System.Text.Json;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Default implementation of the sampling service.
/// </summary>
public class SamplingService : ISamplingService
{
    private readonly ILogger<SamplingService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private ITransport? _transport;
    private ClientCapabilities? _clientCapabilities;
    private int _nextRequestId = 1;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public SamplingService(ILogger<SamplingService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingService"/> class with a transport.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="transport">The transport to use for sending requests.</param>
    public SamplingService(ILogger<SamplingService> logger, ITransport transport) : this(logger)
    {
        _transport = transport;
    }
    
    /// <inheritdoc/>
    public bool IsSamplingSupported => _clientCapabilities?.Sampling != null;
    
    /// <inheritdoc/>
    public async Task<CreateMessageResponse> CreateMessageAsync(CreateMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsSamplingSupported)
        {
            _logger.LogWarning("Sampling is not supported by the client");
            throw new ProtocolException("Sampling is not supported by the client");
        }
        
        if (_transport == null)
        {
            _logger.LogError("Cannot send sampling request: No transport available");
            throw new InvalidOperationException("No transport available");
        }
        
        var requestId = _nextRequestId++;
        var jsonRpcRequest = new JsonRpcRequest<CreateMessageRequest>
        {
            Jsonrpc = "2.0",
            Id = requestId,
            Method = "sampling/createMessage",
            Params = request
        };
        
        _logger.LogInformation("Sending createMessage request with {MessageCount} messages", request.Messages.Count);
        
        try
        {
            // Create a task completion source to wait for the response
            var tcs = new TaskCompletionSource<CreateMessageResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            // Store the completion source for later resolution
            lock (_pendingRequests)
            {
                _pendingRequests[requestId] = tcs;
            }
            
            // Send the request
            await _transport.SendMessageAsync(jsonRpcRequest, cancellationToken);
            
            // Wait for the response with a timeout
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(TimeSpan.FromMinutes(5)); // 5 minute timeout for LLM responses
                
                try
                {
                    return await tcs.Task.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    lock (_pendingRequests)
                    {
                        _pendingRequests.Remove(requestId);
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Create message request was cancelled", cancellationToken);
                    }
                    else
                    {
                        throw new TimeoutException("Create message request timed out after 5 minutes");
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not ProtocolException && ex is not OperationCanceledException && ex is not TimeoutException)
        {
            _logger.LogError(ex, "Error sending create message request");
            throw new ProtocolException("Error sending create message request", ex);
        }
    }
    
    /// <inheritdoc/>
    public void SetClientCapabilities(ClientCapabilities capabilities)
    {
        _clientCapabilities = capabilities;
        _logger.LogInformation("Client capabilities updated. Sampling supported: {Supported}", IsSamplingSupported);
    }
    
    /// <summary>
    /// Sets the transport for sending requests.
    /// </summary>
    /// <param name="transport">The transport.</param>
    public void SetTransport(ITransport transport)
    {
        _transport = transport;
        
        // Subscribe to message received events to handle responses
        _transport.MessageReceived += OnMessageReceived;
    }
    
    private readonly Dictionary<int, TaskCompletionSource<CreateMessageResponse>> _pendingRequests = new();
    
    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(e.Message);
            var root = jsonDocument.RootElement;
            
            // Check if it's a response to our request
            if (root.TryGetProperty("id", out var idElement) && 
                idElement.TryGetInt32(out var id) &&
                root.TryGetProperty("result", out var resultElement))
            {
                TaskCompletionSource<CreateMessageResponse>? tcs;
                lock (_pendingRequests)
                {
                    if (!_pendingRequests.TryGetValue(id, out tcs))
                    {
                        return; // Not our request
                    }
                    _pendingRequests.Remove(id);
                }
                
                // Parse the response
                try
                {
                    var response = JsonSerializer.Deserialize<CreateMessageResponse>(resultElement.GetRawText(), _jsonOptions);
                    if (response != null)
                    {
                        tcs.SetResult(response);
                    }
                    else
                    {
                        tcs.SetException(new ProtocolException("Invalid create message response"));
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(new ProtocolException("Failed to parse create message response", ex));
                }
            }
            else if (root.TryGetProperty("id", out idElement) && 
                     idElement.TryGetInt32(out id) &&
                     root.TryGetProperty("error", out var errorElement))
            {
                TaskCompletionSource<CreateMessageResponse>? tcs;
                lock (_pendingRequests)
                {
                    if (!_pendingRequests.TryGetValue(id, out tcs))
                    {
                        return; // Not our request
                    }
                    _pendingRequests.Remove(id);
                }
                
                // Parse the error
                var error = JsonSerializer.Deserialize<JsonRpcError>(errorElement.GetRawText(), _jsonOptions);
                tcs.SetException(new ProtocolException($"Create message request failed: {error?.Message ?? "Unknown error"}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message in sampling service");
        }
    }
}