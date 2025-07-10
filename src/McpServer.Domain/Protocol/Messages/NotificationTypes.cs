using System.Text.Json.Serialization;

namespace McpServer.Domain.Protocol.Messages;

/// <summary>
/// Base class for all MCP notifications.
/// </summary>
public abstract record Notification
{
    /// <summary>
    /// Gets the JSON-RPC version.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; } = "2.0";
    
    /// <summary>
    /// Gets the notification method.
    /// </summary>
    [JsonPropertyName("method")]
    public abstract string Method { get; }
    
    /// <summary>
    /// Gets the notification parameters.
    /// </summary>
    [JsonPropertyName("params")]
    public abstract object? Params { get; }
}

/// <summary>
/// Notification sent when resources are updated.
/// </summary>
public record ResourcesUpdatedNotification : Notification
{
    /// <inheritdoc/>
    public override string Method => "notifications/resources/updated";
    
    /// <inheritdoc/>
    public override object? Params => null;
}

/// <summary>
/// Notification sent when tools are updated.
/// </summary>
public record ToolsUpdatedNotification : Notification
{
    /// <inheritdoc/>
    public override string Method => "notifications/tools/updated";
    
    /// <inheritdoc/>
    public override object? Params => null;
}

/// <summary>
/// Notification sent when prompts are updated.
/// </summary>
public record PromptsUpdatedNotification : Notification
{
    /// <inheritdoc/>
    public override string Method => "notifications/prompts/updated";
    
    /// <inheritdoc/>
    public override object? Params => null;
}

/// <summary>
/// Parameters for progress notifications.
/// </summary>
public record ProgressNotificationParams
{
    /// <summary>
    /// Gets or sets the progress token.
    /// </summary>
    [JsonPropertyName("progressToken")]
    public required string ProgressToken { get; init; }
    
    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    [JsonPropertyName("progress")]
    public required double Progress { get; init; }
    
    /// <summary>
    /// Gets or sets the total units of work.
    /// </summary>
    [JsonPropertyName("total")]
    public double? Total { get; init; }
    
    /// <summary>
    /// Gets or sets the optional progress message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
/// Notification sent to report progress.
/// </summary>
public record ProgressNotification : Notification
{
    /// <inheritdoc/>
    public override string Method => "notifications/progress";
    
    /// <inheritdoc/>
    public override object? Params => ProgressParams;
    
    /// <summary>
    /// Gets or sets the progress parameters.
    /// </summary>
    [JsonIgnore]
    public required ProgressNotificationParams ProgressParams { get; init; }
}

/// <summary>
/// Parameters for cancellation notifications.
/// </summary>
public record CancelledNotificationParams
{
    /// <summary>
    /// Gets or sets the request ID that was cancelled.
    /// </summary>
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }
    
    /// <summary>
    /// Gets or sets the cancellation reason.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Notification sent when an operation is cancelled.
/// </summary>
public record CancelledNotification : Notification
{
    /// <inheritdoc/>
    public override string Method => "notifications/cancelled";
    
    /// <inheritdoc/>
    public override object? Params => CancelledParams;
    
    /// <summary>
    /// Gets or sets the cancellation parameters.
    /// </summary>
    [JsonIgnore]
    public required CancelledNotificationParams CancelledParams { get; init; }
}

/// <summary>
/// Notification sent when a specific resource is updated.
/// </summary>
public record ResourceUpdatedNotification : Notification
{
    /// <inheritdoc/>
    public override string Method => "notifications/resources/updated";
    
    /// <inheritdoc/>
    public override object? Params => ResourceParams;
    
    /// <summary>
    /// Gets or sets the resource update parameters.
    /// </summary>
    [JsonIgnore]
    public required ResourceUpdatedParams ResourceParams { get; init; }
}

/// <summary>
/// Parameters for resource update notifications.
/// </summary>
public record ResourceUpdatedParams
{
    /// <summary>
    /// Gets or sets the URI of the updated resource.
    /// </summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }
}