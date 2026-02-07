using Softoverse.EventBus.InMemory.Abstractions;

namespace EventBus.InMemory.Tests.Implementations;

public class TestEventHandler(EventTracker eventTracker) : IEventHandler<TestEvent>
{
    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
    {
        eventTracker.ProcessedEvents.Add(@event.Id);
        return Task.CompletedTask;
    }
}

public class TestScheduledHandler(EventTracker eventTracker) : IEventHandler<TestScheduledEvent>
{
    public Task HandleAsync(TestScheduledEvent @event, CancellationToken cancellationToken = default)
    {
        eventTracker.ScheduledEvents.Add(@event.Id);
        return Task.CompletedTask;
    }
}

public class TestRequestHandler(EventTracker eventTracker) : IRequestHandler<TestRequest, bool>
{
    public Task<bool> HandleAsync(TestRequest @event, CancellationToken cancellationToken = default)
    {
        eventTracker.InvokedEvents.Add(@event.Id);
        return Task.FromResult(true);
    }
}