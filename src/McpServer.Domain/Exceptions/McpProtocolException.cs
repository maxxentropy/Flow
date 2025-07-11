using McpServer.Domain.Protocol.JsonRpc;

namespace McpServer.Domain.Exceptions;

/// <summary>
/// Base exception for MCP protocol-related errors.
/// </summary>
public class McpProtocolException : McpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpProtocolException"/> class.
    /// </summary>
    /// <param name="errorCode">The MCP error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="data">Additional error data.</param>
    /// <param name="innerException">The inner exception.</param>
    public McpProtocolException(int errorCode, string? message = null, object? data = null, Exception? innerException = null)
        : base(errorCode, message ?? McpErrorCodes.GetErrorMessage(errorCode), data, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a tool is not found.
/// </summary>
public class ToolNotFoundException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolNotFoundException"/> class.
    /// </summary>
    /// <param name="toolName">The name of the tool that was not found.</param>
    public ToolNotFoundException(string toolName)
        : base(McpErrorCodes.ToolNotFound, $"Tool '{toolName}' not found", new { toolName })
    {
        ToolName = toolName;
    }

    /// <summary>
    /// Gets the name of the tool that was not found.
    /// </summary>
    public string ToolName { get; }
}

/// <summary>
/// Exception thrown when a tool execution fails.
/// </summary>
public class ToolExecutionException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutionException"/> class.
    /// </summary>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="message">The error message.</param>
    public ToolExecutionException(string toolName, string message)
        : base(McpErrorCodes.ToolExecutionFailed, $"Tool '{toolName}' execution failed: {message}", new { toolName })
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
        : base(McpErrorCodes.ToolExecutionFailed, $"Tool '{toolName}' execution failed: {message}", new { toolName }, innerException)
    {
        ToolName = toolName;
    }

    /// <summary>
    /// Gets the name of the tool that failed.
    /// </summary>
    public string ToolName { get; }
}


/// <summary>
/// Exception thrown when tool arguments are invalid.
/// </summary>
public class InvalidToolArgumentsException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidToolArgumentsException"/> class.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="validationErrors">The validation errors.</param>
    public InvalidToolArgumentsException(string toolName, IEnumerable<string> validationErrors)
        : base(McpErrorCodes.InvalidToolArguments, 
               $"Invalid arguments for tool '{toolName}'", 
               new { toolName, validationErrors = validationErrors.ToArray() })
    {
        ToolName = toolName;
        ValidationErrors = validationErrors.ToArray();
    }

    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public string[] ValidationErrors { get; }
}

/// <summary>
/// Exception thrown when a resource is not found.
/// </summary>
public class ResourceNotFoundException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceNotFoundException"/> class.
    /// </summary>
    /// <param name="resourceUri">The URI of the resource that was not found.</param>
    public ResourceNotFoundException(string resourceUri)
        : base(McpErrorCodes.ResourceNotFound, $"Resource '{resourceUri}' not found", new { resourceUri })
    {
        ResourceUri = resourceUri;
    }

    /// <summary>
    /// Gets the URI of the resource that was not found.
    /// </summary>
    public string ResourceUri { get; }
}

/// <summary>
/// Exception thrown when resource access is denied.
/// </summary>
public class ResourceAccessDeniedException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceAccessDeniedException"/> class.
    /// </summary>
    /// <param name="resourceUri">The URI of the resource.</param>
    /// <param name="reason">The reason for access denial.</param>
    public ResourceAccessDeniedException(string resourceUri, string? reason = null)
        : base(McpErrorCodes.ResourceAccessDenied, 
               reason ?? $"Access denied to resource '{resourceUri}'", 
               new { resourceUri, reason })
    {
        ResourceUri = resourceUri;
        Reason = reason;
    }

    /// <summary>
    /// Gets the URI of the resource.
    /// </summary>
    public string ResourceUri { get; }

    /// <summary>
    /// Gets the reason for access denial.
    /// </summary>
    public string? Reason { get; }
}

