using FluentValidation;
using System.Text.Json;

namespace McpServer.Domain.Validation.FluentValidators;

/// <summary>
/// Validator for JSON-RPC 2.0 base requests.
/// </summary>
public class JsonRpcRequestValidator : AbstractValidator<JsonElement>
{
    public JsonRpcRequestValidator()
    {
        RuleFor(x => x)
            .Must(BeValidJsonObject)
            .WithMessage("Request must be a valid JSON object")
            .WithErrorCode("invalid_type");

        RuleFor(x => x)
            .Must(HaveJsonRpcVersion)
            .WithMessage("Missing or invalid 'jsonrpc' field - must be '2.0'")
            .WithErrorCode("invalid_jsonrpc");

        RuleFor(x => x)
            .Must(HaveMethod)
            .WithMessage("Missing or invalid 'method' field")
            .WithErrorCode("missing_method");

        RuleFor(x => x)
            .Must(HaveValidParams)
            .When(HasParams)
            .WithMessage("'params' must be an object, array, or null")
            .WithErrorCode("invalid_params");

        RuleFor(x => x)
            .Must(HaveValidId)
            .When(HasId)
            .WithMessage("'id' must be a string, number, or null")
            .WithErrorCode("invalid_id");

        RuleFor(x => x)
            .Must(HaveValidMeta)
            .When(HasMeta)
            .WithMessage("'_meta' must be an object")
            .WithErrorCode("invalid_meta");

        RuleFor(x => x)
            .Must(HaveNoExtraProperties)
            .WithMessage("Request contains unexpected properties")
            .WithErrorCode("unexpected_properties");
    }

    private static bool BeValidJsonObject(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveJsonRpcVersion(JsonElement element)
    {
        return element.TryGetProperty("jsonrpc", out var jsonrpc) &&
               jsonrpc.ValueKind == JsonValueKind.String &&
               jsonrpc.GetString() == "2.0";
    }

    private static bool HaveMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.ValueKind == JsonValueKind.String &&
               !string.IsNullOrEmpty(method.GetString());
    }

    private static bool HasParams(JsonElement element)
    {
        return element.TryGetProperty("params", out _);
    }

    private static bool HaveValidParams(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params))
            return true;

        return @params.ValueKind == JsonValueKind.Object ||
               @params.ValueKind == JsonValueKind.Array ||
               @params.ValueKind == JsonValueKind.Null;
    }

    private static bool HasId(JsonElement element)
    {
        return element.TryGetProperty("id", out _);
    }

    private static bool HaveValidId(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var id))
            return true;

        return id.ValueKind == JsonValueKind.String ||
               id.ValueKind == JsonValueKind.Number ||
               id.ValueKind == JsonValueKind.Null;
    }

    private static bool HasMeta(JsonElement element)
    {
        return element.TryGetProperty("_meta", out _);
    }

    private static bool HaveValidMeta(JsonElement element)
    {
        if (!element.TryGetProperty("_meta", out var meta))
            return true;

        return meta.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveNoExtraProperties(JsonElement element)
    {
        var allowedProperties = new HashSet<string> { "jsonrpc", "method", "params", "id", "_meta" };
        
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
                return false;
        }

        return true;
    }
}

/// <summary>
/// Validator for JSON-RPC 2.0 responses.
/// </summary>
public class JsonRpcResponseValidator : AbstractValidator<JsonElement>
{
    public JsonRpcResponseValidator()
    {
        RuleFor(x => x)
            .Must(BeValidJsonObject)
            .WithMessage("Response must be a valid JSON object")
            .WithErrorCode("invalid_type");

        RuleFor(x => x)
            .Must(HaveJsonRpcVersion)
            .WithMessage("Missing or invalid 'jsonrpc' field - must be '2.0'")
            .WithErrorCode("invalid_jsonrpc");

        RuleFor(x => x)
            .Must(HaveId)
            .WithMessage("Response must have an 'id' field")
            .WithErrorCode("missing_id");

        RuleFor(x => x)
            .Must(HaveValidId)
            .WithMessage("'id' must be a string, number, or null")
            .WithErrorCode("invalid_id");

        RuleFor(x => x)
            .Must(HaveEitherResultOrError)
            .WithMessage("Response must have either 'result' or 'error', but not both")
            .WithErrorCode("invalid_response_structure");

        RuleFor(x => x)
            .Must(HaveValidError)
            .When(HasError)
            .WithMessage("'error' must be a valid error object with 'code' and 'message'")
            .WithErrorCode("invalid_error");

        RuleFor(x => x)
            .Must(HaveNoExtraProperties)
            .WithMessage("Response contains unexpected properties")
            .WithErrorCode("unexpected_properties");
    }

    private static bool BeValidJsonObject(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveJsonRpcVersion(JsonElement element)
    {
        return element.TryGetProperty("jsonrpc", out var jsonrpc) &&
               jsonrpc.ValueKind == JsonValueKind.String &&
               jsonrpc.GetString() == "2.0";
    }

    private static bool HaveId(JsonElement element)
    {
        return element.TryGetProperty("id", out _);
    }

    private static bool HaveValidId(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var id))
            return false;

        return id.ValueKind == JsonValueKind.String ||
               id.ValueKind == JsonValueKind.Number ||
               id.ValueKind == JsonValueKind.Null;
    }

    private static bool HaveEitherResultOrError(JsonElement element)
    {
        var hasResult = element.TryGetProperty("result", out _);
        var hasError = element.TryGetProperty("error", out _);

        return (hasResult && !hasError) || (!hasResult && hasError);
    }

    private static bool HasError(JsonElement element)
    {
        return element.TryGetProperty("error", out _);
    }

    private static bool HaveValidError(JsonElement element)
    {
        if (!element.TryGetProperty("error", out var error))
            return true;

        if (error.ValueKind != JsonValueKind.Object)
            return false;

        var hasCode = error.TryGetProperty("code", out var code) &&
                      code.ValueKind == JsonValueKind.Number;

        var hasMessage = error.TryGetProperty("message", out var message) &&
                         message.ValueKind == JsonValueKind.String;

        return hasCode && hasMessage;
    }

    private static bool HaveNoExtraProperties(JsonElement element)
    {
        var allowedProperties = new HashSet<string> { "jsonrpc", "id", "result", "error" };
        
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
                return false;
        }

        return true;
    }
}