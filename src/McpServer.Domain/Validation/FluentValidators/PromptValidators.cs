using FluentValidation;
using System.Text.Json;

namespace McpServer.Domain.Validation.FluentValidators;

/// <summary>
/// Validator for prompts/list requests.
/// </summary>
public class PromptsListRequestValidator : AbstractValidator<JsonElement>
{
    public PromptsListRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HavePromptsListMethod)
            .WithMessage("Method must be 'prompts/list'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Prompts list request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HavePromptsListMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "prompts/list";
    }

    private static bool HaveRequestId(JsonElement element)
    {
        return element.TryGetProperty("id", out var id) &&
               (id.ValueKind == JsonValueKind.String || id.ValueKind == JsonValueKind.Number);
    }
}

/// <summary>
/// Validator for prompts/get requests.
/// </summary>
public class PromptsGetRequestValidator : AbstractValidator<JsonElement>
{
    public PromptsGetRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HavePromptsGetMethod)
            .WithMessage("Method must be 'prompts/get'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveParams)
            .WithMessage("Prompts get request must have 'params'")
            .WithErrorCode("missing_params");

        RuleFor(x => x)
            .Must(HaveValidName)
            .WithMessage("Invalid or missing 'name' in params")
            .WithErrorCode("invalid_prompt_name");

        RuleFor(x => x)
            .Must(HaveValidArguments)
            .When(HasArguments)
            .WithMessage("'arguments' must be an object when present")
            .WithErrorCode("invalid_arguments");

        RuleFor(x => x)
            .Must(HaveNoExtraParamProperties)
            .WithMessage("Params contains unexpected properties")
            .WithErrorCode("unexpected_param_properties");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Prompts get request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HavePromptsGetMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "prompts/get";
    }

    private static bool HaveParams(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveValidName(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("name", out var name))
            return false;

        return name.ValueKind == JsonValueKind.String &&
               !string.IsNullOrEmpty(name.GetString());
    }

    private static bool HasArguments(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.TryGetProperty("arguments", out _);
    }

    private static bool HaveValidArguments(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("arguments", out var arguments))
            return true;

        return arguments.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveNoExtraParamProperties(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params))
            return true;

        var allowedProperties = new HashSet<string> { "name", "arguments" };
        
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