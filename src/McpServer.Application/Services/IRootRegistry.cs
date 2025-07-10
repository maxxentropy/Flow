using McpServer.Domain.Protocol.Messages;

namespace McpServer.Application.Services;

/// <summary>
/// Service for managing filesystem and resource roots that define server operation boundaries.
/// </summary>
public interface IRootRegistry
{
    /// <summary>
    /// Gets the current list of roots.
    /// </summary>
    IReadOnlyList<Root> Roots { get; }

    /// <summary>
    /// Gets whether any roots are currently configured.
    /// </summary>
    bool HasRoots { get; }

    /// <summary>
    /// Event triggered when the roots list changes.
    /// </summary>
    event EventHandler<RootsChangedEventArgs>? RootsChanged;

    /// <summary>
    /// Updates the list of roots. This completely replaces the existing roots.
    /// </summary>
    /// <param name="roots">The new list of roots.</param>
    void UpdateRoots(IEnumerable<Root> roots);

    /// <summary>
    /// Adds a root to the registry.
    /// </summary>
    /// <param name="root">The root to add.</param>
    void AddRoot(Root root);

    /// <summary>
    /// Removes a root from the registry by URI.
    /// </summary>
    /// <param name="uri">The URI of the root to remove.</param>
    /// <returns>True if the root was found and removed, false otherwise.</returns>
    bool RemoveRoot(string uri);

    /// <summary>
    /// Clears all roots from the registry.
    /// </summary>
    void ClearRoots();

    /// <summary>
    /// Checks if a given URI is within the boundaries of any configured root.
    /// </summary>
    /// <param name="uri">The URI to check.</param>
    /// <returns>True if the URI is within an allowed root, false otherwise.</returns>
    bool IsWithinRootBoundaries(string uri);

    /// <summary>
    /// Gets the root that contains the specified URI, if any.
    /// </summary>
    /// <param name="uri">The URI to find the containing root for.</param>
    /// <returns>The containing root, or null if no root contains this URI.</returns>
    Root? GetContainingRoot(string uri);

    /// <summary>
    /// Validates that a URI is within allowed boundaries and throws an exception if not.
    /// </summary>
    /// <param name="uri">The URI to validate.</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when the URI is outside allowed boundaries.</exception>
    void ValidateUriAccess(string uri);
}

/// <summary>
/// Event arguments for when the roots list changes.
/// </summary>
public class RootsChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RootsChangedEventArgs"/> class.
    /// </summary>
    /// <param name="previousRoots">The previous list of roots.</param>
    /// <param name="newRoots">The new list of roots.</param>
    public RootsChangedEventArgs(IReadOnlyList<Root> previousRoots, IReadOnlyList<Root> newRoots)
    {
        PreviousRoots = previousRoots;
        NewRoots = newRoots;
    }

    /// <summary>
    /// Gets the previous list of roots.
    /// </summary>
    public IReadOnlyList<Root> PreviousRoots { get; }

    /// <summary>
    /// Gets the new list of roots.
    /// </summary>
    public IReadOnlyList<Root> NewRoots { get; }
}