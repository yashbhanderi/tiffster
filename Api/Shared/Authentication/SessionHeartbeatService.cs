using Api.Shared.Caching;
using Api.Shared.Dtos;
using Microsoft.Extensions.Options;

namespace Api.Shared.Authentication;

public class SessionHeartbeatService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionHeartbeatService> _logger;
    private readonly HeartbeatSettings _settings;

    public SessionHeartbeatService(
        IServiceProvider serviceProvider,
        ILogger<SessionHeartbeatService> logger,
        IOptions<HeartbeatSettings> settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session Heartbeat Service is starting.");

        // Run the heartbeat check at the configured interval
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // await CheckExpiredSessionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking expired sessions");
            }

            // Wait for the next check interval
            await Task.Delay(TimeSpan.FromMinutes(_settings.CheckIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("Session Heartbeat Service is stopping.");
    }

    private async Task CheckExpiredSessionsAsync()
    {
        _logger.LogInformation("Starting expired session check");

        // Create a scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();
        var sessionTracker = scope.ServiceProvider.GetRequiredService<SessionTrackingService>();

        // Get all active sessions
        var sessions = await sessionTracker.GetAllSessionsAsync();
        int expiredCount = 0;

        // Check each session for expiry
        foreach (var sessionName in sessions)
        {
            var expiryTime = await sessionTracker.GetSessionExpiryAsync(sessionName);

            if (expiryTime.HasValue)
            {
                // Check if session has expired
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiryTime.Value)
                {
                    _logger.LogInformation("Session {SessionName} has expired. Ending session.", sessionName);

                    // End the session (remove from Redis)
                    await sessionTracker.RemoveSessionAsync(sessionName);
                    expiredCount++;
                }
            }
        }

        _logger.LogInformation("Expired session check completed. Removed {ExpiredCount} expired sessions.",
            expiredCount);
    }
}