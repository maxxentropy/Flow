using FluentValidation;
using System.Text.Json;

namespace McpServer.Domain.Validation.FluentValidators;

/// <summary>
/// Validator for MCP tools/call requests.
/// </summary>
public class ToolsCallRequestValidator : AbstractValidator<JsonElement>
{
    public ToolsCallRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveToolsCallMethod)
            .WithMessage("Method must be 'tools/call'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveParams)
            .WithMessage("Tools call request must have 'params'")
            .WithErrorCode("missing_params");

        RuleFor(x => x)
            .Must(HaveValidToolName)
            .WithMessage("Invalid or missing 'name' in params")
            .WithErrorCode("invalid_tool_name");

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
            .WithMessage("Tools call request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveToolsCallMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "tools/call";
    }

    private static bool HaveParams(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveValidToolName(JsonElement element)
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

/// <summary>
/// Validator for tool arguments based on a tool schema.
/// </summary>
public class ToolArgumentsValidator : AbstractValidator<Dictionary<string, object?>>
{
    public ToolArgumentsValidator(Domain.Tools.ToolSchema schema)
    {
        // Validate required properties
        if (schema.Required != null)
        {
            foreach (var requiredProp in schema.Required)
            {
                RuleFor(x => x)
                    .Must(args => args != null && args.ContainsKey(requiredProp))
                    .WithMessage($"Required property '{requiredProp}' is missing")
                    .WithErrorCode("missing_required_property");
            }
        }

        // Validate additional properties setting
        if (schema.AdditionalProperties == false)
        {
            RuleFor(x => x)
                .Must(args => ValidateNoExtraProperties(args, schema))
                .WithMessage("Arguments contain unexpected properties")
                .WithErrorCode("unexpected_properties");
        }

        // Add property-specific validations based on schema
        if (schema.Properties != null && schema.Properties.Count > 0)
        {
            RuleFor(x => x)
                .Must(args => ValidatePropertyTypes(args, schema))
                .WithMessage("One or more properties have invalid types")
                .WithErrorCode("invalid_property_type");
        }
    }

    private static bool ValidateNoExtraProperties(Dictionary<string, object?>? args, Domain.Tools.ToolSchema schema)
    {
        if (args == null || schema.Properties == null)
            return true;

        var allowedProperties = schema.Properties.Keys.ToHashSet();
        
        foreach (var key in args.Keys)
        {
            if (!allowedProperties.Contains(key))
                return false;
        }

        return true;
    }

    private static bool ValidatePropertyTypes(Dictionary<string, object?>? args, Domain.Tools.ToolSchema schema)
    {
        if (args == null || schema.Properties == null)
            return true;

        foreach (var (propName, propValue) in args)
        {
            if (schema.Properties.TryGetValue(propName, out var propSchema) && propSchema is JsonElement schemaElement)
            {
                if (!ValidatePropertyType(propValue, schemaElement))
                    return false;
            }
        }

        return true;
    }

    private static bool ValidatePropertyType(object? value, JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var typeElement))
            return true;

        var expectedType = typeElement.GetString();
        
        return expectedType switch
        {
            "string" => value is string,
            "number" => value is int or long or float or double or decimal,
            "integer" => value is int or long,
            "boolean" => value is bool,
            "object" => value is Dictionary<string, object?> or JsonElement { ValueKind: JsonValueKind.Object },
            "array" => value is System.Collections.IEnumerable and not string,
            "null" => value == null,
            _ => true
        };
    }
}