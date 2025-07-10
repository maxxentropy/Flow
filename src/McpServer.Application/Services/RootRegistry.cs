using System.Collections.Concurrent;
using McpServer.Domain.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Default implementation of the root registry service.
/// </summary>
public class RootRegistry : IRootRegistry
{
    private readonly ILogger<RootRegistry> _logger;
    private readonly object _lock = new();
    private volatile List<Root> _roots = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RootRegistry"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public RootRegistry(ILogger<RootRegistry> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Root> Roots
    {
        get
        {
            lock (_lock)
            {
                return _roots.ToList().AsReadOnly();
            }
        }
    }

    /// <inheritdoc/>
    public bool HasRoots
    {
        get
        {
            lock (_lock)
            {
                return _roots.Count > 0;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<RootsChangedEventArgs>? RootsChanged;

    /// <inheritdoc/>
    public void UpdateRoots(IEnumerable<Root> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        var newRoots = roots.ToList();
        var previousRoots = Roots;

        lock (_lock)
        {
            _roots = newRoots;
        }

        _logger.LogInformation("Updated roots list: {RootCount} roots", newRoots.Count);
        
        foreach (var root in newRoots)
        {
            _logger.LogDebug("Root: {Uri} ({Name})", root.Uri, root.Name ?? "unnamed");
        }

        OnRootsChanged(previousRoots, newRoots);
    }

    /// <inheritdoc/>
    public void AddRoot(Root root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var previousRoots = Roots;

        lock (_lock)
        {
            if (!_roots.Any(r => string.Equals(r.Uri, root.Uri, StringComparison.OrdinalIgnoreCase)))
            {
                _roots = new List<Root>(_roots) { root };
            }
        }

        _logger.LogInformation("Added root: {Uri} ({Name})", root.Uri, root.Name ?? "unnamed");

        OnRootsChanged(previousRoots, Roots);
    }

    /// <inheritdoc/>
    public bool RemoveRoot(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        var previousRoots = Roots;
        bool removed;

        lock (_lock)
        {
            var rootToRemove = _roots.FirstOrDefault(r => string.Equals(r.Uri, uri, StringComparison.OrdinalIgnoreCase));
            if (rootToRemove != null)
            {
                _roots = _roots.Where(r => r != rootToRemove).ToList();
                removed = true;
            }
            else
            {
                removed = false;
            }
        }

        if (removed)
        {
            _logger.LogInformation("Removed root: {Uri}", uri);
            OnRootsChanged(previousRoots, Roots);
        }

        return removed;
    }

    /// <inheritdoc/>
    public void ClearRoots()
    {
        var previousRoots = Roots;

        lock (_lock)
        {
            _roots = new List<Root>();
        }

        _logger.LogInformation("Cleared all roots");

        OnRootsChanged(previousRoots, Roots);
    }

    /// <inheritdoc/>
    public bool IsWithinRootBoundaries(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        lock (_lock)
        {
            // If no roots are configured, allow all access (backward compatibility)
            if (_roots.Count == 0)
            {
                return true;
            }

            return _roots.Any(root => IsUriWithinRoot(uri, root.Uri));
        }
    }

    /// <inheritdoc/>
    public Root? GetContainingRoot(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        lock (_lock)
        {
            return _roots.FirstOrDefault(root => IsUriWithinRoot(uri, root.Uri));
        }
    }

    /// <inheritdoc/>
    public void ValidateUriAccess(string uri)
    {
        if (!IsWithinRootBoundaries(uri))
        {
            throw new UnauthorizedAccessException($"Access to URI '{uri}' is not allowed. The URI is outside of configured root boundaries.");
        }
    }

    private static bool IsUriWithinRoot(string uri, string rootUri)
    {
        // Normalize URIs for comparison
        var normalizedUri = NormalizeUri(uri);
        var normalizedRoot = NormalizeUri(rootUri);

        // For file:// URIs, ensure the path is within the root directory
        if (normalizedRoot.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && 
            normalizedUri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return IsFilePathWithinRoot(normalizedUri, normalizedRoot);
        }

        // For other URI schemes, check if the URI starts with the root URI
        return normalizedUri.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFilePathWithinRoot(string fileUri, string rootUri)
    {
        try
        {
            var filePath = new Uri(fileUri).LocalPath;
            var rootPath = new Uri(rootUri).LocalPath;

            // Normalize paths (handle case sensitivity based on OS)
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            
            // Ensure both paths end with directory separator for proper comparison
            if (!rootPath.EndsWith(Path.DirectorySeparatorChar))
            {
                rootPath += Path.DirectorySeparatorChar;
            }

            return filePath.StartsWith(rootPath, comparison) || 
                   string.Equals(filePath, rootPath.TrimEnd(Path.DirectorySeparatorChar), comparison);
        }
        catch (UriFormatException)
        {
            // If URI parsing fails, fall back to string comparison
            return fileUri.StartsWith(rootUri, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string NormalizeUri(string uri)
    {
        // Basic URI normalization
        if (string.IsNullOrWhiteSpace(uri))
        {
            return string.Empty;
        }

        // Ensure trailing slash for directory URIs
        if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var parsedUri = new Uri(uri);
                return parsedUri.ToString();
            }
            catch (UriFormatException)
            {
                return uri;
            }
        }

        return uri;
    }

    private void OnRootsChanged(IReadOnlyList<Root> previousRoots, IReadOnlyList<Root> newRoots)
    {
        try
        {
            RootsChanged?.Invoke(this, new RootsChangedEventArgs(previousRoots, newRoots));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while notifying roots changed event handlers");
        }
    }
}