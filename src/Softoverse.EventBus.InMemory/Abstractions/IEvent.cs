namespace Softoverse.EventBus.InMemory.Abstractions;

public interface IEvent
{
    Guid Id { get; set; }
}

public abstract class EventBase(Guid? id = null) : IEvent
{
    public Guid Id { get; set; } = id ?? Guid.CreateVersion7();
}