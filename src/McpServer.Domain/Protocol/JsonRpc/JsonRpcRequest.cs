namespace McpServer.Domain.Protocol.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 request.
/// </summary>
public record JsonRpcRequest
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

    /// <summary>
    /// Gets the request identifier. Can be a string, number, or null.
    /// </summary>
    public object? Id { get; init; }
}

/// <summary>
/// Represents a strongly-typed JSON-RPC 2.0 request.
/// </summary>
/// <typeparam name="TParams">The type of the parameters.</typeparam>
public record JsonRpcRequest<TParams> : JsonRpcRequest
{
    /// <summary>
    /// Gets the strongly-typed method parameters.
    /// </summary>
    public new TParams? Params { get; init; }
}