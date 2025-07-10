using System.Text.Json.Serialization;

namespace McpServer.Domain.Protocol.Messages;

/// <summary>
/// Response to tools/list request.
/// </summary>
public record ToolsListResponse
{
    /// <summary>
    /// Gets the list of available tools.
    /// </summary>
    [JsonPropertyName("tools")]
    public required IReadOnlyList<ToolInfo> Tools { get; init; }
}

/// <summary>
/// Information about a tool.
/// </summary>
public record ToolInfo
{
    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    /// <summary>
    /// Gets the description of the tool.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }
    
    /// <summary>
    /// Gets the JSON schema for the tool's input parameters.
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public required object InputSchema { get; init; }
}