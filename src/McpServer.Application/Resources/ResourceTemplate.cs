using System.Text.RegularExpressions;
using McpServer.Domain.Resources;

namespace McpServer.Application.Resources;

/// <summary>
/// Default implementation of a resource template.
/// </summary>
public partial class ResourceTemplate : IResourceTemplate
{
    private readonly Regex _patternRegex;
    private readonly List<string> _parameterNames;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceTemplate"/> class.
    /// </summary>
    /// <param name="uriPattern">The URI pattern with placeholders.</param>
    /// <param name="name">The template name.</param>
    /// <param name="description">The template description.</param>
    /// <param name="mimeType">The MIME type for generated resources.</param>
    public ResourceTemplate(string uriPattern, string name, string? description = null, string? mimeType = null)
    {
        UriPattern = uriPattern ?? throw new ArgumentNullException(nameof(uriPattern));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
        MimeType = mimeType;
        
        // Extract parameter names and build regex pattern
        _parameterNames = new List<string>();
        var regexPattern = ParameterRegex().Replace(uriPattern, match =>
        {
            var paramName = match.Groups[1].Value;
            _parameterNames.Add(paramName);
            return $"(?<{paramName}>[^/]+)";
        });
        
        // Escape special regex characters except for our parameter groups
        regexPattern = "^" + Regex.Escape(regexPattern)
            .Replace(@"\(\?<", "(?<")
            .Replace(@"\>", ">")
            .Replace(@"\[", "[")
            .Replace(@"\]", "]")
            .Replace(@"\+", "+")
            .Replace(@"\)", ")") + "$";
            
        _patternRegex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
    
    /// <inheritdoc/>
    public string UriPattern { get; }
    
    /// <inheritdoc/>
    public string Name { get; }
    
    /// <inheritdoc/>
    public string? Description { get; }
    
    /// <inheritdoc/>
    public string? MimeType { get; }
    
    /// <inheritdoc/>
    public bool Matches(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return false;
            
        return _patternRegex.IsMatch(uri);
    }
    
    /// <inheritdoc/>
    public IDictionary<string, string> ExtractParameters(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return new Dictionary<string, string>();
            
        var match = _patternRegex.Match(uri);
        if (!match.Success)
            return new Dictionary<string, string>();
            
        var parameters = new Dictionary<string, string>();
        foreach (var paramName in _parameterNames)
        {
            var group = match.Groups[paramName];
            if (group.Success)
            {
                parameters[paramName] = Uri.UnescapeDataString(group.Value);
            }
        }
        
        return parameters;
    }
    
    /// <inheritdoc/>
    public string GenerateUri(IDictionary<string, string> parameters)
    {
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));
            
        var uri = UriPattern;
        foreach (var paramName in _parameterNames)
        {
            if (parameters.TryGetValue(paramName, out var value))
            {
                uri = uri.Replace($"{{{paramName}}}", Uri.EscapeDataString(value));
            }
            else
            {
                throw new ArgumentException($"Missing required parameter: {paramName}");
            }
        }
        
        return uri;
    }
    
    [GeneratedRegex(@"\{([^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex ParameterRegex();
}

/// <summary>
/// Builder for creating resource templates.
/// </summary>
public class ResourceTemplateBuilder
{
    private string? _uriPattern;
    private string? _name;
    private string? _description;
    private string? _mimeType;
    
    /// <summary>
    /// Sets the URI pattern.
    /// </summary>
    public ResourceTemplateBuilder WithUriPattern(string pattern)
    {
        _uriPattern = pattern;
        return this;
    }
    
    /// <summary>
    /// Sets the template name.
    /// </summary>
    public ResourceTemplateBuilder WithName(string name)
    {
        _name = name;
        return this;
    }
    
    /// <summary>
    /// Sets the template description.
    /// </summary>
    public ResourceTemplateBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }
    
    /// <summary>
    /// Sets the MIME type.
    /// </summary>
    public ResourceTemplateBuilder WithMimeType(string mimeType)
    {
        _mimeType = mimeType;
        return this;
    }
    
    /// <summary>
    /// Builds the resource template.
    /// </summary>
    public ResourceTemplate Build()
    {
        if (string.IsNullOrEmpty(_uriPattern))
            throw new InvalidOperationException("URI pattern is required");
            
        if (string.IsNullOrEmpty(_name))
            throw new InvalidOperationException("Template name is required");
            
        return new ResourceTemplate(_uriPattern, _name, _description, _mimeType);
    }
}