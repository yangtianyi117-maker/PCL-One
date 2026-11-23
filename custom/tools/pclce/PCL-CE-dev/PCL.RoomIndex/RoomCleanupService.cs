namespace PCL.RoomIndex;

public sealed class RoomCleanupService(RoomStore store, ILogger<RoomCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var removed = store.Cleanup();
                if (removed > 0) logger.LogInformation("清理过期房间 {Count} 个", removed);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "清理过期房间失败");
            }
        }
    }
}
