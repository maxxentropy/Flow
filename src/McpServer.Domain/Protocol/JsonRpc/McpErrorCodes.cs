namespace McpServer.Domain.Protocol.JsonRpc;

/// <summary>
/// MCP-specific error codes extending JSON-RPC standard error codes.
/// </summary>
public static class McpErrorCodes
{
    // Standard JSON-RPC errors (from JsonRpcErrorCodes)
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    
    // MCP-specific protocol errors (-32000 to -32099)
    
    /// <summary>
    /// Server has not been initialized yet.
    /// </summary>
    public const int ServerNotInitialized = -32000;
    
    /// <summary>
    /// Server is already initialized.
    /// </summary>
    public const int ServerAlreadyInitialized = -32001;
    
    /// <summary>
    /// Unsupported protocol version.
    /// </summary>
    public const int UnsupportedProtocolVersion = -32002;
    
    /// <summary>
    /// Capability not supported by this server.
    /// </summary>
    public const int CapabilityNotSupported = -32003;
    
    /// <summary>
    /// Invalid capability negotiation.
    /// </summary>
    public const int InvalidCapabilityNegotiation = -32004;
    
    // Tool-related errors (-32100 to -32199)
    
    /// <summary>
    /// Tool not found.
    /// </summary>
    public const int ToolNotFound = -32100;
    
    /// <summary>
    /// Tool execution failed.
    /// </summary>
    public const int ToolExecutionFailed = -32101;
    
    /// <summary>
    /// Tool execution timeout.
    /// </summary>
    public const int ToolExecutionTimeout = -32102;
    
    /// <summary>
    /// Invalid tool arguments.
    /// </summary>
    public const int InvalidToolArguments = -32103;
    
    /// <summary>
    /// Tool not authorized for current user.
    /// </summary>
    public const int ToolNotAuthorized = -32104;
    
    /// <summary>
    /// Too many concurrent tool executions.
    /// </summary>
    public const int TooManyConcurrentTools = -32105;
    
    // Resource-related errors (-32200 to -32299)
    
    /// <summary>
    /// Resource not found.
    /// </summary>
    public const int ResourceNotFound = -32200;
    
    /// <summary>
    /// Resource access denied.
    /// </summary>
    public const int ResourceAccessDenied = -32201;
    
    /// <summary>
    /// Resource is not readable.
    /// </summary>
    public const int ResourceNotReadable = -32202;
    
    /// <summary>
    /// Resource subscription failed.
    /// </summary>
    public const int ResourceSubscriptionFailed = -32203;
    
    /// <summary>
    /// Resource subscription not supported.
    /// </summary>
    public const int ResourceSubscriptionNotSupported = -32204;
    
    /// <summary>
    /// Invalid resource URI.
    /// </summary>
    public const int InvalidResourceUri = -32205;
    
    /// <summary>
    /// Resource provider error.
    /// </summary>
    public const int ResourceProviderError = -32206;
    
    // Prompt-related errors (-32300 to -32399)
    
    /// <summary>
    /// Prompt not found.
    /// </summary>
    public const int PromptNotFound = -32300;
    
    /// <summary>
    /// Invalid prompt arguments.
    /// </summary>
    public const int InvalidPromptArguments = -32301;
    
    /// <summary>
    /// Prompt execution failed.
    /// </summary>
    public const int PromptExecutionFailed = -32302;
    
    // Authentication and authorization errors (-32400 to -32499)
    
    /// <summary>
    /// Authentication required.
    /// </summary>
    public const int AuthenticationRequired = -32400;
    
    /// <summary>
    /// Authentication failed.
    /// </summary>
    public const int AuthenticationFailed = -32401;
    
    /// <summary>
    /// Insufficient permissions.
    /// </summary>
    public const int InsufficientPermissions = -32402;
    
    /// <summary>
    /// Session expired.
    /// </summary>
    public const int SessionExpired = -32403;
    
    /// <summary>
    /// Invalid authentication token.
    /// </summary>
    public const int InvalidAuthenticationToken = -32404;
    
    // Progress and cancellation errors (-32500 to -32599)
    
    /// <summary>
    /// Operation already cancelled.
    /// </summary>
    public const int OperationCancelled = -32500;
    
    /// <summary>
    /// Operation timeout.
    /// </summary>
    public const int OperationTimeout = -32501;
    
    /// <summary>
    /// Progress token not found.
    /// </summary>
    public const int ProgressTokenNotFound = -32502;
    
    /// <summary>
    /// Invalid progress token.
    /// </summary>
    public const int InvalidProgressToken = -32503;
    
    // Rate limiting errors (-32650 to -32699)
    
    /// <summary>
    /// Rate limit exceeded.
    /// </summary>
    public const int RateLimitExceeded = -32650;
    
    /// <summary>
    /// Too many requests.
    /// </summary>
    public const int TooManyRequests = -32651;
    
    /// <summary>
    /// Quota exceeded.
    /// </summary>
    public const int QuotaExceeded = -32652;
    
    // Transport and connection errors (-32750 to -32799)
    
