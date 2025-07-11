using McpServer.Domain.Resources;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Resources;

/// <summary>
/// Base class for resource providers that use templates.
/// </summary>
public abstract class TemplateResourceProvider : ITemplateResourceProvider
{
    private readonly ILogger _logger;
    private readonly List<IResourceTemplate> _templates = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateResourceProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    protected TemplateResourceProvider(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc/>
    public IReadOnlyCollection<IResourceTemplate> Templates => _templates.AsReadOnly();
    
    /// <summary>
    /// Registers a template with this provider.
    /// </summary>
    /// <param name="template">The template to register.</param>
    protected void RegisterTemplate(IResourceTemplate template)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
            
        _templates.Add(template);
        _logger.LogDebug("Registered template: {TemplateName} with pattern: {Pattern}", 
            template.Name, template.UriPattern);
    }
    
    /// <inheritdoc/>
    public async Task<IEnumerable<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        var instances = await ListTemplateInstancesAsync(cancellationToken);
        
        return instances.Select(instance => new Resource
        {
            Uri = instance.Uri,
            Name = instance.DisplayName ?? GenerateResourceName(instance),
            Description = instance.Template.Description,
            MimeType = instance.Template.MimeType
        });
    }
    
    /// <inheritdoc/>
    public async Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(uri))
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));
            
        // Find matching template
        foreach (var template in _templates)
        {
            if (template.Matches(uri))
            {
                var parameters = template.ExtractParameters(uri);
                _logger.LogDebug("URI {Uri} matched template {TemplateName}", uri, template.Name);
                
                return await ReadTemplateResourceAsync(template, parameters, cancellationToken);
            }
        }
        
        throw new ResourceNotFoundException($"No template matches URI: {uri}");
    }
    
    /// <inheritdoc/>
    public virtual Task SubscribeToResourceAsync(string uri, IResourceObserver observer, CancellationToken cancellationToken = default)
    {
        // Default implementation doesn't support subscriptions
        throw new NotSupportedException("This provider does not support resource subscriptions");
    }
    
    /// <inheritdoc/>
    public virtual Task UnsubscribeFromResourceAsync(string uri, IResourceObserver observer, CancellationToken cancellationToken = default)
    {
        // Default implementation doesn't support subscriptions
        throw new NotSupportedException("This provider does not support resource subscriptions");
    }
    
    /// <inheritdoc/>
    public abstract Task<IEnumerable<TemplateResourceInstance>> ListTemplateInstancesAsync(CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract Task<ResourceContent> ReadTemplateResourceAsync(
        IResourceTemplate template,
        IDictionary<string, string> parameters,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a display name for a resource instance.
    /// </summary>
    /// <param name="instance">The template instance.</param>
    /// <returns>A display name.</returns>
    protected virtual string GenerateResourceName(TemplateResourceInstance instance)
    {
        // Default implementation combines template name with parameters
        var paramString = string.Join(", ", instance.Parameters.Select(p => $"{p.Key}={p.Value}"));
        return string.IsNullOrEmpty(paramString) 
            ? instance.Template.Name 
            : $"{instance.Template.Name} ({paramString})";
    }
}