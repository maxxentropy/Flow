using System.Text.Json;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.JsonRpc;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Service for building detailed error responses for MCP operations.
/// </summary>
public class ErrorResponseBuilder : IErrorResponseBuilder
{
    private readonly ILogger<ErrorResponseBuilder> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorResponseBuilder"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ErrorResponseBuilder(ILogger<ErrorResponseBuilder> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc/>
    public JsonRpcResponse CreateErrorResponse(object? id, Exception exception)
    {
        var error = CreateJsonRpcError(exception);
        
        _logger.LogError(exception, "Creating error response for request {RequestId}: {ErrorCode} - {ErrorMessage}", 
            id, error.Code, error.Message);

        return new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Error = error,
            Id = id
        };
    }

    /// <inheritdoc/>
    public JsonRpcResponse CreateErrorResponse(object? id, int errorCode, string message, object? data = null)
    {
        var error = new JsonRpcError
        {
            Code = errorCode,
            Message = message,
            Data = data
        };

        _logger.LogError("Creating error response for request {RequestId}: {ErrorCode} - {ErrorMessage}", 
            id, errorCode, message);

        return new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Error = error,
            Id = id
        };
    }

    /// <inheritdoc/>
    public JsonRpcError CreateJsonRpcError(Exception exception)
    {
        return exception switch
        {
            McpException mcpEx => new JsonRpcError
            {
                Code = mcpEx.ErrorCode,
                Message = mcpEx.Message,
                Data = CreateErrorData(mcpEx)
            },
            
            ArgumentException argEx => new JsonRpcError
            {
                Code = McpErrorCodes.InvalidParams,
                Message = argEx.Message,
                Data = CreateErrorData(argEx, new { parameter = argEx.ParamName })
            },
            
            UnauthorizedAccessException => new JsonRpcError
            {
                Code = McpErrorCodes.AuthenticationRequired,
                Message = "Authentication required",
                Data = CreateErrorData(exception)
            },
            
            TimeoutException => new JsonRpcError
            {
                Code = McpErrorCodes.OperationTimeout,
                Message = "Operation timeout",
                Data = CreateErrorData(exception)
            },
            
            OperationCanceledException cancelEx => new JsonRpcError
            {
                Code = McpErrorCodes.OperationCancelled,
                Message = "Operation was cancelled",
                Data = CreateErrorData(exception, new { cancellationToken = cancelEx.CancellationToken.IsCancellationRequested })
            },
            
            NotImplementedException => new JsonRpcError
            {
                Code = McpErrorCodes.CapabilityNotSupported,
                Message = "Feature not implemented",
                Data = CreateErrorData(exception)
            },
            
            InvalidOperationException invalidOpEx => new JsonRpcError
            {
                Code = McpErrorCodes.InternalError,
                Message = invalidOpEx.Message,
                Data = CreateErrorData(exception)
            },
            
            JsonException jsonEx => new JsonRpcError
            {
                Code = McpErrorCodes.ParseError,
                Message = "JSON parse error",
                Data = CreateErrorData(exception, new { 
                    lineNumber = jsonEx.LineNumber,
                    bytePositionInLine = jsonEx.BytePositionInLine 
                })
            },
            
            _ => new JsonRpcError
            {
                Code = McpErrorCodes.InternalError,
                Message = "Internal server error",
                Data = CreateErrorData(exception)
            }
        };
    }

    /// <inheritdoc/>
    public object CreateErrorData(Exception exception, object? additionalData = null)
    {
        var errorData = new Dictionary<string, object?>
        {
            ["type"] = exception.GetType().Name,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Add stack trace in development/debug mode
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            errorData["stackTrace"] = exception.StackTrace?.Split('\n')
                .Take(10) // Limit stack trace depth
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToArray();
        }

        // Add inner exception details
        if (exception.InnerException != null)
        {
            errorData["innerException"] = new
            {
                type = exception.InnerException.GetType().Name,
                message = exception.InnerException.Message
            };
        }

        // Add MCP-specific data for MCP exceptions
        if (exception is McpException mcpEx && mcpEx.Data != null)
        {
            errorData["mcpData"] = mcpEx.Data;
        }

        // Merge additional data
        if (additionalData != null)
        {
            try
            {
                var additionalJson = JsonSerializer.Serialize(additionalData, _jsonOptions);
                var additionalDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(additionalJson, _jsonOptions);
                
                if (additionalDict != null)
                {
                    foreach (var kvp in additionalDict)
                    {
                        errorData[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize additional error data");
                errorData["additionalDataError"] = "Failed to serialize additional data";
            }
        }

        return errorData;
    }

    /// <inheritdoc/>
    public bool ShouldIncludeStackTrace(Exception exception)
    {
        // Include stack trace for development/debug environments
        if (_logger.IsEnabled(LogLevel.Debug))
            return true;

        // Include for server errors but not client errors
        if (exception is McpException mcpEx)
        {
            return McpErrorCodes.IsServerError(mcpEx.ErrorCode);
        }

        // Include for unexpected exceptions
        return exception is not (ArgumentException or UnauthorizedAccessException);
    }

    /// <inheritdoc/>
    public string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            McpException mcpEx => mcpEx.Message,
            ArgumentException => "Invalid request parameters",
            UnauthorizedAccessException => "Authentication required",
            TimeoutException => "The operation timed out",
            OperationCanceledException => "The operation was cancelled",
            NotImplementedException => "This feature is not supported",
            JsonException => "Invalid JSON format",
            _ => "An internal error occurred"
        };
    }
}

/// <summary>
/// Interface for building error responses.
/// </summary>
public interface IErrorResponseBuilder
{
    /// <summary>
    /// Creates an error response from an exception.
    /// </summary>
    /// <param name="id">The request ID.</param>
    /// <param name="exception">The exception.</param>
    /// <returns>The error response.</returns>
    JsonRpcResponse CreateErrorResponse(object? id, Exception exception);

    /// <summary>
    /// Creates an error response with specific error details.
    /// </summary>
    /// <param name="id">The request ID.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="data">Additional error data.</param>
    /// <returns>The error response.</returns>
    JsonRpcResponse CreateErrorResponse(object? id, int errorCode, string message, object? data = null);

    /// <summary>
    /// Creates a JSON-RPC error from an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The JSON-RPC error.</returns>
    JsonRpcError CreateJsonRpcError(Exception exception);

    /// <summary>
    /// Creates error data object from an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="additionalData">Additional data to include.</param>
    /// <returns>The error data object.</returns>
    object CreateErrorData(Exception exception, object? additionalData = null);

    /// <summary>
    /// Determines if stack trace should be included for this exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>True if stack trace should be included.</returns>
    bool ShouldIncludeStackTrace(Exception exception);

    /// <summary>
    /// Gets a user-friendly error message for the exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>A user-friendly error message.</returns>
    string GetUserFriendlyMessage(Exception exception);
}