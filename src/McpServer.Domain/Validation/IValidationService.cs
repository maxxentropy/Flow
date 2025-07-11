using System.Text.Json;
using McpServer.Domain.Tools;

namespace McpServer.Domain.Validation;

/// <summary>
/// Service for validating JSON data against schemas.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates a JSON element against a JSON schema.
    /// </summary>
    /// <param name="data">The JSON data to validate.</param>
    /// <param name="schema">The JSON schema to validate against.</param>
    /// <returns>The validation result.</returns>
    ValidationResult Validate(JsonElement data, JsonElement schema);

    /// <summary>
    /// Validates a JSON string against a JSON schema string.
    /// </summary>
    /// <param name="jsonData">The JSON data string to validate.</param>
    /// <param name="jsonSchema">The JSON schema string to validate against.</param>
    /// <returns>The validation result.</returns>
    ValidationResult ValidateJson(string jsonData, string jsonSchema);

    /// <summary>
    /// Validates tool arguments against a tool schema.
    /// </summary>
    /// <param name="arguments">The tool arguments to validate.</param>
    /// <param name="schema">The tool schema to validate against.</param>
    /// <returns>The validation result.</returns>
    ValidationResult ValidateToolArguments(Dictionary<string, object?>? arguments, ToolSchema schema);

    /// <summary>
    /// Validates a JSON-RPC request against the protocol schema.
    /// </summary>
    /// <param name="request">The JSON-RPC request to validate.</param>
    /// <returns>The validation result.</returns>
    ValidationResult ValidateJsonRpcRequest(JsonElement request);

    /// <summary>
    /// Validates an MCP message against the appropriate schema.
    /// </summary>
    /// <param name="message">The MCP message to validate.</param>
    /// <param name="messageType">The type of MCP message.</param>
    /// <returns>The validation result.</returns>
    ValidationResult ValidateMcpMessage(JsonElement message, string messageType);
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Gets whether the validation was successful.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public List<ValidationError> Errors { get; init; } = new();

    /// <summary>
    /// Gets additional validation context information.
    /// </summary>
    public Dictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A successful validation result.</returns>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(params ValidationError[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="path">The JSON path where the error occurred.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(string message, string? path = null) => new()
    {
        IsValid = false,
        Errors = new List<ValidationError> { new() { Message = message, Path = path } }
    };
}

/// <summary>
/// Represents a validation error.
/// </summary>
public record ValidationError
{
    /// <summary>
    /// Gets the error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the JSON path where the error occurred.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the error code or type.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets additional error context.
    /// </summary>
    public object? Context { get; init; }

    /// <summary>
    /// Gets the severity of the error.
    /// </summary>
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;
}

/// <summary>
/// Represents the severity of a validation error.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Warning that doesn't prevent processing.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that prevents processing.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that indicates a serious problem.
    /// </summary>
    Critical
}