namespace McpServer.Domain.Protocol.Messages;

/// <summary>
/// Request to initialize the MCP session.
/// </summary>
public record InitializeRequest
{
    /// <summary>
    /// Gets the protocol version supported by the client.
    /// </summary>
    public required string ProtocolVersion { get; init; }

    /// <summary>
    /// Gets the client capabilities.
    /// </summary>
    public required ClientCapabilities Capabilities { get; init; }

    /// <summary>
    /// Gets information about the client.
    /// </summary>
    public required ClientInfo ClientInfo { get; init; }
}

/// <summary>
/// Information about the client.
/// </summary>
public record ClientInfo
{
    /// <summary>
    /// Gets the client name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the client version.
    /// </summary>
    public required string Version { get; init; }
}

/// <summary>
/// Client capabilities for feature negotiation.
/// </summary>
public record ClientCapabilities
{
    /// <summary>
    /// Gets the experimental features supported by the client.
    /// </summary>
    public Dictionary<string, object>? Experimental { get; init; }

    /// <summary>
    /// Gets the sampling capability.
    /// </summary>
    public object? Sampling { get; init; }
}