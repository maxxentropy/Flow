namespace McpServer.Domain.Tools;

/// <summary>
/// Represents a tool that can be executed by the MCP server.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Gets the unique name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the human-readable description of the tool.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the JSON schema for the tool's input parameters.
    /// </summary>
    ToolSchema Schema { get; }

    /// <summary>
    /// Executes the tool with the given request.
    /// </summary>
    /// <param name="request">The tool execution request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool execution result.</returns>
    Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the schema for a tool's input parameters.
/// </summary>
public record ToolSchema
{
    /// <summary>
    /// Gets the type of the schema (usually "object").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the properties of the schema.
    /// </summary>
    public Dictionary<string, object>? Properties { get; init; }

    /// <summary>
    /// Gets the list of required properties.
    /// </summary>
    public List<string>? Required { get; init; }

    /// <summary>
    /// Gets whether additional properties are allowed.
    /// </summary>
    public bool? AdditionalProperties { get; init; }
}

/// <summary>
/// Represents a request to execute a tool.
/// </summary>
public record ToolRequest
{
    /// <summary>
    /// Gets the name of the tool to execute.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the arguments for the tool execution.
    /// </summary>
    public Dictionary<string, object?>? Arguments { get; init; }
}

/// <summary>
/// Represents the result of a tool execution.
/// </summary>
public record ToolResult
{
    /// <summary>
    /// Gets the content returned by the tool.
    /// </summary>
    public required List<ToolContent> Content { get; init; }

    /// <summary>
    /// Gets whether the tool indicates the model should continue.
    /// </summary>
    public bool? IsError { get; init; }
    
    /// <summary>
    /// Gets whether the tool execution was successful.
    /// </summary>
    public bool IsSuccess => !IsError.GetValueOrDefault();
}

/// <summary>
/// Represents content returned by a tool.
/// </summary>
public abstract record ToolContent
{
    /// <summary>
    /// Gets the type of content.
    /// </summary>
    public abstract string Type { get; }
}

/// <summary>
/// Represents text content returned by a tool.
/// </summary>
public record TextContent : ToolContent
{
    /// <summary>
    /// Gets the type of content.
    /// </summary>
    public override string Type => "text";

    /// <summary>
    /// Gets the text content.
    /// </summary>
    public required string Text { get; init; }
}

/// <summary>
/// Represents image content returned by a tool.
/// </summary>
public record ImageContent : ToolContent
{
    /// <summary>
    /// Gets the type of content.
    /// </summary>
    public override string Type => "image";

    /// <summary>
    /// Gets the base64-encoded image data.
    /// </summary>
    public required string Data { get; init; }

    /// <summary>
    /// Gets the MIME type of the image.
    /// </summary>
    public required string MimeType { get; init; }
}