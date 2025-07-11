using FluentValidation;
using System.Text.Json;

namespace McpServer.Domain.Validation.FluentValidators;

/// <summary>
/// Validator for resources/list requests.
/// </summary>
public class ResourcesListRequestValidator : AbstractValidator<JsonElement>
{
    public ResourcesListRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveResourcesListMethod)
            .WithMessage("Method must be 'resources/list'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Resources list request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveResourcesListMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "resources/list";
    }

    private static bool HaveRequestId(JsonElement element)
    {
        return element.TryGetProperty("id", out var id) &&
               (id.ValueKind == JsonValueKind.String || id.ValueKind == JsonValueKind.Number);
    }
}

/// <summary>
/// Validator for resources/subscribe requests.
/// </summary>
public class ResourcesSubscribeRequestValidator : AbstractValidator<JsonElement>
{
    public ResourcesSubscribeRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveResourcesSubscribeMethod)
            .WithMessage("Method must be 'resources/subscribe'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveParams)
            .WithMessage("Resources subscribe request must have 'params'")
            .WithErrorCode("missing_params");

        RuleFor(x => x)
            .Must(HaveValidUri)
            .WithMessage("Invalid or missing 'uri' in params")
            .WithErrorCode("invalid_uri");

        RuleFor(x => x)
            .Must(HaveNoExtraParamProperties)
            .WithMessage("Params contains unexpected properties")
            .WithErrorCode("unexpected_param_properties");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Resources subscribe request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveResourcesSubscribeMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "resources/subscribe";
    }

    private static bool HaveParams(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveValidUri(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("uri", out var uri))
            return false;

        if (uri.ValueKind != JsonValueKind.String)
            return false;

        var uriString = uri.GetString();
        if (string.IsNullOrEmpty(uriString))
            return false;

        return Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out _);
    }

    private static bool HaveNoExtraParamProperties(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params))
            return true;

        var allowedProperties = new HashSet<string> { "uri" };
        
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
/// Validator for resources/unsubscribe requests.
/// </summary>
public class ResourcesUnsubscribeRequestValidator : AbstractValidator<JsonElement>
{
    public ResourcesUnsubscribeRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveResourcesUnsubscribeMethod)
            .WithMessage("Method must be 'resources/unsubscribe'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveParams)
            .WithMessage("Resources unsubscribe request must have 'params'")
            .WithErrorCode("missing_params");

        RuleFor(x => x)
            .Must(HaveValidUri)
            .WithMessage("Invalid or missing 'uri' in params")
            .WithErrorCode("invalid_uri");

        RuleFor(x => x)
            .Must(HaveNoExtraParamProperties)
            .WithMessage("Params contains unexpected properties")
            .WithErrorCode("unexpected_param_properties");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Resources unsubscribe request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveResourcesUnsubscribeMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "resources/unsubscribe";
    }

    private static bool HaveParams(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveValidUri(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("uri", out var uri))
            return false;

        if (uri.ValueKind != JsonValueKind.String)
            return false;

        var uriString = uri.GetString();
        if (string.IsNullOrEmpty(uriString))
            return false;

        return Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out _);
    }

    private static bool HaveNoExtraParamProperties(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params))
            return true;

        var allowedProperties = new HashSet<string> { "uri" };
        
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