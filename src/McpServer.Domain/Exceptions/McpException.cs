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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public McpException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public McpException(string message, Exception innerException) : base(message, innerException)
    {
    }
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
    public ProtocolException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ProtocolException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a tool execution fails.
/// </summary>
public class ToolExecutionException : McpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutionException"/> class.
    /// </summary>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="message">The error message.</param>
    public ToolExecutionException(string toolName, string message) : base($"Tool '{toolName}' execution failed: {message}")
    {
        ToolName = toolName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutionException"/> class.
    /// </summary>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ToolExecutionException(string toolName, string message, Exception innerException) 
        : base($"Tool '{toolName}' execution failed: {message}", innerException)
    {
        ToolName = toolName;
    }

    /// <summary>
    /// Gets the name of the tool that failed.
    /// </summary>
    public string ToolName { get; }
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
    public ResourceException(string uri, string message) : base($"Resource '{uri}' operation failed: {message}")
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
        : base($"Resource '{uri}' operation failed: {message}", innerException)
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
    public TransportException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TransportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}