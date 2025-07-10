using System.Text.Json.Serialization;

namespace McpServer.Domain.Protocol.Messages;

/// <summary>
/// Response to the roots/list request.
/// </summary>
public record RootsListResponse
{
    /// <summary>
    /// Gets the list of roots available to the server.
    /// </summary>
    [JsonPropertyName("roots")]
    public required List<Root> Roots { get; init; }
}

/// <summary>
/// Represents a filesystem or resource root that defines server boundaries.
/// </summary>
public record Root
{
    /// <summary>
    /// Gets the URI of the root (e.g., file:///path/to/directory).
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    /// <summary>
    /// Gets the human-readable name for this root.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>
/// Notification to inform the server that the list of roots has changed.
/// </summary>
public record RootsListChangedNotification : Notification
{
    /// <inheritdoc/>
    public override string Method => "notifications/roots/list_changed";
    
    /// <inheritdoc/>
    public override object? Params => null;
}