using System.Collections.Concurrent;
using McpServer.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpServer.Application.Services;

/// <summary>
/// Standalone implementation of tool registry.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly ConcurrentDictionary<string, ITool> _tools = new();
    private readonly SemaphoreSlim _registrationLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolRegistry"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ToolRegistry(ILogger<ToolRegistry> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void RegisterTool(ITool tool)
    {
        _registrationLock.Wait();
        try
        {
            _tools[tool.Name] = tool;
            _logger.LogInformation("Registered tool: {ToolName}", tool.Name);
            
            // Raise an event that MultiplexingMcpServer can subscribe to
            ToolRegistered?.Invoke(this, new ToolEventArgs(tool));
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    /// <inheritdoc/>
    public ITool? GetTool(string name) => _tools.TryGetValue(name, out var tool) ? tool : null;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ITool> GetTools() => _tools;

    /// <summary>
    /// Event raised when a tool is registered.
    /// </summary>
    public event EventHandler<ToolEventArgs>? ToolRegistered;
}

/// <summary>
/// Event args for tool registration.
/// </summary>
public class ToolEventArgs : EventArgs
{
    /// <summary>
    /// Gets the registered tool.
    /// </summary>
    public ITool Tool { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolEventArgs"/> class.
    /// </summary>
    /// <param name="tool">The tool that was registered.</param>
    public ToolEventArgs(ITool tool)
    {
        Tool = tool;
    }
}