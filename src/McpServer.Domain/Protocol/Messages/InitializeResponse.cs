using System.Text.Json.Serialization;

namespace McpServer.Domain.Protocol.Messages;

/// <summary>
/// Response to the initialize request.
/// </summary>
public record InitializeResponse
{
    /// <summary>
    /// Gets the protocol version supported by the server.
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; init; }

    /// <summary>
    /// Gets the server capabilities.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public required ServerCapabilities Capabilities { get; init; }

    /// <summary>
    /// Gets information about the server.
    /// </summary>
    [JsonPropertyName("serverInfo")]
    public required ServerInfo ServerInfo { get; init; }
}

/// <summary>
/// Information about the server.
/// </summary>
public record ServerInfo
{
    /// <summary>
    /// Gets the server name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the server version.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

/// <summary>
/// Server capabilities for feature negotiation.
/// </summary>
public record ServerCapabilities
{
    /// <summary>
    /// Gets the tools capability if the server supports tools.
    /// </summary>
    [JsonPropertyName("tools")]
    public ToolsCapability? Tools { get; init; }

    /// <summary>
    /// Gets the resources capability if the server supports resources.
    /// </summary>
    [JsonPropertyName("resources")]
    public ResourcesCapability? Resources { get; init; }

    /// <summary>
    /// Gets the prompts capability if the server supports prompts.
    /// </summary>
    [JsonPropertyName("prompts")]
    public PromptsCapability? Prompts { get; init; }

    /// <summary>
    /// Gets the logging capability if the server supports logging.
    /// </summary>
    [JsonPropertyName("logging")]
    public LoggingCapability? Logging { get; init; }

    /// <summary>
    /// Gets the roots capability if the server supports roots.
    /// </summary>
    [JsonPropertyName("roots")]
    public RootsCapability? Roots { get; init; }

    /// <summary>
    /// Gets the completion capability if the server supports completion.
    /// </summary>
    [JsonPropertyName("completion")]
    public CompletionCapability? Completion { get; init; }

    /// <summary>
    /// Gets the experimental features supported by the server.
    /// </summary>
    [JsonPropertyName("experimental")]
    public Dictionary<string, object>? Experimental { get; init; }
}

/// <summary>
/// Tools capability.
/// </summary>
public record ToolsCapability
{
    /// <summary>
    /// Gets whether the server supports listing available tools.
    /// </summary>
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; }
}

/// <summary>
/// Resources capability.
/// </summary>
public record ResourcesCapability
{
    /// <summary>
    /// Gets whether the server supports subscribing to resource updates.
    /// </summary>
    [JsonPropertyName("subscribe")]
    public bool Subscribe { get; init; }

    /// <summary>
    /// Gets whether the server supports listing available resources.
    /// </summary>
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; }
}

/// <summary>
/// Prompts capability.
/// </summary>
public record PromptsCapability
{
    /// <summary>
    /// Gets whether the server supports listing available prompts.
    /// </summary>
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; }
}

/// <summary>
/// Logging capability.
/// </summary>
public record LoggingCapability
{
}

/// <summary>
/// Roots capability.
/// </summary>
public record RootsCapability
{
    /// <summary>
    /// Gets whether the server supports notifications when the list of roots changes.
    /// </summary>
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; }
}

/// <summary>
/// Completion capability.
/// </summary>
public record CompletionCapability
{
}