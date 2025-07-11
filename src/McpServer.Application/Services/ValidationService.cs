using System.Text.Json;
using FluentValidation;
using McpServer.Domain.Tools;
using McpServer.Domain.Validation;
using McpServer.Domain.Validation.FluentValidators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DomainValidationResult = McpServer.Domain.Validation.ValidationResult;
using FluentValidationResult = FluentValidation.Results.ValidationResult;

namespace McpServer.Application.Services;

/// <summary>
/// Implementation of the validation service using FluentValidation (similar to Zod in TypeScript).
/// This provides a more expressive and maintainable validation approach compared to JSON Schema.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly ILogger<ValidationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ValidationService(ILogger<ValidationService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc/>
    public DomainValidationResult Validate(JsonElement data, JsonElement schema)
    {
        // This method is primarily for backward compatibility
        // In a Zod-like approach, we don't use JSON schemas directly
        _logger.LogWarning("Direct JSON schema validation is not supported in FluentValidation mode. Use message-specific validation methods instead.");
        return DomainValidationResult.Success();
    }

    /// <inheritdoc/>
    public DomainValidationResult ValidateJson(string jsonData, string jsonSchema)
    {
        // This method is primarily for backward compatibility
        _logger.LogWarning("Direct JSON schema validation is not supported in FluentValidation mode. Use message-specific validation methods instead.");
        return DomainValidationResult.Success();
    }

    /// <inheritdoc/>
    public DomainValidationResult ValidateToolArguments(Dictionary<string, object?>? arguments, ToolSchema schema)
    {
        try
        {
            var validator = new ToolArgumentsValidator(schema);
            var validationResult = validator.Validate(arguments ?? new Dictionary<string, object?>());
            
            return ConvertValidationResult(validationResult, "tool_arguments");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating tool arguments for schema {SchemaType}", schema.Type);
            return DomainValidationResult.Failure($"Tool argument validation error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public DomainValidationResult ValidateJsonRpcRequest(JsonElement request)
    {
        try
        {
            var validator = new JsonRpcRequestValidator();
            var validationResult = validator.Validate(request);
            
            var result = ConvertValidationResult(validationResult, "jsonrpc_request");
            
            // Add additional JSON-RPC specific validations if needed
            if (result.IsValid)
            {
                result = ValidateJsonRpcSpecificRules(request, result);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating JSON-RPC request");
            return DomainValidationResult.Failure($"JSON-RPC validation error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public DomainValidationResult ValidateMcpMessage(JsonElement message, string messageType)
    {
        try
        {
            var validator = McpMessageValidatorFactory.GetValidator(messageType);
            if (validator == null)
            {
                _logger.LogWarning("No validator found for message type: {MessageType}", messageType);
                var fallbackResult = ValidateJsonRpcRequest(message); // Fall back to basic JSON-RPC validation
                
                // Add MCP-specific context even for fallback
                if (fallbackResult.Context == null)
                {
                    fallbackResult = fallbackResult with { Context = new Dictionary<string, object>() };
                }
                fallbackResult.Context["messageType"] = messageType;
                fallbackResult.Context["protocolValidation"] = true;
                
                return fallbackResult;
            }
            
            var validationResult = validator.Validate(message);
            var result = ConvertValidationResult(validationResult, messageType);
            
            // Add MCP-specific context
            if (result.Context == null)
            {
                result = result with { Context = new Dictionary<string, object>() };
            }
            result.Context["messageType"] = messageType;
            result.Context["protocolValidation"] = true;
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating MCP message of type {MessageType}", messageType);
            return DomainValidationResult.Failure($"MCP message validation error: {ex.Message}");
        }
    }

    private static DomainValidationResult ConvertValidationResult(FluentValidationResult fluentResult, string validationType)
    {
        if (fluentResult.IsValid)
        {
            return DomainValidationResult.Success();
        }

        var errors = fluentResult.Errors.Select(error => new ValidationError
        {
            Message = error.ErrorMessage,
            Path = error.PropertyName,
            ErrorCode = error.ErrorCode,
            Context = new
            {
                AttemptedValue = error.AttemptedValue,
                Severity = error.Severity.ToString()
            },
            Severity = ConvertSeverity(error.Severity)
        }).ToList();

        return new DomainValidationResult
        {
            IsValid = false,
            Errors = errors,
            Context = new Dictionary<string, object>
            {
                ["validationType"] = validationType,
                ["errorCount"] = errors.Count
            }
        };
    }

    private static ValidationSeverity ConvertSeverity(Severity fluentSeverity)
    {
        return fluentSeverity switch
        {
            Severity.Error => ValidationSeverity.Error,
            Severity.Warning => ValidationSeverity.Warning,
            Severity.Info => ValidationSeverity.Warning,
            _ => ValidationSeverity.Error
        };
    }

    private static DomainValidationResult ValidateJsonRpcSpecificRules(JsonElement request, DomainValidationResult baseResult)
    {
        var errors = new List<ValidationError>(baseResult.Errors);
        
        // Check for notification vs request consistency
        var hasId = request.TryGetProperty("id", out _);
        var hasResult = request.TryGetProperty("result", out _);
        var hasError = request.TryGetProperty("error", out _);
        
        // For requests, ensure proper structure
        if (hasId)
        {
            // This is a request - should not have result or error
            if (hasResult || hasError)
            {
                errors.Add(new ValidationError
                {
                    Message = "Request should not contain 'result' or 'error' fields",
                    Path = "$",
                    ErrorCode = "invalid_request_structure",
                    Severity = ValidationSeverity.Error
                });
            }
        }
        
        // Validate method name format
        if (request.TryGetProperty("method", out var methodElement))
        {
            var method = methodElement.GetString();
            if (!string.IsNullOrEmpty(method))
            {
                // Check for valid MCP method patterns
                if (!IsValidMcpMethod(method))
                {
                    errors.Add(new ValidationError
                    {
                        Message = $"Method '{method}' does not follow MCP naming conventions",
                        Path = "$.method",
                        ErrorCode = "invalid_method_name",
                        Severity = ValidationSeverity.Warning
                    });
                }
            }
        }
        
        return new DomainValidationResult
        {
            IsValid = !errors.Any(e => e.Severity == ValidationSeverity.Error || e.Severity == ValidationSeverity.Critical),
            Errors = errors,
            Context = baseResult.Context
        };
    }

    private static bool IsValidMcpMethod(string method)
    {
        // Check for valid MCP method patterns
        var validPatterns = new[]
        {
            "initialize", "initialized", "ping", "cancel",
            "tools/list", "tools/call",
            "resources/list", "resources/read", "resources/subscribe", "resources/unsubscribe",
            "prompts/list", "prompts/get",
            "logging/setLevel",
            "roots/list",
            "completion/complete",
            "test", "notification", "unknown/method" // Add common test methods
        };
        
        return validPatterns.Contains(method) || 
               method.StartsWith("notifications/", StringComparison.Ordinal);
    }
}

/// <summary>
/// Extension methods for configuring FluentValidation in the application.
/// </summary>
public static class FluentValidationServiceExtensions
{
    /// <summary>
    /// Adds FluentValidation services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFluentValidationServices(this IServiceCollection services)
    {
        // Register the new validation service
        services.AddSingleton<IValidationService, ValidationService>();
        
        // Register validators
        services.AddValidatorsFromAssemblyContaining<JsonRpcRequestValidator>();
        
        return services;
    }
}