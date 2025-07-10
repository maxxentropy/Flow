namespace McpServer.Domain.Resources;

/// <summary>
/// Represents a resource provider that can list and read resources.
/// </summary>
public interface IResourceProvider
{
    /// <summary>
    /// Lists all available resources.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of available resources.</returns>
    Task<IEnumerable<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the content of a specific resource.
    /// </summary>
    /// <param name="uri">The URI of the resource to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resource content.</returns>
    Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to updates for a specific resource.
    /// </summary>
    /// <param name="uri">The URI of the resource to subscribe to.</param>
    /// <param name="observer">The observer to notify of updates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the subscription operation.</returns>
    Task SubscribeToResourceAsync(string uri, IResourceObserver observer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from updates for a specific resource.
    /// </summary>
    /// <param name="uri">The URI of the resource to unsubscribe from.</param>
    /// <param name="observer">The observer to remove.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the unsubscription operation.</returns>
    Task UnsubscribeFromResourceAsync(string uri, IResourceObserver observer, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a resource that can be accessed by the MCP server.
/// </summary>
public record Resource
{
    /// <summary>
    /// Gets the URI of the resource.
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Gets the human-readable name of the resource.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the description of the resource.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the MIME type of the resource.
    /// </summary>
    public string? MimeType { get; init; }
}

/// <summary>
/// Represents the content of a resource.
/// </summary>
public record ResourceContent
{
    /// <summary>
    /// Gets the URI of the resource.
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Gets the MIME type of the content.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Gets the text content if the resource is text-based.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets the base64-encoded blob content if the resource is binary.
    /// </summary>
    public string? Blob { get; init; }
}

/// <summary>
/// Observer interface for resource updates.
/// </summary>
public interface IResourceObserver
{
    /// <summary>
    /// Called when a resource is updated.
    /// </summary>
    /// <param name="uri">The URI of the updated resource.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the notification operation.</returns>
    Task OnResourceUpdatedAsync(string uri, CancellationToken cancellationToken = default);
}