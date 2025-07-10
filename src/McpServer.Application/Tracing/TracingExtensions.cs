using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace McpServer.Application.Tracing;

/// <summary>
/// Extension methods for distributed tracing.
/// </summary>
public static class TracingExtensions
{
    /// <summary>
    /// The activity source for MCP Server application.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("McpServer.Application", "1.0.0");

    /// <summary>
    /// Starts a new activity for a handler operation.
    /// </summary>
    public static Activity? StartHandlerActivity(
        string handlerName,
        string method,
        string? requestId = null,
        [CallerMemberName] string operationName = "")
    {
        var activity = ActivitySource.StartActivity(
            $"{handlerName}.{operationName}",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("mcp.handler", handlerName);
            activity.SetTag("mcp.method", method);
            
            if (!string.IsNullOrEmpty(requestId))
            {
                activity.SetTag("mcp.request_id", requestId);
            }
        }

        return activity;
    }

    /// <summary>
    /// Records an exception on the activity.
    /// </summary>
    public static void RecordException(this Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.RecordException(exception);
    }

    /// <summary>
    /// Sets success status on the activity.
    /// </summary>
    public static void SetSuccess(this Activity? activity, string? description = null)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Ok, description);
    }

    /// <summary>
    /// Adds a tag to the activity.
    /// </summary>
    public static Activity? AddTag(this Activity? activity, string key, object? value)
    {
        activity?.SetTag(key, value);
        return activity;
    }

    /// <summary>
    /// Adds multiple tags to the activity.
    /// </summary>
    public static Activity? AddTags(this Activity? activity, params (string key, object? value)[] tags)
    {
        if (activity == null) return null;

        foreach (var (key, value) in tags)
        {
            activity.SetTag(key, value);
        }

        return activity;
    }

    /// <summary>
    /// Adds an event to the activity.
    /// </summary>
    public static Activity? AddEvent(this Activity? activity, string name, Dictionary<string, object?>? attributes = null)
    {
        if (activity == null) return null;

        if (attributes != null)
        {
            var activityTags = new ActivityTagsCollection();
            foreach (var kvp in attributes)
            {
                activityTags.Add(kvp.Key, kvp.Value);
            }
            activity.AddEvent(new ActivityEvent(name, tags: activityTags));
        }
        else
        {
            activity.AddEvent(new ActivityEvent(name));
        }

        return activity;
    }
}