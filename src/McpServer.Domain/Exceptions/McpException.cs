namespace McpServer.Domain.Exceptions;

/// <summary>
/// Base exception for all MCP-related errors.
/// </summary>
public class McpException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpException"/> class.
    /// </summary>
    public McpException()
    {
        ErrorCode = -32603; // Internal error by default
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public McpException(string message) : base(message)
    {
        ErrorCode = -32603; // Internal error by default
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public McpException(string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = -32603; // Internal error by default
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpException"/> class.
    /// </summary>
    /// <param name="errorCode">The MCP error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="data">Additional error data.</param>
    /// <param name="innerException">The inner exception.</param>
    public McpException(int errorCode, string message, object? data = null, Exception? innerException = null) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Data = data;
    }

    /// <summary>
    /// Gets the MCP error code.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Gets additional error data.
    /// </summary>
    public new object? Data { get; }
}

/// <summary>
/// Exception thrown when protocol validation fails.
/// </summary>
public class ProtocolException : McpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ProtocolException(string message) : base(-32600, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ProtocolException(string message, Exception innerException) : base(-32600, message, null, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a resource operation fails.
/// </summary>
public class ResourceException : McpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceException"/> class.
    /// </summary>
    /// <param name="uri">The URI of the resource.</param>
    /// <param name="message">The error message.</param>
    public ResourceException(string uri, string message) : base(-32200, $"Resource '{uri}' operation failed: {message}", new { uri })
    {
        Uri = uri;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceException"/> class.
    /// </summary>
    /// <param name="uri">The URI of the resource.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ResourceException(string uri, string message, Exception innerException) 
        : base(-32200, $"Resource '{uri}' operation failed: {message}", new { uri }, innerException)
    {
        Uri = uri;
    }

    /// <summary>
    /// Gets the URI of the resource.
    /// </summary>
    public string Uri { get; }
}

/// <summary>
/// Exception thrown when transport operations fail.
/// </summary>
public class TransportException : McpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransportException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TransportException(string message) : base(-32751, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TransportException(string message, Exception innerException) : base(-32751, message, null, innerException)
    {
    }
}