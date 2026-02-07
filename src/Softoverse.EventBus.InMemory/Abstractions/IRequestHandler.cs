namespace Softoverse.EventBus.InMemory.Abstractions;

public interface IRequestHandler
{
    bool CanHandle(object @event);

    Task<object?> HandleAsync(
        object @event,
        CancellationToken cancellationToken = default);
}

    
    
public interface IRequestHandler<in TEvent, TResult> : IRequestHandler
    where TEvent : IEvent
{
    Task<TResult> HandleAsync(
        TEvent @event,
        CancellationToken cancellationToken = default);

    bool IRequestHandler.CanHandle(object @event)
        => @event is TEvent;

    async Task<object?> IRequestHandler.HandleAsync(
        object @event,
        CancellationToken cancellationToken)
    {
        return @event is TEvent typed
            ? await HandleAsync(typed, cancellationToken)
            : default;
    }
}