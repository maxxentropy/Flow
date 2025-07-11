using System.Text.Json.Serialization;
using McpServer.Domain.Protocol.JsonRpc;

namespace McpServer.Domain.Protocol.Messages;

/// <summary>
/// Metadata that can be included in requests for progress tracking.
/// </summary>
public record RequestMeta
{
    /// <summary>
    /// Gets or sets the progress token for tracking long-running operations.
    /// </summary>
    [JsonPropertyName("progressToken")]
    public string? ProgressToken { get; init; }
}

/// <summary>
/// Extended JSON-RPC request with metadata support.
/// </summary>
/// <typeparam name="T">The parameter type.</typeparam>
public record JsonRpcRequestWithMeta<T> : JsonRpcRequest<T>
{
    /// <summary>
    /// Gets or sets the metadata for progress tracking.
    /// </summary>
    [JsonPropertyName("_meta")]
    public RequestMeta? Meta { get; init; }
}

/// <summary>
/// Extended JSON-RPC request with metadata support (no parameters).
/// </summary>
public record JsonRpcRequestWithMeta : JsonRpcRequest
{
    /// <summary>
    /// Gets or sets the metadata for progress tracking.
    /// </summary>
    [JsonPropertyName("_meta")]
    public RequestMeta? Meta { get; init; }
}

/// <summary>
/// Progress update information.
/// </summary>
public record ProgressUpdate
{
    /// <summary>
    /// Gets the progress token.
    /// </summary>
    [JsonPropertyName("progressToken")]
    public required string ProgressToken { get; init; }
    
    /// <summary>
    /// Gets the progress percentage (0-100).
    /// </summary>
    [JsonPropertyName("progress")]
    public required double Progress { get; init; }
    
    /// <summary>
    /// Gets the total amount of work.
    /// </summary>
    [JsonPropertyName("total")]
    public double? Total { get; init; }
    
    /// <summary>
    /// Gets the current progress message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
    
    /// <summary>
    /// Gets the timestamp of this progress update.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}