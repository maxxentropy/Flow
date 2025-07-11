using FluentValidation;
using System.Text.Json;

namespace McpServer.Domain.Validation.FluentValidators;

/// <summary>
/// Factory for creating appropriate validators for MCP messages.
/// </summary>
public static class McpMessageValidatorFactory
{
    private static readonly Dictionary<string, Lazy<IValidator<JsonElement>>> Validators = new()
    {
        ["jsonrpc_request"] = new Lazy<IValidator<JsonElement>>(() => new JsonRpcRequestValidator()),
        ["jsonrpc_response"] = new Lazy<IValidator<JsonElement>>(() => new JsonRpcResponseValidator()),
        ["initialize"] = new Lazy<IValidator<JsonElement>>(() => new InitializeRequestValidator()),
        ["tools/call"] = new Lazy<IValidator<JsonElement>>(() => new ToolsCallRequestValidator()),
        ["tools/list"] = new Lazy<IValidator<JsonElement>>(() => new ToolsListRequestValidator()),
        ["resources/read"] = new Lazy<IValidator<JsonElement>>(() => new ResourcesReadRequestValidator()),
        ["resources/list"] = new Lazy<IValidator<JsonElement>>(() => new ResourcesListRequestValidator()),
        ["resources/subscribe"] = new Lazy<IValidator<JsonElement>>(() => new ResourcesSubscribeRequestValidator()),
        ["resources/unsubscribe"] = new Lazy<IValidator<JsonElement>>(() => new ResourcesUnsubscribeRequestValidator()),
        ["prompts/list"] = new Lazy<IValidator<JsonElement>>(() => new PromptsListRequestValidator()),
        ["prompts/get"] = new Lazy<IValidator<JsonElement>>(() => new PromptsGetRequestValidator()),
        ["ping"] = new Lazy<IValidator<JsonElement>>(() => new PingRequestValidator()),
        ["cancel"] = new Lazy<IValidator<JsonElement>>(() => new CancelRequestValidator()),
        ["logging/setLevel"] = new Lazy<IValidator<JsonElement>>(() => new LoggingSetLevelRequestValidator()),
        ["roots/list"] = new Lazy<IValidator<JsonElement>>(() => new RootsListRequestValidator()),
        ["completion/complete"] = new Lazy<IValidator<JsonElement>>(() => new CompletionCompleteRequestValidator()),
    };

    /// <summary>
    /// Gets a validator for the specified message type.
    /// </summary>
    /// <param name="messageType">The type of message to validate.</param>
    /// <returns>The appropriate validator, or null if no specific validator exists.</returns>
    public static IValidator<JsonElement>? GetValidator(string messageType)
    {
        return Validators.TryGetValue(messageType, out var lazyValidator) 
            ? lazyValidator.Value 
            : null;
    }

    /// <summary>
    /// Gets all supported message types.
    /// </summary>
    /// <returns>List of message types with validators.</returns>
    public static IEnumerable<string> GetSupportedMessageTypes()
    {
        return Validators.Keys.Where(k => k != "jsonrpc_request" && k != "jsonrpc_response");
    }
}

/// <summary>
/// Validator for resources/read requests.
/// </summary>
public class ResourcesReadRequestValidator : AbstractValidator<JsonElement>
{
    public ResourcesReadRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveResourcesReadMethod)
            .WithMessage("Method must be 'resources/read'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveParams)
            .WithMessage("Resources read request must have 'params'")
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
            .WithMessage("Resources read request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveResourcesReadMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "resources/read";
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

        // Basic URI validation - could be enhanced
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
/// Validator for ping requests.
/// </summary>
public class PingRequestValidator : AbstractValidator<JsonElement>
{
    public PingRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HavePingMethod)
            .WithMessage("Method must be 'ping'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveValidTimestamp)
            .When(HasParams)
            .WithMessage("Invalid timestamp in params")
            .WithErrorCode("invalid_timestamp");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Ping request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HavePingMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "ping";
    }

    private static bool HasParams(JsonElement element)
    {
        return element.TryGetProperty("params", out _);
    }

    private static bool HaveValidTimestamp(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params))
            return true;

        if (@params.ValueKind != JsonValueKind.Object)
            return false;

        if (@params.TryGetProperty("timestamp", out var timestamp))
        {
            return timestamp.ValueKind == JsonValueKind.Number;
        }

        // Timestamp is optional
        return true;
    }

    private static bool HaveRequestId(JsonElement element)
    {
        return element.TryGetProperty("id", out var id) &&
               (id.ValueKind == JsonValueKind.String || id.ValueKind == JsonValueKind.Number);
    }
}

/// <summary>
/// Validator for cancel requests.
/// </summary>
public class CancelRequestValidator : AbstractValidator<JsonElement>
{
    public CancelRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveCancelMethod)
            .WithMessage("Method must be 'cancel'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveParams)
            .WithMessage("Cancel request must have 'params'")
            .WithErrorCode("missing_params");

        RuleFor(x => x)
            .Must(HaveValidRequestId)
            .WithMessage("Invalid or missing 'requestId' in params")
            .WithErrorCode("invalid_request_id");

        RuleFor(x => x)
            .Must(HaveValidReason)
            .When(HasReason)
            .WithMessage("'reason' must be a string when present")
            .WithErrorCode("invalid_reason");

        RuleFor(x => x)
            .Must(HaveNoExtraParamProperties)
            .WithMessage("Params contains unexpected properties")
            .WithErrorCode("unexpected_param_properties");

        RuleFor(x => x)
            .Must(HaveRequestId)
            .WithMessage("Cancel request must have an 'id'")
            .WithErrorCode("missing_id");
    }

    private static bool HaveCancelMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "cancel";
    }

    private static bool HaveParams(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveValidRequestId(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("requestId", out var requestId))
            return false;

        return requestId.ValueKind == JsonValueKind.String ||
               requestId.ValueKind == JsonValueKind.Number;
    }

    private static bool HasReason(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.TryGetProperty("reason", out _);
    }

    private static bool HaveValidReason(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("reason", out var reason))
            return true;

        return reason.ValueKind == JsonValueKind.String;
    }

    private static bool HaveNoExtraParamProperties(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params))
            return true;

        var allowedProperties = new HashSet<string> { "requestId", "reason" };
        
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