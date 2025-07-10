namespace McpServer.Domain.Prompts;

/// <summary>
/// Represents a prompt provider that can list and get prompts.
/// </summary>
public interface IPromptProvider
{
    /// <summary>
    /// Lists all available prompts.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of available prompts.</returns>
    Task<IEnumerable<Prompt>> ListPromptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific prompt by name.
    /// </summary>
    /// <param name="name">The name of the prompt.</param>
    /// <param name="arguments">The arguments for the prompt.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The prompt messages.</returns>
    Task<PromptResult> GetPromptAsync(string name, Dictionary<string, string>? arguments = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a prompt template.
/// </summary>
public record Prompt
{
    /// <summary>
    /// Gets the unique name of the prompt.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the human-readable description of the prompt.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the arguments for the prompt.
    /// </summary>
    public List<PromptArgument>? Arguments { get; init; }
}

/// <summary>
/// Represents an argument for a prompt.
/// </summary>
public record PromptArgument
{
    /// <summary>
    /// Gets the name of the argument.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the description of the argument.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets whether the argument is required.
    /// </summary>
    public bool Required { get; init; }
}

/// <summary>
/// Represents the result of a prompt request.
/// </summary>
public record PromptResult
{
    /// <summary>
    /// Gets the prompt messages.
    /// </summary>
    public required List<PromptMessage> Messages { get; init; }
}

/// <summary>
/// Represents a message in a prompt.
/// </summary>
public record PromptMessage
{
    /// <summary>
    /// Gets the role of the message (e.g., "user", "assistant").
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Gets the content of the message.
    /// </summary>
    public required PromptMessageContent Content { get; init; }
}

/// <summary>
/// Represents the content of a prompt message.
/// </summary>
public abstract record PromptMessageContent
{
    /// <summary>
    /// Gets the type of content.
    /// </summary>
    public abstract string Type { get; }
}

/// <summary>
/// Represents text content in a prompt message.
/// </summary>
public record TextPromptContent : PromptMessageContent
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
/// Represents image content in a prompt message.
/// </summary>
public record ImagePromptContent : PromptMessageContent
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