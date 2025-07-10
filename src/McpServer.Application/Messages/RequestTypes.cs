namespace McpServer.Application.Messages;

// Request/notification types
internal record InitializedNotification { }
internal record ToolsListRequest { }
internal record ToolsCallRequest 
{
    public required string Name { get; init; }
    public Dictionary<string, object?>? Arguments { get; init; }
}
internal record ResourcesListRequest { }
internal record ResourcesReadRequest 
{
    public required string Uri { get; init; }
}
internal record ResourcesSubscribeRequest 
{
    public required string Uri { get; init; }
}
internal record ResourcesUnsubscribeRequest 
{
    public required string Uri { get; init; }
}
internal record PromptsListRequest { }
internal record PromptsGetRequest 
{
    public required string Name { get; init; }
    public Dictionary<string, string>? Arguments { get; init; }
}
public record LoggingSetLevelRequest 
{
    public required string Level { get; init; }
}
public record RootsListRequest { }