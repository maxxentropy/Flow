using McpServer.Domain.Protocol.Messages;

namespace McpServer.Application.Services;

/// <summary>
/// Service for handling LLM sampling requests.
/// </summary>
public interface ISamplingService
{
    /// <summary>
    /// Checks if the client supports sampling.
    /// </summary>
    bool IsSamplingSupported { get; }
    
    /// <summary>
    /// Sends a create message request to the client.
    /// </summary>
    /// <param name="request">The create message request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response from the client.</returns>
    Task<CreateMessageResponse> CreateMessageAsync(CreateMessageRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets the client capabilities.
    /// </summary>
    /// <param name="capabilities">The client capabilities.</param>
    void SetClientCapabilities(ClientCapabilities capabilities);
}