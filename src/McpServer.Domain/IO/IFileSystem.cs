namespace McpServer.Domain.IO;

/// <summary>
/// Abstraction for file system operations.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>True if the file exists.</returns>
    bool FileExists(string path);
    
    /// <summary>
    /// Checks if a directory exists.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>True if the directory exists.</returns>
    bool DirectoryExists(string path);
    
    /// <summary>
    /// Reads all text from a file asynchronously.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file contents.</returns>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reads all bytes from a file asynchronously.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file contents as bytes.</returns>
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets files in a directory.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="searchPattern">The search pattern.</param>
    /// <param name="searchOption">The search option.</param>
    /// <returns>File paths.</returns>
    IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption);
    
    /// <summary>
    /// Gets directories in a path.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="searchPattern">The search pattern.</param>
    /// <param name="searchOption">The search option.</param>
    /// <returns>Directory paths.</returns>
    IEnumerable<string> GetDirectories(string path, string searchPattern, SearchOption searchOption);
    
    /// <summary>
    /// Gets file information.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>File information.</returns>
    IFileInfo GetFileInfo(string path);
    
    /// <summary>
    /// Gets directory information.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>Directory information.</returns>
    IDirectoryInfo GetDirectoryInfo(string path);
}

/// <summary>
/// Abstraction for file information.
/// </summary>
public interface IFileInfo
{
    /// <summary>
    /// Gets the file name.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the full path.
    /// </summary>
    string FullName { get; }
    
    /// <summary>
    /// Gets the file extension.
    /// </summary>
    string Extension { get; }
    
    /// <summary>
    /// Gets the file length in bytes.
    /// </summary>
    long Length { get; }
    
    /// <summary>
    /// Gets the last write time.
    /// </summary>
    DateTime LastWriteTime { get; }
    
    /// <summary>
    /// Gets whether the file exists.
    /// </summary>
    bool Exists { get; }
}

/// <summary>
/// Abstraction for directory information.
/// </summary>
public interface IDirectoryInfo
{
    /// <summary>
    /// Gets the directory name.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the full path.
    /// </summary>
    string FullName { get; }
    
    /// <summary>
    /// Gets whether the directory exists.
    /// </summary>
    bool Exists { get; }
    
    /// <summary>
    /// Gets the parent directory.
    /// </summary>
    IDirectoryInfo? Parent { get; }
}

/// <summary>
/// Search options for file/directory enumeration.
/// </summary>
public enum SearchOption
{
    /// <summary>
    /// Search only the top directory.
    /// </summary>
    TopDirectoryOnly,
    
    /// <summary>
    /// Search all subdirectories.
    /// </summary>
    AllDirectories
}