using McpServer.Domain.Tools;

namespace McpServer.Application.Services;

/// <summary>
/// Service for managing tool registrations.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    /// <returns>A dictionary of tool names to tool instances.</returns>
    IReadOnlyDictionary<string, ITool> GetTools();
    
    /// <summary>
    /// Registers a tool.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    void RegisterTool(ITool tool);
    
    /// <summary>
    /// Gets a tool by name.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <returns>The tool if found, null otherwise.</returns>
    ITool? GetTool(string name);
}