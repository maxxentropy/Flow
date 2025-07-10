namespace McpServer.Domain.Protocol.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 notification.
/// </summary>
public record JsonRpcNotification
{
    /// <summary>
    /// Gets the JSON-RPC version. Must be "2.0".
    /// </summary>
    public required string Jsonrpc { get; init; } = "2.0";

    /// <summary>
    /// Gets the method name to be invoked.
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Gets the method parameters.
    /// </summary>
    public object? Params { get; init; }
}