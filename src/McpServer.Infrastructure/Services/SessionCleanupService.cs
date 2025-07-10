using McpServer.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Infrastructure.Services;

/// <summary>
/// Background service for cleaning up expired sessions.
/// </summary>
public class SessionCleanupService : BackgroundService
{
    private readonly ILogger<SessionCleanupService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SessionCleanupOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionCleanupService"/> class.
    /// </summary>
    public SessionCleanupService(
        ILogger<SessionCleanupService> logger,
        IServiceProvider serviceProvider,
        IOptions<SessionCleanupOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.CleanupInterval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

                var count = await sessionService.CleanupExpiredSessionsAsync(stoppingToken);
                
                if (count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired sessions", count);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }
        }

        _logger.LogInformation("Session cleanup service stopped");
    }
}

/// <summary>
/// Configuration options for session cleanup.
/// </summary>
public class SessionCleanupOptions
{
    /// <summary>
    /// Gets or sets the interval between cleanup runs.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}