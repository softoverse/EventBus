using System.Collections.Concurrent;
using Softoverse.EventBus.InMemory.Abstractions;

namespace EventBus.InMemory.Tests.Implementations;

public class EventTracker
{
    public ConcurrentBag<int> ProcessedEvents { get; } = new();
    public ConcurrentBag<int> ScheduledEvents { get; } = new();
    public ConcurrentBag<int> InvokedEvents { get; } = new();

    public void Clear()
    {
        ProcessedEvents.Clear();
        ScheduledEvents.Clear();
        InvokedEvents.Clear();
    }
}

internal class TestEvent(int id) : IEvent
{
    public int Id { get; set; } = id;
}

internal class TestRequest(int id) : IEvent,
                                     IRequest
{
    public int Id { get; set; } = id;
}

internal class TestScheduledEvent(int id) : IEvent
{
    public int Id { get; set; } = id;
}

internal class TestEventHandler(EventTracker eventTracker) : IEventHandler<TestEvent>
{
    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
    {
        eventTracker.ProcessedEvents.Add(@event.Id);
        return Task.CompletedTask;
    }
}

internal class TestScheduledHandler(EventTracker eventTracker) : IEventHandler<TestScheduledEvent>
{
    public Task HandleAsync(TestScheduledEvent @event, CancellationToken cancellationToken = default)
    {
        eventTracker.ScheduledEvents.Add(@event.Id);
        return Task.CompletedTask;
    }
}

internal class TestRequestHandler(EventTracker eventTracker) : IRequestHandler<TestRequest, bool>
{
    public Task<bool> HandleAsync(TestRequest @event, CancellationToken cancellationToken = default)
    {
        eventTracker.InvokedEvents.Add(@event.Id);
        return Task.FromResult(true);
    }
}