/// <summary>
/// Exception thrown when authentication is required but not provided.
/// </summary>
public class AuthenticationRequiredException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class.
    /// </summary>
    /// <param name="operation">The operation that requires authentication.</param>
    public AuthenticationRequiredException(string? operation = null)
        : base(McpErrorCodes.AuthenticationRequired, 
               operation != null ? $"Authentication required for operation: {operation}" : "Authentication required",
               operation != null ? new { operation } : null)
    {
        Operation = operation;
    }

    /// <summary>
    /// Gets the operation that requires authentication.
    /// </summary>
    public string? Operation { get; }
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class AuthenticationFailedException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationFailedException"/> class.
    /// </summary>
    /// <param name="reason">The reason for authentication failure.</param>
    public AuthenticationFailedException(string? reason = null)
        : base(McpErrorCodes.AuthenticationFailed, 
               reason ?? "Authentication failed", 
               reason != null ? new { reason } : null)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the reason for authentication failure.
    /// </summary>
    public string? Reason { get; }
}

/// <summary>
/// Exception thrown when insufficient permissions for an operation.
/// </summary>
public class InsufficientPermissionsException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InsufficientPermissionsException"/> class.
    /// </summary>
    /// <param name="operation">The operation that requires permissions.</param>
    /// <param name="requiredPermissions">The required permissions.</param>
    public InsufficientPermissionsException(string operation, IEnumerable<string>? requiredPermissions = null)
        : base(McpErrorCodes.InsufficientPermissions, 
               $"Insufficient permissions for operation: {operation}", 
               new { operation, requiredPermissions = requiredPermissions?.ToArray() })
    {
        Operation = operation;
        RequiredPermissions = requiredPermissions?.ToArray() ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets the operation that requires permissions.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the required permissions.
    /// </summary>
    public string[] RequiredPermissions { get; }
}

/// <summary>
/// Exception thrown when rate limit is exceeded.
/// </summary>
public class RateLimitExceededException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitExceededException"/> class.
    /// </summary>
    /// <param name="retryAfter">The time to wait before retrying.</param>
    /// <param name="operation">The operation that was rate limited.</param>
    public RateLimitExceededException(TimeSpan? retryAfter = null, string? operation = null)
        : base(McpErrorCodes.RateLimitExceeded, 
               "Rate limit exceeded", 
               new { retryAfter = retryAfter?.TotalSeconds, operation })
    {
        RetryAfter = retryAfter;
        Operation = operation;
    }

    /// <summary>
    /// Gets the time to wait before retrying.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets the operation that was rate limited.
    /// </summary>
    public string? Operation { get; }
}

/// <summary>
/// Exception thrown when an operation is cancelled.
/// </summary>
public class OperationCancelledException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationCancelledException"/> class.
    /// </summary>
    /// <param name="operationId">The ID of the cancelled operation.</param>
    /// <param name="reason">The reason for cancellation.</param>
    public OperationCancelledException(string? operationId = null, string? reason = null)
        : base(McpErrorCodes.OperationCancelled, 
               reason ?? "Operation was cancelled", 
               new { operationId, reason })
    {
        OperationId = operationId;
        Reason = reason;
    }

    /// <summary>
    /// Gets the ID of the cancelled operation.
    /// </summary>
    public string? OperationId { get; }

    /// <summary>
    /// Gets the reason for cancellation.
    /// </summary>
    public string? Reason { get; }
}

/// <summary>
/// Exception thrown when server is not initialized.
/// </summary>
public class ServerNotInitializedException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerNotInitializedException"/> class.
    /// </summary>
    public ServerNotInitializedException()
        : base(McpErrorCodes.ServerNotInitialized)
    {
    }
}

/// <summary>
/// Exception thrown when server is already initialized.
/// </summary>
public class ServerAlreadyInitializedException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerAlreadyInitializedException"/> class.
    /// </summary>
    public ServerAlreadyInitializedException()
        : base(McpErrorCodes.ServerAlreadyInitialized)
    {
    }
}