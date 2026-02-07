using Softoverse.EventBus.InMemory;
using Softoverse.EventBus.InMemory.Abstractions;
using WorkerService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddEventBus(builder.Configuration, typeof(Program).Assembly);
builder.Services.AddSingleton(new EventTracker());

var host = builder.Build();
await host.RunAsync();


public class Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger) : BackgroundService
{
    private static int _currentId = 0;

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ensure the scope is created before resolving services
        await using var scope = scopeFactory.CreateAsyncScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
            for (int i = 0; i < 10; i++)
            {
                await eventBus.ScheduleAsync(new TestScheduledEvent(++_currentId), DateTimeOffset.UtcNow.AddSeconds(10), stoppingToken);
            }
        }
    }
}

