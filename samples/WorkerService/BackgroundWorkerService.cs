using SampleCore;
using Softoverse.EventBus.InMemory.Abstractions;

namespace WorkerService;

public class BackgroundWorkerService(IServiceScopeFactory scopeFactory, ILogger<BackgroundWorkerService> logger) : BackgroundService
{
    private static int _currentId;

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ensure the scope is created before resolving services
        await using var scope = scopeFactory.CreateAsyncScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(2000, stoppingToken);
            await eventBus.BulkScheduleAsync([
                new TestScheduledEvent(++_currentId),
                new TestScheduledEvent(++_currentId)
            ],
            DateTimeOffset.UtcNow.AddSeconds(4),
            stoppingToken);
        }
    }
}
