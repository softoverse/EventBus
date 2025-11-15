namespace Softoverse.EventBus.InMemory.Abstractions;

public interface IEventProcessor
{
    Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default);

    Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default);
}