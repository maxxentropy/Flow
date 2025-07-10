namespace McpServer.Domain.Protocol.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 response.
/// </summary>
public record JsonRpcResponse
{
    /// <summary>
    /// Gets the JSON-RPC version. Must be "2.0".
    /// </summary>
    public required string Jsonrpc { get; init; } = "2.0";

    /// <summary>
    /// Gets the result of the method invocation. This must be null if there was an error.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Gets the error object if there was an error invoking the method.
    /// </summary>
    public JsonRpcError? Error { get; init; }

    /// <summary>
    /// Gets the request identifier. Must match the value from the request.
    /// </summary>
    public required object? Id { get; init; }
}