using McpServer.Domain.Protocol.Messages;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace McpServer.Application.Services;

/// <summary>
/// Service for handling MCP logging functionality.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Gets the current minimum log level.
    /// </summary>
    McpLogLevel MinimumLogLevel { get; }
    
    /// <summary>
    /// Sets the minimum log level.
    /// </summary>
    /// <param name="level">The minimum log level to set.</param>
    void SetLogLevel(McpLogLevel level);
    
    /// <summary>
    /// Sets the minimum log level from a string.
    /// </summary>
    /// <param name="level">The minimum log level string.</param>
    void SetLogLevel(string level);
    
    /// <summary>
    /// Logs a message at the specified level.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <param name="data">The log data.</param>
    /// <param name="logger">The optional logger name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LogAsync(McpLogLevel level, object data, string? logger = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="data">The log data.</param>
    /// <param name="logger">The optional logger name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LogDebugAsync(object data, string? logger = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs an info message.
    /// </summary>
    /// <param name="data">The log data.</param>
    /// <param name="logger">The optional logger name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LogInfoAsync(object data, string? logger = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs a notice message.
    /// </summary>
    /// <param name="data">The log data.</param>
    /// <param name="logger">The optional logger name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LogNoticeAsync(object data, string? logger = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="data">The log data.</param>
    /// <param name="logger">The optional logger name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LogWarningAsync(object data, string? logger = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="data">The log data.</param>
    /// <param name="logger">The optional logger name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LogErrorAsync(object data, string? logger = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs a critical message.
    /// </summary>
    /// <param name="data">The log data.</param>
    /// <param name="logger">The optional logger name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LogCriticalAsync(object data, string? logger = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs an alert message.
    /// </summary>
    /// <param name="data">The log data.</param>
    /// <param name="logger">The optional logger name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LogAlertAsync(object data, string? logger = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs an emergency message.
    /// </summary>
    /// <param name="data">The log data.</param>
    /// <param name="logger">The optional logger name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task LogEmergencyAsync(object data, string? logger = null, CancellationToken cancellationToken = default);
}