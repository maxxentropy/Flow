using System.Diagnostics.CodeAnalysis;

namespace McpServer.Domain.Protocol;

/// <summary>
/// Interface for negotiating protocol versions between client and server.
/// </summary>
public interface IProtocolVersionNegotiator
{
    /// <summary>
    /// Gets the list of protocol versions supported by the server.
    /// </summary>
    IReadOnlyList<ProtocolVersion> SupportedVersions { get; }
    
    /// <summary>
    /// Gets the current/preferred protocol version.
    /// </summary>
    ProtocolVersion CurrentVersion { get; }
    
    /// <summary>
    /// Negotiates the protocol version to use based on client capabilities.
    /// </summary>
    /// <param name="clientVersion">The protocol version requested by the client.</param>
    /// <returns>The negotiated protocol version to use.</returns>
    /// <exception cref="ProtocolVersionException">Thrown when no compatible version can be negotiated.</exception>
    ProtocolVersion NegotiateVersion(string clientVersion);
    
    /// <summary>
    /// Checks if a specific protocol version is supported.
    /// </summary>
    /// <param name="version">The version to check.</param>
    /// <returns>True if the version is supported; otherwise, false.</returns>
    bool IsVersionSupported(string version);
    
    /// <summary>
    /// Tries to negotiate a protocol version without throwing exceptions.
    /// </summary>
    /// <param name="clientVersion">The protocol version requested by the client.</param>
    /// <param name="negotiatedVersion">The negotiated version if successful.</param>
    /// <returns>True if negotiation was successful; otherwise, false.</returns>
    bool TryNegotiateVersion(string clientVersion, [NotNullWhen(true)] out ProtocolVersion? negotiatedVersion);
}

/// <summary>
/// Represents a protocol version with semantic versioning.
/// </summary>
public record ProtocolVersion : IComparable<ProtocolVersion>
{
    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public int Major { get; }
    
    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    public int Minor { get; }
    
    /// <summary>
    /// Gets the patch version number.
    /// </summary>
    public int Patch { get; }
    
    /// <summary>
    /// Gets the full version string.
    /// </summary>
    public string Version => $"{Major}.{Minor}.{Patch}";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolVersion"/> class.
    /// </summary>
    /// <param name="major">The major version.</param>
    /// <param name="minor">The minor version.</param>
    /// <param name="patch">The patch version.</param>
    public ProtocolVersion(int major, int minor, int patch)
    {
        if (major < 0) throw new ArgumentException("Major version cannot be negative", nameof(major));
        if (minor < 0) throw new ArgumentException("Minor version cannot be negative", nameof(minor));
        if (patch < 0) throw new ArgumentException("Patch version cannot be negative", nameof(patch));
        
        Major = major;
        Minor = minor;
        Patch = patch;
    }
    
    /// <summary>
    /// Parses a version string into a ProtocolVersion.
    /// </summary>
    /// <param name="version">The version string to parse (e.g., "1.0.0").</param>
    /// <returns>The parsed protocol version.</returns>
    /// <exception cref="FormatException">Thrown when the version string is invalid.</exception>
    public static ProtocolVersion Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version string cannot be empty", nameof(version));
        
        var parts = version.Split('.');
        if (parts.Length != 3)
            throw new FormatException($"Invalid version format: {version}. Expected format: major.minor.patch");
        
        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
        {
            throw new FormatException($"Invalid version format: {version}. Version parts must be integers.");
        }
        
        return new ProtocolVersion(major, minor, patch);
    }
    
    /// <summary>
    /// Tries to parse a version string without throwing exceptions.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <param name="result">The parsed version if successful.</param>
    /// <returns>True if parsing was successful; otherwise, false.</returns>
    public static bool TryParse(string version, [NotNullWhen(true)] out ProtocolVersion? result)
    {
        result = null;
        
        if (string.IsNullOrWhiteSpace(version))
            return false;
        
        var parts = version.Split('.');
        if (parts.Length != 3)
            return false;
        
        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch) ||
            major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }
        
        result = new ProtocolVersion(major, minor, patch);
        return true;
    }
    
    /// <inheritdoc/>
    public int CompareTo(ProtocolVersion? other)
    {
        if (other is null) return 1;
        
        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0) return majorComparison;
        
        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0) return minorComparison;
        
        return Patch.CompareTo(other.Patch);
    }
    
    /// <summary>
    /// Checks if this version is compatible with another version.
    /// Compatible means same major version and this minor version >= other minor version.
    /// </summary>
    /// <param name="other">The other version to check compatibility with.</param>
    /// <returns>True if compatible; otherwise, false.</returns>
    public bool IsCompatibleWith(ProtocolVersion other)
    {
        if (other is null) return false;
        
        // Different major versions are incompatible
        if (Major != other.Major) return false;
        
        // Same major, this minor must be >= other minor for backward compatibility
        return Minor >= other.Minor;
    }
    
    /// <inheritdoc/>
    public override string ToString() => Version;
    
    // Operator overloads for comparison
    public static bool operator <(ProtocolVersion left, ProtocolVersion right)
        => left is null ? right is not null : left.CompareTo(right) < 0;
    
    public static bool operator <=(ProtocolVersion left, ProtocolVersion right)
        => left is null || left.CompareTo(right) <= 0;
    
    public static bool operator >(ProtocolVersion left, ProtocolVersion right)
        => left is not null && left.CompareTo(right) > 0;
    
    public static bool operator >=(ProtocolVersion left, ProtocolVersion right)
        => left is null ? right is null : left.CompareTo(right) >= 0;
}

/// <summary>
/// Exception thrown when protocol version negotiation fails.
/// </summary>
public class ProtocolVersionException : Exception
{
    /// <summary>
    /// Gets the client version that was requested.
    /// </summary>
    public string ClientVersion { get; }
    
    /// <summary>
    /// Gets the supported versions by the server.
    /// </summary>
    public IReadOnlyList<string> SupportedVersions { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolVersionException"/> class.
    /// </summary>
    /// <param name="clientVersion">The client version.</param>
    /// <param name="supportedVersions">The supported versions.</param>
    /// <param name="message">The error message.</param>
    public ProtocolVersionException(string clientVersion, IReadOnlyList<string> supportedVersions, string? message = null)
        : base(message ?? $"Protocol version '{clientVersion}' is not supported. Supported versions: {string.Join(", ", supportedVersions)}")
    {
        ClientVersion = clientVersion;
        SupportedVersions = supportedVersions;
    }
}