namespace Softoverse.EventBus.InMemory.Abstractions;

public interface IEventBus
{
    // Fire-and-forget publish: enqueue background job to process subscribers
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    ValueTask BulkPublishAsync<TEvent>(List<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}