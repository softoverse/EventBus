using Softoverse.EventBus.InMemory.Abstractions;

namespace SampleCore;

public class TestEvent(int id) : IEvent
{
    public int Id { get; set; } = id;
}

public class TestRequest(int id) : IEvent,
                                   IRequest
{
    public int Id { get; set; } = id;
}

public class TestScheduledEvent(int id) : IEvent
{
    public int Id { get; set; } = id;
}