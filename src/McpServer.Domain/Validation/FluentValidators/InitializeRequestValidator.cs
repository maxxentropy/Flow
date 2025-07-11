using FluentValidation;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace McpServer.Domain.Validation.FluentValidators;

/// <summary>
/// Validator for MCP initialize requests.
/// </summary>
public class InitializeRequestValidator : AbstractValidator<JsonElement>
{
    private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

    public InitializeRequestValidator()
    {
        Include(new JsonRpcRequestValidator());

        RuleFor(x => x)
            .Must(HaveInitializeMethod)
            .WithMessage("Method must be 'initialize'")
            .WithErrorCode("invalid_method");

        RuleFor(x => x)
            .Must(HaveParams)
            .WithMessage("Initialize request must have 'params'")
            .WithErrorCode("missing_params");

        RuleFor(x => x)
            .Must(HaveValidProtocolVersion)
            .WithMessage("Invalid or missing 'protocolVersion' in params")
            .WithErrorCode("invalid_protocol_version");

        RuleFor(x => x)
            .Must(HaveValidCapabilities)
            .WithMessage("Invalid or missing 'capabilities' in params")
            .WithErrorCode("invalid_capabilities");

        RuleFor(x => x)
            .Must(HaveValidClientInfo)
            .WithMessage("Invalid or missing 'clientInfo' in params")
            .WithErrorCode("invalid_client_info");

        RuleFor(x => x)
            .Must(HaveNoExtraParamProperties)
            .WithMessage("Params contains unexpected properties")
            .WithErrorCode("unexpected_param_properties");
    }

    private static bool HaveInitializeMethod(JsonElement element)
    {
        return element.TryGetProperty("method", out var method) &&
               method.GetString() == "initialize";
    }

    private static bool HaveParams(JsonElement element)
    {
        return element.TryGetProperty("params", out var @params) &&
               @params.ValueKind == JsonValueKind.Object;
    }

    private static bool HaveValidProtocolVersion(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("protocolVersion", out var version))
            return false;

        if (version.ValueKind != JsonValueKind.String)
            return false;

        var versionString = version.GetString();
        return !string.IsNullOrEmpty(versionString) && 
               VersionPattern.IsMatch(versionString);
    }

    private static bool HaveValidCapabilities(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("capabilities", out var capabilities))
            return false;

        if (capabilities.ValueKind != JsonValueKind.Object)
            return false;

        // Validate optional roots capability
        if (capabilities.TryGetProperty("roots", out var roots))
        {
            if (roots.ValueKind != JsonValueKind.Object)
                return false;

            // Check that roots only contains expected properties
            foreach (var prop in roots.EnumerateObject())
            {
                if (prop.Name != "listChanged")
                    return false;

                if (prop.Name == "listChanged" && prop.Value.ValueKind != JsonValueKind.True && prop.Value.ValueKind != JsonValueKind.False)
                    return false;
            }
        }

        // Validate optional sampling capability
        if (capabilities.TryGetProperty("sampling", out var sampling))
        {
            if (sampling.ValueKind != JsonValueKind.Object)
                return false;
        }

        // Check for no extra properties in capabilities
        foreach (var prop in capabilities.EnumerateObject())
        {
            if (prop.Name != "roots" && prop.Name != "sampling")
                return false;
        }

        return true;
    }

    private static bool HaveValidClientInfo(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params) ||
            !@params.TryGetProperty("clientInfo", out var clientInfo))
            return false;

        if (clientInfo.ValueKind != JsonValueKind.Object)
            return false;

        // Check required fields
        if (!clientInfo.TryGetProperty("name", out var name) ||
            name.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(name.GetString()))
            return false;

        if (!clientInfo.TryGetProperty("version", out var version) ||
            version.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(version.GetString()))
            return false;

        // Check for no extra properties
        foreach (var prop in clientInfo.EnumerateObject())
        {
            if (prop.Name != "name" && prop.Name != "version")
                return false;
        }

        return true;
    }

    private static bool HaveNoExtraParamProperties(JsonElement element)
    {
        if (!element.TryGetProperty("params", out var @params))
            return true;

        var allowedProperties = new HashSet<string> { "protocolVersion", "capabilities", "clientInfo" };
        
        foreach (var property in @params.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
                return false;
        }

        return true;
    }
}