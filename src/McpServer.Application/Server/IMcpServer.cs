using McpServer.Domain.Connection;
using McpServer.Domain.Transport;
using McpServer.Domain.Tools;
using McpServer.Domain.Resources;
using McpServer.Domain.Prompts;
using McpServer.Domain.Protocol.Messages;

namespace McpServer.Application.Server;

/// <summary>
/// Main interface for the MCP server, composed of segregated interfaces.
/// </summary>
public interface IMcpServer : IServerInfo, IServerLifecycle, IServerConnectionManager, IServerRegistry
{
    // All methods are inherited from segregated interfaces
}