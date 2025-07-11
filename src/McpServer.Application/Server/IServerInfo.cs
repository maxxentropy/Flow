using McpServer.Domain.Protocol.Messages;

namespace McpServer.Application.Server;

/// <summary>
/// Provides server information and capabilities.
/// </summary>
public interface IServerInfo
{
    /// <summary>
    /// Gets the server information.
    /// </summary>
    ServerInfo ServerInfo { get; }
    
    /// <summary>
    /// Gets the server capabilities.
    /// </summary>
    ServerCapabilities Capabilities { get; }
}