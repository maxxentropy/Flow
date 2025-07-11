using System.Collections.Concurrent;

namespace McpServer.Infrastructure.Resources;

/// <summary>
/// Provides access to file system resources.
/// </summary>
public class FileSystemResourceProvider : IResourceProvider, IDisposable
{
    private readonly ILogger<FileSystemResourceProvider> _logger;
    private readonly IOptions<FileSystemResourceOptions> _options;
    private readonly ConcurrentDictionary<string, HashSet<IResourceObserver>> _observers = new();
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemResourceProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>
    public FileSystemResourceProvider(ILogger<FileSystemResourceProvider> logger, IOptions<FileSystemResourceOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        var resources = new List<Resource>();

        foreach (var rootPath in _options.Value.AllowedPaths)
        {
            if (!Directory.Exists(rootPath))
            {
                _logger.LogWarning("Root path does not exist: {Path}", rootPath);
                continue;
            }

            try
            {
                await AddDirectoryResourcesAsync(rootPath, resources, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing resources in path: {Path}", rootPath);
            }
        }

        return resources;
    }

    /// <inheritdoc/>
    public Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (!TryParseFileUri(uri, out var filePath))
        {
            throw new ResourceException(uri, "Invalid file URI format");
        }

        if (!IsPathAllowed(filePath))
        {
            throw new ResourceException(uri, "Access denied to this path");
        }

        if (!File.Exists(filePath))
        {
            throw new ResourceException(uri, "File not found");
        }

        try
        {
            var mimeType = GetMimeType(filePath);
            var isText = IsTextFile(mimeType);

            if (isText)
            {
                var text = File.ReadAllText(filePath);
                return Task.FromResult(new ResourceContent
                {
                    Uri = uri,
                    MimeType = mimeType,
                    Text = text
                });
            }
            else
            {
                var bytes = File.ReadAllBytes(filePath);
                var base64 = Convert.ToBase64String(bytes);
                return Task.FromResult(new ResourceContent
                {
                    Uri = uri,
                    MimeType = mimeType,
                    Blob = base64
                });
            }
        }
        catch (Exception ex) when (ex is not ResourceException)
        {
            throw new ResourceException(uri, $"Failed to read file: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public Task SubscribeToResourceAsync(string uri, IResourceObserver observer, CancellationToken cancellationToken = default)
    {
        if (!TryParseFileUri(uri, out var filePath))
        {
            throw new ResourceException(uri, "Invalid file URI format");
        }

        if (!IsPathAllowed(filePath))
        {
            throw new ResourceException(uri, "Access denied to this path");
        }

        if (!File.Exists(filePath))
        {
            throw new ResourceException(uri, "File not found");
        }

        lock (_observers)
        {
            if (!_observers.TryGetValue(uri, out var observers))
            {
                observers = new HashSet<IResourceObserver>();
                _observers[uri] = observers;
                
                // Create file watcher
                CreateFileWatcher(uri, filePath);
            }
            
            observers.Add(observer);
        }

        _logger.LogInformation("Subscribed to file resource: {Uri}", uri);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnsubscribeFromResourceAsync(string uri, IResourceObserver observer, CancellationToken cancellationToken = default)
    {
        lock (_observers)
        {
            if (_observers.TryGetValue(uri, out var observers))
            {
                observers.Remove(observer);
                
                if (observers.Count == 0)
                {
                    _observers.TryRemove(uri, out _);
                    
                    // Remove file watcher
                    if (_watchers.TryRemove(uri, out var watcher))
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                }
            }
        }

        _logger.LogInformation("Unsubscribed from file resource: {Uri}", uri);
        return Task.CompletedTask;
    }

    private async Task AddDirectoryResourcesAsync(string path, List<Resource> resources, CancellationToken cancellationToken)
    {
        // Add files
        foreach (var file in Directory.GetFiles(path))
        {
            if (ShouldIncludeFile(file))
            {
                var uri = $"file://{file.Replace('\\', '/')}";
                resources.Add(new Resource
                {
                    Uri = uri,
                    Name = Path.GetFileName(file),
                    Description = $"File: {file}",
                    MimeType = GetMimeType(file)
                });
            }
        }

        // Recursively add subdirectories if enabled
        if (_options.Value.RecursiveSearch)
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                await AddDirectoryResourcesAsync(directory, resources, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool TryParseFileUri(string uri, out string filePath)
    {
        filePath = string.Empty;
        
        if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var path = uri.Substring(7);
            filePath = Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsPathAllowed(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return _options.Value.AllowedPaths.Any(allowedPath => 
            fullPath.StartsWith(Path.GetFullPath(allowedPath), StringComparison.OrdinalIgnoreCase));
    }

    private bool ShouldIncludeFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        // Check excluded patterns
        if (_options.Value.ExcludePatterns?.Any(pattern => 
            fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return false;
        }

        // Check included extensions
        if (_options.Value.IncludeExtensions?.Length > 0)
        {
            var extension = Path.GetExtension(filePath);
            return _options.Value.IncludeExtensions.Any(ext => 
                extension.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".cs" => "text/x-csharp",
            ".md" => "text/markdown",
            ".yml" or ".yaml" => "text/yaml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    private static bool IsTextFile(string mimeType)
    {
        return mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || 
               mimeType == "application/json" ||
               mimeType == "application/xml" ||
               mimeType == "image/svg+xml";
    }

    private void CreateFileWatcher(string uri, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileName(filePath);

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        watcher.Changed += (sender, e) => OnFileChanged(uri);
        watcher.EnableRaisingEvents = true;

        _watchers.TryAdd(uri, watcher);
    }

    private async void OnFileChanged(string uri)
    {
        _logger.LogInformation("File changed: {Uri}", uri);

        HashSet<IResourceObserver> observersCopy;
        lock (_observers)
        {
            if (!_observers.TryGetValue(uri, out var observers))
            {
                return;
            }
            observersCopy = new HashSet<IResourceObserver>(observers);
        }

        foreach (var observer in observersCopy)
        {
            try
            {
                await observer.OnResourceUpdatedAsync(uri).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying observer of resource update");
            }
        }
    }
    
    /// <summary>
    /// Disposes the resource provider and all watchers.
    /// </summary>
    public void Dispose()
    {
        foreach (var watcher in _watchers.Values)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing file watcher");
            }
        }
        
        _watchers.Clear();
        _observers.Clear();
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration options for the file system resource provider.
/// </summary>
public class FileSystemResourceOptions
{
    /// <summary>
    /// Gets or sets the allowed root paths.
    /// </summary>
    public string[] AllowedPaths { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets whether to search recursively.
    /// </summary>
    public bool RecursiveSearch { get; set; } = true;

    /// <summary>
    /// Gets or sets the file extensions to include.
    /// </summary>
    public string[]? IncludeExtensions { get; set; }

    /// <summary>
    /// Gets or sets the patterns to exclude.
    /// </summary>
    public string[]? ExcludePatterns { get; set; }
}