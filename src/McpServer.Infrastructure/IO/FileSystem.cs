using McpServer.Domain.IO;

namespace McpServer.Infrastructure.IO;

/// <summary>
/// Default implementation of file system operations.
/// </summary>
public class FileSystem : IFileSystem
{
    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);
    
    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);
    
    /// <inheritdoc/>
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllTextAsync(path, cancellationToken);
    
    /// <inheritdoc/>
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllBytesAsync(path, cancellationToken);
    
    /// <inheritdoc/>
    public IEnumerable<string> GetFiles(string path, string searchPattern, Domain.IO.SearchOption searchOption)
        => Directory.GetFiles(path, searchPattern, (System.IO.SearchOption)searchOption);
    
    /// <inheritdoc/>
    public IEnumerable<string> GetDirectories(string path, string searchPattern, Domain.IO.SearchOption searchOption)
        => Directory.GetDirectories(path, searchPattern, (System.IO.SearchOption)searchOption);
    
    /// <inheritdoc/>
    public IFileInfo GetFileInfo(string path) => new FileInfoWrapper(new FileInfo(path));
    
    /// <inheritdoc/>
    public IDirectoryInfo GetDirectoryInfo(string path) => new DirectoryInfoWrapper(new DirectoryInfo(path));
}

/// <summary>
/// Wrapper for FileInfo.
/// </summary>
internal class FileInfoWrapper : IFileInfo
{
    private readonly FileInfo _fileInfo;
    
    public FileInfoWrapper(FileInfo fileInfo)
    {
        _fileInfo = fileInfo;
    }
    
    public string Name => _fileInfo.Name;
    public string FullName => _fileInfo.FullName;
    public string Extension => _fileInfo.Extension;
    public long Length => _fileInfo.Length;
    public DateTime LastWriteTime => _fileInfo.LastWriteTime;
    public bool Exists => _fileInfo.Exists;
}

/// <summary>
/// Wrapper for DirectoryInfo.
/// </summary>
internal class DirectoryInfoWrapper : IDirectoryInfo
{
    private readonly DirectoryInfo _directoryInfo;
    
    public DirectoryInfoWrapper(DirectoryInfo directoryInfo)
    {
        _directoryInfo = directoryInfo;
    }
    
    public string Name => _directoryInfo.Name;
    public string FullName => _directoryInfo.FullName;
    public bool Exists => _directoryInfo.Exists;
    public IDirectoryInfo? Parent => _directoryInfo.Parent != null 
        ? new DirectoryInfoWrapper(_directoryInfo.Parent) 
        : null;
}