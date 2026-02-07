using System.Collections.Concurrent;

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