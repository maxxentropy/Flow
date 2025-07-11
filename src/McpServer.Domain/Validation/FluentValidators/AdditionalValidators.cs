using FluentValidation;
using System.Text.Json;

namespace McpServer.Domain.Validation.FluentValidators;

/// <summary>
/// Validator for tools/list requests.
/// </summary>
public class ToolsListRequestValidator : AbstractValidator<JsonElement>
{
    public ToolsListRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveToolsListMethod)
            .WithMessage("Method must be 'tools/list'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Tools list request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveToolsListMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "tools/list";
    }

    private static bool HaveRequestId(JsonElement element)
    {
        return element.TryGetProperty("id", out var id) &&
               (id.ValueKind == JsonValueKind.String || id.ValueKind == JsonValueKind.Number);
    }
}

/// <summary>
/// Validator for logging/setLevel requests.
/// </summary>
public class LoggingSetLevelRequestValidator : AbstractValidator<JsonElement>
{
    private static readonly HashSet<string> ValidLogLevels = new()
    {
        "debug", "info", "warning", "error", "critical"
    };

    public LoggingSetLevelRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveLoggingSetLevelMethod)
            .WithMessage("Method must be 'logging/setLevel'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveParams)
            .WithMessage("Logging setLevel request must have 'params'")
            .WithErrorCode("missing_params");

        RuleFor(x => x)
            .Must(HaveValidLevel)
            .WithMessage("Invalid or missing 'level' in params")
            .WithErrorCode("invalid_log_level");

        RuleFor(x => x)
            .Must(HaveNoExtraParamProperties)
            .WithMessage("Params contains unexpected properties")
            .WithErrorCode("unexpected_param_properties");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Logging setLevel request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveLoggingSetLevelMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "logging/setLevel";
    }

    private static bool HaveParams(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveValidLevel(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("level", out var level))
            return false;

        if (level.ValueKind != JsonValueKind.String)
            return false;

        var levelString = level.GetString();
        return !string.IsNullOrEmpty(levelString) && 
               ValidLogLevels.Contains(levelString.ToLowerInvariant());
    }

    private static bool HaveNoExtraParamProperties(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params))
            return true;

        var allowedProperties = new HashSet<string> { "level" };
        
        foreach (var property in @params.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
                return false;
        }

        return true;
    }

    private static bool HaveRequestId(JsonElement element)
    {
        return element.TryGetProperty("id", out var id) &&
               (id.ValueKind == JsonValueKind.String || id.ValueKind == JsonValueKind.Number);
    }
}

/// <summary>
/// Validator for roots/list requests.
/// </summary>
public class RootsListRequestValidator : AbstractValidator<JsonElement>
{
    public RootsListRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveRootsListMethod)
            .WithMessage("Method must be 'roots/list'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Roots list request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveRootsListMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "roots/list";
    }

    private static bool HaveRequestId(JsonElement element)
    {
        return element.TryGetProperty("id", out var id) &&
               (id.ValueKind == JsonValueKind.String || id.ValueKind == JsonValueKind.Number);
    }
}

/// <summary>
/// Validator for completion/complete requests.
/// </summary>
public class CompletionCompleteRequestValidator : AbstractValidator<JsonElement>
{
    public CompletionCompleteRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveCompletionCompleteMethod)
            .WithMessage("Method must be 'completion/complete'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveParams)
            .WithMessage("Completion complete request must have 'params'")
            .WithErrorCode("missing_params");

        RuleFor(x => x)
            .Must(HaveValidRef)
            .WithMessage("Invalid or missing 'ref' in params")
            .WithErrorCode("invalid_ref");

        RuleFor(x => x)
            .Must(HaveValidArgument)
            .WithMessage("Invalid or missing 'argument' in params")
            .WithErrorCode("invalid_argument");

        RuleFor(x => x)
            .Must(HaveNoExtraParamProperties)
            .WithMessage("Params contains unexpected properties")
            .WithErrorCode("unexpected_param_properties");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Completion complete request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveCompletionCompleteMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "completion/complete";
    }

    private static bool HaveParams(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveValidRef(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("ref", out var @ref))
            return false;

        if (@ref.ValueKind != JsonValueKind.Object)
            return false;

        // Ref must have either type or name
        return @ref.TryGetProperty("type", out _) || @ref.TryGetProperty("name", out _);
    }

    private static bool HaveValidArgument(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("argument", out var argument))
            return false;

        if (argument.ValueKind != JsonValueKind.Object)
            return false;

        // Argument must have name and value
        return argument.TryGetProperty("name", out var name) &&
               name.ValueKind == JsonValueKind.String &&
               argument.TryGetProperty("value", out _);
    }

    private static bool HaveNoExtraParamProperties(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params))
            return true;

        var allowedProperties = new HashSet<string> { "ref", "argument" };
        
        foreach (var property in @params.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
                return false;
        }

        return true;
    }

    private static bool HaveRequestId(JsonElement element)
    {
        return element.TryGetProperty("id", out var id) &&
               (id.ValueKind == JsonValueKind.String || id.ValueKind == JsonValueKind.Number);
    }
}