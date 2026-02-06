using System.Collections.Concurrent;

using Softoverse.EventBus.InMemory.Abstractions;

namespace EventBus.InMemory.Tests.Implementations;

internal class TestEvent(int id) : IEvent
{
    public int Id { get; set; } = id;
}

internal static class EventTracker
{
    public static ConcurrentBag<int> ProcessedEvents { get; } = new();
    public static ConcurrentBag<int> ScheduledEvents { get; } = new();
    public static ConcurrentBag<int> InvokedEvents { get; } = new();

    public static void Clear()
    {
        ProcessedEvents.Clear();
        ScheduledEvents.Clear();
        InvokedEvents.Clear();
    }
}

internal class TestEventHandler : IEventHandler<TestEvent>
{
    public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
    {
        EventTracker.ProcessedEvents.Add(@event.Id);
        return Task.CompletedTask;
    }
}

internal class TestInvokeHandler
{
    public Task<bool> HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
    {
        EventTracker.InvokedEvents.Add(@event.Id);
        return Task.FromResult(true);
    }
}
