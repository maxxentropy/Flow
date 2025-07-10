using System.Text.Json.Serialization;

namespace McpServer.Domain.Protocol.Messages;

/// <summary>
/// Request to get completion suggestions for a reference.
/// </summary>
public record CompletionCompleteRequest
{
    /// <summary>
    /// Gets the reference to complete.
    /// </summary>
    public required CompletionReference Ref { get; init; }

    /// <summary>
    /// Gets the argument object for completion.
    /// </summary>
    public required CompletionArgument Argument { get; init; }
}

/// <summary>
/// Response containing completion suggestions.
/// </summary>
public record CompletionCompleteResponse
{
    /// <summary>
    /// Gets the completion items.
    /// </summary>
    public required CompletionItem[] Completion { get; init; }

    /// <summary>
    /// Gets whether there are more completion items available.
    /// </summary>
    public bool? HasMore { get; init; }

    /// <summary>
    /// Gets the total number of completion items.
    /// </summary>
    public int? Total { get; init; }
}

/// <summary>
/// A reference to an object that can be completed.
/// </summary>
public record CompletionReference
{
    /// <summary>
    /// Gets the type of the reference.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the name of the reference.
    /// </summary>
    public required string Name { get; init; }
}

/// <summary>
/// Argument object for completion requests.
/// </summary>
public record CompletionArgument
{
    /// <summary>
    /// Gets the name of the argument being completed.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the current value (partial) of the argument.
    /// </summary>
    public required string Value { get; init; }
}

/// <summary>
/// A completion suggestion.
/// </summary>
public record CompletionItem
{
    /// <summary>
    /// Gets the completion value.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the display label for the completion.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Gets the description of the completion.
    /// </summary>
    public string? Description { get; init; }
}