using System.Globalization;

namespace McpServer.Infrastructure.Tools;

/// <summary>
/// A tool for working with dates and times.
/// </summary>
public class DateTimeTool : ITool
{
    private readonly ILogger<DateTimeTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DateTimeTool"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DateTimeTool(ILogger<DateTimeTool> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "datetime";

    /// <inheritdoc/>
    public string Description => "Provides current date/time information and formatting";

    /// <inheritdoc/>
    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["format"] = new
            {
                type = "string",
                description = "The format string for the output (optional)",
                @default = "yyyy-MM-dd HH:mm:ss"
            },
            ["timezone"] = new
            {
                type = "string",
                description = "The timezone ID (optional, defaults to UTC)",
                @default = "UTC"
            }
        },
        AdditionalProperties = false
    };

    /// <inheritdoc/>
    public Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken = default)
    {
        var format = "yyyy-MM-dd HH:mm:ss";
        var timezoneId = "UTC";

        if (request.Arguments != null)
        {
            if (request.Arguments.TryGetValue("format", out var formatObj) && formatObj != null)
            {
                format = formatObj.ToString()!;
            }

            if (request.Arguments.TryGetValue("timezone", out var tzObj) && tzObj != null)
            {
                timezoneId = tzObj.ToString()!;
            }
        }

        try
        {
            TimeZoneInfo timezone;
            try
            {
                timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try common timezone abbreviations
                timezone = timezoneId.ToUpperInvariant() switch
                {
                    "PST" => TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),
                    "EST" => TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
                    "CST" => TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"),
                    "MST" => TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time"),
                    _ => TimeZoneInfo.Utc
                };
            }

            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
            var formatted = now.ToString(format, CultureInfo.InvariantCulture);

            _logger.LogInformation("Formatted datetime: {DateTime} in timezone {Timezone}", formatted, timezone.Id);

            return Task.FromResult(new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new TextContent 
                    { 
                        Text = $"Current time: {formatted}\nTimezone: {timezone.DisplayName}"
                    }
                }
            });
        }
        catch (FormatException)
        {
            return Task.FromResult(new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new TextContent { Text = $"Error: Invalid format string '{format}'" }
                },
                IsError = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing datetime tool");
            return Task.FromResult(new ToolResult
            {
                Content = new List<ToolContent>
                {
                    new TextContent { Text = $"Error: {ex.Message}" }
                },
                IsError = true
            });
        }
    }
}