    /// <summary>
    /// Connection lost.
    /// </summary>
    public const int ConnectionLost = -32750;
    
    /// <summary>
    /// Transport error.
    /// </summary>
    public const int TransportError = -32751;
    
    /// <summary>
    /// Message too large.
    /// </summary>
    public const int MessageTooLarge = -32752;
    
    /// <summary>
    /// Protocol mismatch.
    /// </summary>
    public const int ProtocolMismatch = -32753;
    
    // Configuration and setup errors (-32800 to -32899)
    
    /// <summary>
    /// Server misconfigured.
    /// </summary>
    public const int ServerMisconfigured = -32800;
    
    /// <summary>
    /// Feature not enabled.
    /// </summary>
    public const int FeatureNotEnabled = -32801;
    
    /// <summary>
    /// Service unavailable.
    /// </summary>
    public const int ServiceUnavailable = -32802;
    
    /// <summary>
    /// Maintenance mode.
    /// </summary>
    public const int MaintenanceMode = -32803;
    
    /// <summary>
    /// Gets a human-readable error message for the given error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>A human-readable error message.</returns>
    public static string GetErrorMessage(int errorCode)
    {
        return errorCode switch
        {
            ParseError => "Parse error",
            InvalidRequest => "Invalid Request",
            MethodNotFound => "Method not found",
            InvalidParams => "Invalid params",
            InternalError => "Internal error",
            
            ServerNotInitialized => "Server has not been initialized",
            ServerAlreadyInitialized => "Server is already initialized",
            UnsupportedProtocolVersion => "Unsupported protocol version",
            CapabilityNotSupported => "Capability not supported",
            InvalidCapabilityNegotiation => "Invalid capability negotiation",
            
            ToolNotFound => "Tool not found",
            ToolExecutionFailed => "Tool execution failed",
            ToolExecutionTimeout => "Tool execution timeout",
            InvalidToolArguments => "Invalid tool arguments",
            ToolNotAuthorized => "Tool not authorized",
            TooManyConcurrentTools => "Too many concurrent tool executions",
            
            ResourceNotFound => "Resource not found",
            ResourceAccessDenied => "Resource access denied",
            ResourceNotReadable => "Resource is not readable",
            ResourceSubscriptionFailed => "Resource subscription failed",
            ResourceSubscriptionNotSupported => "Resource subscription not supported",
            InvalidResourceUri => "Invalid resource URI",
            ResourceProviderError => "Resource provider error",
            
            PromptNotFound => "Prompt not found",
            InvalidPromptArguments => "Invalid prompt arguments",
            PromptExecutionFailed => "Prompt execution failed",
            
            AuthenticationRequired => "Authentication required",
            AuthenticationFailed => "Authentication failed",
            InsufficientPermissions => "Insufficient permissions",
            SessionExpired => "Session expired",
            InvalidAuthenticationToken => "Invalid authentication token",
            
            OperationCancelled => "Operation was cancelled",
            OperationTimeout => "Operation timeout",
            ProgressTokenNotFound => "Progress token not found",
            InvalidProgressToken => "Invalid progress token",
            
            RateLimitExceeded => "Rate limit exceeded",
            TooManyRequests => "Too many requests",
            QuotaExceeded => "Quota exceeded",
            
            ConnectionLost => "Connection lost",
            TransportError => "Transport error",
            MessageTooLarge => "Message too large",
            ProtocolMismatch => "Protocol mismatch",
            
            ServerMisconfigured => "Server misconfigured",
            FeatureNotEnabled => "Feature not enabled",
            ServiceUnavailable => "Service unavailable",
            MaintenanceMode => "Server is in maintenance mode",
            
            _ => "Unknown error"
        };
    }
    
    /// <summary>
    /// Determines if an error code represents a client error (4xx equivalent).
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>True if the error is a client error, false otherwise.</returns>
    public static bool IsClientError(int errorCode)
    {
        return errorCode switch
        {
            ParseError or InvalidRequest or MethodNotFound or InvalidParams => true,
            InvalidToolArguments or InvalidPromptArguments or InvalidResourceUri => true,
            AuthenticationRequired or AuthenticationFailed or InsufficientPermissions => true,
            InvalidAuthenticationToken or SessionExpired => true,
            ToolNotFound or ResourceNotFound or PromptNotFound => true,
            ProgressTokenNotFound or InvalidProgressToken => true,
            RateLimitExceeded or TooManyRequests or QuotaExceeded => true,
            CapabilityNotSupported or UnsupportedProtocolVersion => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Determines if an error code represents a server error (5xx equivalent).
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>True if the error is a server error, false otherwise.</returns>
    public static bool IsServerError(int errorCode)
    {
        return errorCode switch
        {
            InternalError => true,
            ToolExecutionFailed or ToolExecutionTimeout or ResourceProviderError => true,
            PromptExecutionFailed or ResourceSubscriptionFailed => true,
            ServerMisconfigured or FeatureNotEnabled or ServiceUnavailable => true,
            MaintenanceMode or TransportError or ConnectionLost => true,
            OperationTimeout => true,
            _ => false
        };
    }
}