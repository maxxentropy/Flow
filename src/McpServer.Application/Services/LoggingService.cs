using System.Collections.Concurrent;
using McpServer.Domain.Protocol.Messages;
using McpServer.Domain.Transport;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace McpServer.Application.Services;

/// <summary>
/// Default implementation of the logging service.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;
    private readonly INotificationService _notificationService;
    private readonly ConcurrentDictionary<string, DateTime> _rateLimitCache = new();
    private readonly TimeSpan _rateLimitWindow = TimeSpan.FromSeconds(1);
    private readonly int _maxMessagesPerWindow = 10;
    private McpLogLevel _minimumLogLevel = McpLogLevel.Info; // Default to info level
    
    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="notificationService">The notification service.</param>
    public LoggingService(ILogger<LoggingService> logger, INotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;
    }
    
    /// <inheritdoc/>
    public McpLogLevel MinimumLogLevel => _minimumLogLevel;
    
    /// <inheritdoc/>
    public void SetLogLevel(McpLogLevel level)
    {
        _minimumLogLevel = level;
        _logger.LogInformation("Log level set to {LogLevel}", level.ToLogLevelString());
    }
    
    /// <inheritdoc/>
    public void SetLogLevel(string level)
    {
        SetLogLevel(level.ToLogLevel());
    }
    
    /// <inheritdoc/>
    public async Task LogAsync(McpLogLevel level, object data, string? logger = null, CancellationToken cancellationToken = default)
    {
        // Check if this level should be logged
        if (!level.ShouldLog(_minimumLogLevel))
        {
            return;
        }
        
        // Apply rate limiting
        if (!ShouldAllowMessage(logger))
        {
            return;
        }
        
        // Sanitize data to remove sensitive information
        var sanitizedData = SanitizeLogData(data);
        
        var notification = new LogMessageNotification
        {
            LogParams = new LogMessageParams
            {
                Level = level.ToLogLevelString(),
                Logger = logger,
                Data = sanitizedData
            }
        };
        
        try
        {
            await _notificationService.SendNotificationAsync(notification, cancellationToken);
            _logger.LogDebug("Sent log notification: {Level} from {Logger}", level.ToLogLevelString(), logger ?? "default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send log notification");
        }
    }
    
    /// <inheritdoc/>
    public Task LogDebugAsync(object data, string? logger = null, CancellationToken cancellationToken = default)
        => LogAsync(McpLogLevel.Debug, data, logger, cancellationToken);
    
    /// <inheritdoc/>
    public Task LogInfoAsync(object data, string? logger = null, CancellationToken cancellationToken = default)
        => LogAsync(McpLogLevel.Info, data, logger, cancellationToken);
    
    /// <inheritdoc/>
    public Task LogNoticeAsync(object data, string? logger = null, CancellationToken cancellationToken = default)
        => LogAsync(McpLogLevel.Notice, data, logger, cancellationToken);
    
    /// <inheritdoc/>
    public Task LogWarningAsync(object data, string? logger = null, CancellationToken cancellationToken = default)
        => LogAsync(McpLogLevel.Warning, data, logger, cancellationToken);
    
    /// <inheritdoc/>
    public Task LogErrorAsync(object data, string? logger = null, CancellationToken cancellationToken = default)
        => LogAsync(McpLogLevel.Error, data, logger, cancellationToken);
    
    /// <inheritdoc/>
    public Task LogCriticalAsync(object data, string? logger = null, CancellationToken cancellationToken = default)
        => LogAsync(McpLogLevel.Critical, data, logger, cancellationToken);
    
    /// <inheritdoc/>
    public Task LogAlertAsync(object data, string? logger = null, CancellationToken cancellationToken = default)
        => LogAsync(McpLogLevel.Alert, data, logger, cancellationToken);
    
    /// <inheritdoc/>
    public Task LogEmergencyAsync(object data, string? logger = null, CancellationToken cancellationToken = default)
        => LogAsync(McpLogLevel.Emergency, data, logger, cancellationToken);
    
    private bool ShouldAllowMessage(string? logger)
    {
        var key = logger ?? "default";
        var now = DateTime.UtcNow;
        var windowStart = now - _rateLimitWindow;
        
        // Clean up old entries
        var keysToRemove = _rateLimitCache
            .Where(kvp => kvp.Value < windowStart)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var oldKey in keysToRemove)
        {
            _rateLimitCache.TryRemove(oldKey, out _);
        }
        
        // Count messages in current window
        var messagesInWindow = _rateLimitCache.Count(kvp => kvp.Key.StartsWith($"{key}:", StringComparison.Ordinal) && kvp.Value >= windowStart);
        
        if (messagesInWindow >= _maxMessagesPerWindow)
        {
            return false;
        }
        
        // Add this message to the rate limit cache
        var messageKey = $"{key}:{Guid.NewGuid()}";
        _rateLimitCache.TryAdd(messageKey, now);
        
        return true;
    }
    
    private static object SanitizeLogData(object data)
    {
        // Convert to dictionary if it's a complex object for sanitization
        if (data is string stringData)
        {
            // Simple sanitization for strings - remove common sensitive patterns
            return SanitizeString(stringData);
        }
        
        if (data is Dictionary<string, object> dictData)
        {
            var sanitized = new Dictionary<string, object>();
            foreach (var kvp in dictData)
            {
                if (IsSensitiveKey(kvp.Key))
                {
                    sanitized[kvp.Key] = "[REDACTED]";
                }
                else
                {
                    sanitized[kvp.Key] = SanitizeLogData(kvp.Value);
                }
            }
            return sanitized;
        }
        
        // For other types, return as-is but could be extended for more sophisticated sanitization
        return data;
    }
    
    private static string SanitizeString(string input)
    {
        // Basic patterns to redact (can be extended)
        var sensitivePatterns = new[]
        {
            @"password\s*[=:]\s*\S+",
            @"token\s*[=:]\s*\S+",
            @"key\s*[=:]\s*\S+",
            @"secret\s*[=:]\s*\S+"
        };
        
        var result = input;
        foreach (var pattern in sensitivePatterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(
                result, pattern, "[REDACTED]", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return result;
    }
    
    private static bool IsSensitiveKey(string key)
    {
        var sensitiveKeys = new[]
        {
            "password", "pwd", "pass", "token", "key", "secret", "auth", "authorization",
            "credential", "cred", "api_key", "apikey", "access_token", "refresh_token"
        };
        
        return sensitiveKeys.Any(sensitive => 
            key.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }
}