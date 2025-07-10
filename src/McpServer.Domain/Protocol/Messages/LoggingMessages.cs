using System.Text.Json.Serialization;

namespace McpServer.Domain.Protocol.Messages;

/// <summary>
/// Represents a log level according to RFC 5424 syslog severity standards.
/// </summary>
public enum McpLogLevel
{
    /// <summary>
    /// Debug level messages (lowest priority).
    /// </summary>
    Debug = 0,
    
    /// <summary>
    /// Informational messages.
    /// </summary>
    Info = 1,
    
    /// <summary>
    /// Normal but significant condition.
    /// </summary>
    Notice = 2,
    
    /// <summary>
    /// Warning conditions.
    /// </summary>
    Warning = 3,
    
    /// <summary>
    /// Error conditions.
    /// </summary>
    Error = 4,
    
    /// <summary>
    /// Critical conditions.
    /// </summary>
    Critical = 5,
    
    /// <summary>
    /// Action must be taken immediately.
    /// </summary>
    Alert = 6,
    
    /// <summary>
    /// System is unusable (highest priority).
    /// </summary>
    Emergency = 7
}

/// <summary>
/// Notification to send a log message to the client.
/// </summary>
public record LogMessageNotification : Notification
{
    /// <inheritdoc/>
    public override string Method => "notifications/message";
    
    /// <inheritdoc/>
    public override object? Params => LogParams;
    
    /// <summary>
    /// Gets the log message parameters.
    /// </summary>
    [JsonPropertyName("params")]
    public required LogMessageParams LogParams { get; init; }
}

/// <summary>
/// Parameters for a log message notification.
/// </summary>
public record LogMessageParams
{
    /// <summary>
    /// Gets the log level.
    /// </summary>
    [JsonPropertyName("level")]
    public required string Level { get; init; }
    
    /// <summary>
    /// Gets the optional logger name.
    /// </summary>
    [JsonPropertyName("logger")]
    public string? Logger { get; init; }
    
    /// <summary>
    /// Gets the log data (arbitrary JSON-serializable data).
    /// </summary>
    [JsonPropertyName("data")]
    public required object Data { get; init; }
}

/// <summary>
/// Utility class for log level operations.
/// </summary>
public static class LogLevelExtensions
{
    private static readonly Dictionary<string, McpLogLevel> _stringToLogLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["debug"] = McpLogLevel.Debug,
        ["info"] = McpLogLevel.Info,
        ["notice"] = McpLogLevel.Notice,
        ["warning"] = McpLogLevel.Warning,
        ["error"] = McpLogLevel.Error,
        ["critical"] = McpLogLevel.Critical,
        ["alert"] = McpLogLevel.Alert,
        ["emergency"] = McpLogLevel.Emergency
    };
    
    private static readonly Dictionary<McpLogLevel, string> _logLevelToString = new()
    {
        [McpLogLevel.Debug] = "debug",
        [McpLogLevel.Info] = "info",
        [McpLogLevel.Notice] = "notice",
        [McpLogLevel.Warning] = "warning",
        [McpLogLevel.Error] = "error",
        [McpLogLevel.Critical] = "critical",
        [McpLogLevel.Alert] = "alert",
        [McpLogLevel.Emergency] = "emergency"
    };
    
    /// <summary>
    /// Converts a string to a log level.
    /// </summary>
    /// <param name="level">The log level string.</param>
    /// <returns>The log level enum value.</returns>
    /// <exception cref="ArgumentException">Thrown when the level string is invalid.</exception>
    public static McpLogLevel ToLogLevel(this string level)
    {
        if (_stringToLogLevel.TryGetValue(level, out var logLevel))
        {
            return logLevel;
        }
        
        throw new ArgumentException($"Invalid log level: {level}. Valid levels are: debug, info, notice, warning, error, critical, alert, emergency", nameof(level));
    }
    
    /// <summary>
    /// Converts a log level to its string representation.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <returns>The string representation of the log level.</returns>
    public static string ToLogLevelString(this McpLogLevel level)
    {
        return _logLevelToString[level];
    }
    
    /// <summary>
    /// Checks if a log level should be logged based on the minimum level.
    /// </summary>
    /// <param name="level">The message log level.</param>
    /// <param name="minimumLevel">The minimum log level.</param>
    /// <returns>True if the message should be logged, false otherwise.</returns>
    public static bool ShouldLog(this McpLogLevel level, McpLogLevel minimumLevel)
    {
        return level >= minimumLevel;
    }
}