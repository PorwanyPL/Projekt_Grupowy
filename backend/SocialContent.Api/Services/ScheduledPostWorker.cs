namespace SocialContent.Api.Services;

/// <summary>Co kilkadziesiąt sekund publikuje zaplanowane posty, których termin już nadszedł.</summary>
public class ScheduledPostWorker(IServiceScopeFactory scopes, ILogger<ScheduledPostWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IPostPublisher>();
                var count = await publisher.PublishDueAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Opublikowano zaplanowane posty: {Count}.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Błąd podczas publikacji zaplanowanych postów.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
