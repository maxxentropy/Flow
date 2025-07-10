using System.Text.Json.Serialization;

namespace McpServer.Domain.Protocol.Messages;

/// <summary>
/// Request to create a message via LLM sampling.
/// </summary>
public record CreateMessageRequest
{
    /// <summary>
    /// Gets the conversation messages.
    /// </summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<SamplingMessage> Messages { get; init; }
    
    /// <summary>
    /// Gets the model preferences.
    /// </summary>
    [JsonPropertyName("modelPreferences")]
    public ModelPreferences? ModelPreferences { get; init; }
    
    /// <summary>
    /// Gets the system prompt.
    /// </summary>
    [JsonPropertyName("systemPrompt")]
    public string? SystemPrompt { get; init; }
    
    /// <summary>
    /// Gets the context inclusion option.
    /// </summary>
    [JsonPropertyName("includeContext")]
    public string? IncludeContext { get; init; } = "none";
    
    /// <summary>
    /// Gets the sampling temperature.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }
    
    /// <summary>
    /// Gets the maximum tokens to generate.
    /// </summary>
    [JsonPropertyName("maxTokens")]
    public int? MaxTokens { get; init; }
    
    /// <summary>
    /// Gets the stop sequences.
    /// </summary>
    [JsonPropertyName("stopSequences")]
    public IReadOnlyList<string>? StopSequences { get; init; }
    
    /// <summary>
    /// Gets arbitrary metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// A message in the sampling conversation.
/// </summary>
public record SamplingMessage
{
    /// <summary>
    /// Gets the role of the message sender.
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; } // "user" or "assistant"
    
    /// <summary>
    /// Gets the message content.
    /// </summary>
    [JsonPropertyName("content")]
    public required MessageContent Content { get; init; }
}

/// <summary>
/// Content of a sampling message.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContent), "text")]
[JsonDerivedType(typeof(ImageContent), "image")]
public abstract record MessageContent
{
    /// <summary>
    /// Gets the content type.
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// Text message content.
/// </summary>
public record TextContent : MessageContent
{
    /// <inheritdoc/>
    public override string Type => "text";
    
    /// <summary>
    /// Gets the text content.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// Image message content.
/// </summary>
public record ImageContent : MessageContent
{
    /// <inheritdoc/>
    public override string Type => "image";
    
    /// <summary>
    /// Gets the base64-encoded image data.
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }
    
    /// <summary>
    /// Gets the MIME type of the image.
    /// </summary>
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }
}

/// <summary>
/// Model preferences for sampling.
/// </summary>
public record ModelPreferences
{
    /// <summary>
    /// Gets the cost priority (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("costPriority")]
    public double? CostPriority { get; init; }
    
    /// <summary>
    /// Gets the speed priority (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("speedPriority")]
    public double? SpeedPriority { get; init; }
    
    /// <summary>
    /// Gets the intelligence priority (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("intelligencePriority")]
    public double? IntelligencePriority { get; init; }
    
    /// <summary>
    /// Gets the list of model hints.
    /// </summary>
    [JsonPropertyName("hints")]
    public IReadOnlyList<ModelHint>? Hints { get; init; }
}

/// <summary>
/// A hint about a specific model.
/// </summary>
public record ModelHint
{
    /// <summary>
    /// Gets the model name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>
/// Response from a create message request.
/// </summary>
public record CreateMessageResponse
{
    /// <summary>
    /// Gets the model used for generation.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }
    
    /// <summary>
    /// Gets the reason generation stopped.
    /// </summary>
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; init; } // "endTurn", "stopSequence", "maxTokens"
    
    /// <summary>
    /// Gets the role of the generated message.
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }
    
    /// <summary>
    /// Gets the generated content.
    /// </summary>
    [JsonPropertyName("content")]
    public required MessageContent Content { get; init; }
}

/// <summary>
/// Capability declaration for sampling support.
/// </summary>
public record SamplingCapability
{
    // Empty for now, can be extended in the future
}