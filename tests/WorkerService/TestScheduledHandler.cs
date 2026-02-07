using Softoverse.EventBus.InMemory.Abstractions;

namespace WorkerService;

internal class TestScheduledEvent(int id) : IEvent
{
    public int Id { get; set; } = id;
}

internal class TestScheduledHandler(EventTracker eventTracker, ILogger<TestScheduledHandler> logger) : IEventHandler<TestScheduledEvent>
{
    public async Task HandleAsync(TestScheduledEvent @event, CancellationToken cancellationToken = default)
    {
        eventTracker.ScheduledEvents.Add(@event.Id);
        logger.LogWarning("Handled scheduled event with ID: {Id}", @event.Id);
        await Task.Delay(2000, cancellationToken);
    }
}
