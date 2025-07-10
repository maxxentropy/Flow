namespace McpServer.Domain.Protocol.JsonRpc;

/// <summary>
/// Represents a JSON-RPC 2.0 error.
/// </summary>
public record JsonRpcError
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public required int Code { get; init; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets additional information about the error.
    /// </summary>
    public object? Data { get; init; }
}

/// <summary>
/// Standard JSON-RPC 2.0 error codes.
/// </summary>
public static class JsonRpcErrorCodes
{
    /// <summary>
    /// Invalid JSON was received by the server.
    /// </summary>
    public const int ParseError = -32700;

    /// <summary>
    /// The JSON sent is not a valid Request object.
    /// </summary>
    public const int InvalidRequest = -32600;

    /// <summary>
    /// The method does not exist or is not available.
    /// </summary>
    public const int MethodNotFound = -32601;

    /// <summary>
    /// Invalid method parameter(s).
    /// </summary>
    public const int InvalidParams = -32602;

    /// <summary>
    /// Internal JSON-RPC error.
    /// </summary>
    public const int InternalError = -32603;

    /// <summary>
    /// Reserved for implementation-defined server-errors.
    /// </summary>
    public const int ServerErrorStart = -32000;
    public const int ServerErrorEnd = -32099;
}