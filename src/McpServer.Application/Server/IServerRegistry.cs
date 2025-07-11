using McpServer.Domain.Tools;
using McpServer.Domain.Resources;
using McpServer.Domain.Prompts;

namespace McpServer.Application.Server;

/// <summary>
/// Registry operations for tools, resources, and prompts.
/// </summary>
public interface IServerRegistry
{
    /// <summary>
    /// Registers a tool with the server.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    void RegisterTool(ITool tool);
    
    /// <summary>
    /// Registers a resource provider with the server.
    /// </summary>
    /// <param name="provider">The resource provider to register.</param>
    void RegisterResourceProvider(IResourceProvider provider);
    
    /// <summary>
    /// Registers a prompt provider with the server.
    /// </summary>
    /// <param name="provider">The prompt provider to register.</param>
    void RegisterPromptProvider(IPromptProvider provider);
}