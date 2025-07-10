namespace McpServer.Application.Server;

/// <summary>
/// Main interface for the MCP server.
/// </summary>
public interface IMcpServer
{
    /// <summary>
    /// Gets the server information.
    /// </summary>
    ServerInfo ServerInfo { get; }

    /// <summary>
    /// Gets the server capabilities.
    /// </summary>
    ServerCapabilities Capabilities { get; }

    /// <summary>
    /// Gets whether the server is initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Sets the initialization state of the server.
    /// </summary>
    /// <param name="initialized">The initialization state.</param>
    void SetInitialized(bool initialized);

    /// <summary>
    /// Starts the server with the specified transport.
    /// </summary>
    /// <param name="transport">The transport to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the start operation.</returns>
    Task StartAsync(ITransport transport, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the stop operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

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