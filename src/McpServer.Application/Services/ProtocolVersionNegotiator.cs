using System.Diagnostics.CodeAnalysis;
using McpServer.Domain.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Application.Services;

/// <summary>
/// Configuration for protocol version negotiation.
/// </summary>
public class ProtocolVersionConfiguration
{
    /// <summary>
    /// Gets or sets the supported protocol versions.
    /// </summary>
    public List<string> SupportedVersions { get; set; } = ["0.1.0", "0.2.0", "1.0.0"];
    
    /// <summary>
    /// Gets or sets the current/preferred protocol version.
    /// </summary>
    public string CurrentVersion { get; set; } = "0.1.0";
    
    /// <summary>
    /// Gets or sets whether to allow backward compatibility within major versions.
    /// </summary>
    public bool AllowBackwardCompatibility { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to log version negotiation details.
    /// </summary>
    public bool LogNegotiationDetails { get; set; } = true;
}

/// <summary>
/// Implementation of protocol version negotiation.
/// </summary>
public class ProtocolVersionNegotiator : IProtocolVersionNegotiator
{
    private readonly ILogger<ProtocolVersionNegotiator> _logger;
    private readonly ProtocolVersionConfiguration _configuration;
    private readonly List<ProtocolVersion> _supportedVersions;
    private readonly ProtocolVersion _currentVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolVersionNegotiator"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The configuration options.</param>
    public ProtocolVersionNegotiator(
        ILogger<ProtocolVersionNegotiator> logger,
        IOptions<ProtocolVersionConfiguration> options)
    {
        _logger = logger;
        _configuration = options.Value;
        
        // Parse and validate supported versions
        _supportedVersions = new List<ProtocolVersion>();
        foreach (var version in _configuration.SupportedVersions)
        {
            if (ProtocolVersion.TryParse(version, out var parsed))
            {
                _supportedVersions.Add(parsed);
            }
            else
            {
                _logger.LogWarning("Invalid protocol version in configuration: {Version}", version);
            }
        }
        
        // Sort versions in descending order (newest first)
        _supportedVersions.Sort((a, b) => b.CompareTo(a));
        
        // Parse current version
        if (!ProtocolVersion.TryParse(_configuration.CurrentVersion, out var current))
        {
            throw new InvalidOperationException($"Invalid current protocol version: {_configuration.CurrentVersion}");
        }
        _currentVersion = current;
        
        _logger.LogInformation("Protocol version negotiator initialized. Current version: {Current}, Supported: {Supported}",
            _currentVersion, string.Join(", ", _supportedVersions));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ProtocolVersion> SupportedVersions => _supportedVersions.AsReadOnly();

    /// <inheritdoc/>
    public ProtocolVersion CurrentVersion => _currentVersion;

    /// <inheritdoc/>
    public ProtocolVersion NegotiateVersion(string clientVersion)
    {
        if (!TryNegotiateVersion(clientVersion, out var negotiated))
        {
            var supportedStrings = _supportedVersions.Select(v => v.ToString()).ToList();
            throw new ProtocolVersionException(clientVersion, supportedStrings);
        }
        
        return negotiated;
    }

    /// <inheritdoc/>
    public bool IsVersionSupported(string version)
    {
        if (!ProtocolVersion.TryParse(version, out var parsed))
        {
            return false;
        }
        
        // Check for exact match
        if (_supportedVersions.Any(v => v.Equals(parsed)))
        {
            return true;
        }
        
        // Check for backward compatibility if enabled
        if (_configuration.AllowBackwardCompatibility)
        {
            return _supportedVersions.Any(v => v.IsCompatibleWith(parsed));
        }
        
        return false;
    }

    /// <inheritdoc/>
    public bool TryNegotiateVersion(string clientVersion, [NotNullWhen(true)] out ProtocolVersion? negotiatedVersion)
    {
        negotiatedVersion = null;
        
        if (_configuration.LogNegotiationDetails)
        {
            _logger.LogDebug("Negotiating protocol version. Client: {ClientVersion}, Server supports: {ServerVersions}",
                clientVersion, string.Join(", ", _supportedVersions));
        }
        
        if (!ProtocolVersion.TryParse(clientVersion, out var clientParsed))
        {
            _logger.LogWarning("Client provided invalid protocol version: {ClientVersion}", clientVersion);
            return false;
        }
        
        // First, check for exact match
        var exactMatch = _supportedVersions.FirstOrDefault(v => v.Equals(clientParsed));
        if (exactMatch != null)
        {
            negotiatedVersion = exactMatch;
            if (_configuration.LogNegotiationDetails)
            {
                _logger.LogInformation("Exact version match found: {Version}", negotiatedVersion);
            }
            return true;
        }
        
        // If backward compatibility is enabled, find the best compatible version
        if (_configuration.AllowBackwardCompatibility)
        {
            // Find server versions that share the same major version
            var sameMajor = _supportedVersions
                .Where(serverVersion => serverVersion.Major == clientParsed.Major)
                .ToList();
            
            if (sameMajor.Count > 0)
            {
                // Find the best match:
                // 1. If client minor is less than or equal to any server minor, use the lowest server minor >= client minor
                // 2. If client minor is greater than all server minors, use the highest server minor (backward compatible)
                
                // Find exact minor match first
                var exactMinorMatches = sameMajor
                    .Where(v => v.Minor == clientParsed.Minor)
                    .OrderByDescending(v => v.Patch)
                    .ToList();
                
                if (exactMinorMatches.Count > 0)
                {
                    // If server has equal or higher patch, use it
                    var higherPatch = exactMinorMatches.FirstOrDefault(v => v.Patch >= clientParsed.Patch);
                    if (higherPatch != null)
                    {
                        negotiatedVersion = higherPatch;
                    }
                    else
                    {
                        // Server has lower patch version, still compatible for backward compatibility
                        negotiatedVersion = exactMinorMatches.First();
                    }
                    
                    if (_configuration.LogNegotiationDetails)
                    {
                        _logger.LogInformation("Exact minor version match found. Client: {ClientVersion}, Using: {NegotiatedVersion}",
                            clientVersion, negotiatedVersion);
                    }
                    return true;
                }
                
                // No exact minor match or server patch is lower, find next higher minor
                var higherMinor = sameMajor
                    .Where(v => v.Minor > clientParsed.Minor)
                    .OrderBy(v => v.Minor)
                    .ThenBy(v => v.Patch)
                    .FirstOrDefault();
                
                if (higherMinor != null)
                {
                    negotiatedVersion = higherMinor;
                    if (_configuration.LogNegotiationDetails)
                    {
                        _logger.LogInformation("Higher minor version found. Client: {ClientVersion}, Using: {NegotiatedVersion}",
                            clientVersion, negotiatedVersion);
                    }
                    return true;
                }
                
                // Client has newer minor version, use the highest server minor version
                var highestServerMinor = sameMajor
                    .OrderByDescending(v => v.Minor)
                    .ThenByDescending(v => v.Patch)
                    .First();
                
                negotiatedVersion = highestServerMinor;
                if (_configuration.LogNegotiationDetails)
                {
                    _logger.LogInformation("Client has newer minor version. Client: {ClientVersion}, Using: {NegotiatedVersion}",
                        clientVersion, negotiatedVersion);
                }
                return true;
            }
        }
        
        _logger.LogWarning("No compatible protocol version found. Client: {ClientVersion}, Server supports: {ServerVersions}",
            clientVersion, string.Join(", ", _supportedVersions));
        
        return false;
    }
}