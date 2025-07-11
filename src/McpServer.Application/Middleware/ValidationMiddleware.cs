using System.Text.Json;
using McpServer.Domain.Exceptions;
using McpServer.Domain.Protocol.JsonRpc;
using McpServer.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Middleware;

/// <summary>
/// Middleware for validating MCP messages against JSON schemas.
/// </summary>
public class ValidationMiddleware
{
    private readonly IValidationService _validationService;
    private readonly ILogger<ValidationMiddleware> _logger;
    private readonly ValidationMiddlewareOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationMiddleware"/> class.
    /// </summary>
    /// <param name="validationService">The validation service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The middleware options.</param>
    public ValidationMiddleware(
        IValidationService validationService, 
        ILogger<ValidationMiddleware> logger,
        ValidationMiddlewareOptions? options = null)
    {
        _validationService = validationService;
        _logger = logger;
        _options = options ?? new ValidationMiddlewareOptions();
    }

    /// <summary>
    /// Validates an incoming MCP message.
    /// </summary>
    /// <param name="message">The raw JSON message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validation result.</returns>
    public Task<ValidationResult> ValidateMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse the JSON message
            JsonElement messageElement;
            try
            {
                var document = JsonDocument.Parse(message);
                messageElement = document.RootElement;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Invalid JSON received: {Error}", ex.Message);
                return Task.FromResult(ValidationResult.Failure("Invalid JSON format", "$"));
            }

            // First validate against basic JSON-RPC schema
            var jsonRpcResult = _validationService.ValidateJsonRpcRequest(messageElement);
            if (!jsonRpcResult.IsValid)
            {
                if (_options.StrictMode)
                {
                    _logger.LogWarning("JSON-RPC validation failed: {Errors}", 
                        string.Join("; ", jsonRpcResult.Errors.Select(e => e.Message)));
                    return Task.FromResult(jsonRpcResult);
                }
                else
                {
                    _logger.LogDebug("JSON-RPC validation failed but continuing in lenient mode");
                }
            }

            // Extract method for MCP-specific validation
            string? method = null;
            if (messageElement.TryGetProperty("method", out var methodElement))
            {
                method = methodElement.GetString();
            }

            if (string.IsNullOrEmpty(method))
            {
                // This might be a response, not a request
                _logger.LogDebug("No method found in message, skipping MCP validation");
                return Task.FromResult(jsonRpcResult);
            }

            // Validate against MCP-specific schema
            var mcpResult = _validationService.ValidateMcpMessage(messageElement, method);
            
            // Combine results if needed
            var finalResult = CombineValidationResults(jsonRpcResult, mcpResult);
            
            // Log validation results
            if (!finalResult.IsValid)
            {
                _logger.LogWarning("Message validation failed for method {Method}: {Errors}", 
                    method, string.Join("; ", finalResult.Errors.Select(e => e.Message)));
            }
            else
            {
                _logger.LogDebug("Message validation succeeded for method {Method}", method);
            }

            return Task.FromResult(finalResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during message validation");
            return Task.FromResult(ValidationResult.Failure($"Validation error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Validates tool arguments before execution.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="schema">The tool schema.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult ValidateToolArguments(string toolName, Dictionary<string, object?>? arguments, Domain.Tools.ToolSchema schema)
    {
        try
        {
            var result = _validationService.ValidateToolArguments(arguments, schema);
            
            if (!result.IsValid)
            {
                _logger.LogWarning("Tool argument validation failed for {ToolName}: {Errors}", 
                    toolName, string.Join("; ", result.Errors.Select(e => e.Message)));
                
                if (_options.ThrowOnValidationFailure)
                {
                    var validationErrors = result.Errors.Select(e => e.Message).ToArray();
                    throw new InvalidToolArgumentsException(toolName, validationErrors);
                }
            }
            else
            {
                _logger.LogDebug("Tool argument validation succeeded for {ToolName}", toolName);
            }

            return result;
        }
        catch (InvalidToolArgumentsException)
        {
            throw; // Re-throw our own exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during tool argument validation for {ToolName}", toolName);
            
            if (_options.ThrowOnValidationFailure)
            {
                throw new InvalidToolArgumentsException(toolName, new[] { ex.Message });
            }
            
            return ValidationResult.Failure($"Tool argument validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a validation exception from validation results.
    /// </summary>
    /// <param name="result">The validation result.</param>
    /// <param name="requestId">The request ID, if available.</param>
    /// <returns>The MCP protocol exception.</returns>
    public static McpProtocolException CreateValidationException(ValidationResult result, object? requestId = null)
    {
        if (result.IsValid)
        {
            throw new InvalidOperationException("Cannot create exception from successful validation result");
        }

        var primaryError = result.Errors.FirstOrDefault();
        var message = primaryError?.Message ?? "Validation failed";
        
        // Determine the appropriate error code based on the error type
        var errorCode = primaryError?.ErrorCode switch
        {
            "invalid_method_name" => McpErrorCodes.MethodNotFound,
            "invalid_request_structure" => McpErrorCodes.InvalidRequest,
            "required" => McpErrorCodes.InvalidParams,
            "type" => McpErrorCodes.InvalidParams,
            _ => McpErrorCodes.InvalidParams
        };

        // Create detailed error data
        var errorData = new
        {
            validationErrors = result.Errors.Select(e => new
            {
                message = e.Message,
                path = e.Path,
                errorCode = e.ErrorCode,
                severity = e.Severity.ToString().ToLowerInvariant()
            }).ToArray(),
            context = result.Context,
            requestId = requestId
        };

        return new McpProtocolException(errorCode, message, errorData);
    }

    private static ValidationResult CombineValidationResults(ValidationResult jsonRpcResult, ValidationResult mcpResult)
    {
        // If both are valid, return success
        if (jsonRpcResult.IsValid && mcpResult.IsValid)
        {
            return ValidationResult.Success();
        }

        // Combine errors from both results
        var allErrors = new List<ValidationError>();
        allErrors.AddRange(jsonRpcResult.Errors);
        allErrors.AddRange(mcpResult.Errors);

        // Combine context
        var combinedContext = new Dictionary<string, object>();
        if (jsonRpcResult.Context != null)
        {
            foreach (var kvp in jsonRpcResult.Context)
            {
                combinedContext[kvp.Key] = kvp.Value;
            }
        }
        if (mcpResult.Context != null)
        {
            foreach (var kvp in mcpResult.Context)
            {
                combinedContext[$"mcp_{kvp.Key}"] = kvp.Value;
            }
        }

        return new ValidationResult
        {
            IsValid = false,
            Errors = allErrors,
            Context = combinedContext
        };
    }
}

/// <summary>
/// Configuration options for the validation middleware.
/// </summary>
public class ValidationMiddlewareOptions
{
    /// <summary>
    /// Gets or sets whether to use strict mode validation.
    /// In strict mode, any validation failure will prevent processing.
    /// </summary>
    public bool StrictMode { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to throw exceptions on validation failures.
    /// </summary>
    public bool ThrowOnValidationFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate tool arguments.
    /// </summary>
    public bool ValidateToolArguments { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate response messages.
    /// </summary>
    public bool ValidateResponses { get; set; }

    /// <summary>
    /// Gets or sets the maximum message size to validate (in bytes).
    /// Messages larger than this will skip validation.
    /// </summary>
    public int MaxMessageSizeForValidation { get; set; } = 1024 * 1024; // 1MB

    /// <summary>
    /// Gets or sets whether to log validation details.
    /// </summary>
    public bool LogValidationDetails { get; set; } = true;
}