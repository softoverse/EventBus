namespace Softoverse.EventBus.InMemory.Abstractions;

/// <summary>
/// Non-generic contract for event handlers used for container resolution and dynamic dispatch.
/// Implementations should determine whether they can handle a given <see cref="IEvent"/> via <see cref="CanHandle"/> and execute handling logic in <see cref="HandleAsync(IEvent, CancellationToken)"/>.
/// </summary>
public interface IEventHandler
{
    /// <summary>
    /// Determines whether this handler can handle the provided event instance.
    /// </summary>
    /// <param name="event">Event instance to check.</param>
    /// <returns>True if the handler can process the event; otherwise false.</returns>
    bool CanHandle(IEvent @event);

    /// <summary>
    /// Handles the provided event instance.
    /// </summary>
    /// <param name="event">Event instance to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(IEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Strongly-typed event handler for events of type <typeparamref name="TEvent"/>.
/// Provides bridge implementations so generic handlers can be resolved via the non-generic <see cref="IEventHandler"/>.
/// </summary>
/// <typeparam name="TEvent">The event type handled by this handler.</typeparam>
public interface IEventHandler<in TEvent> : IEventHandler where TEvent : IEvent
{
    /// <summary>
    /// Handles the strongly-typed event instance.
    /// </summary>
    /// <param name="event">The strongly-typed event to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);

    // Bridge implementations
    bool IEventHandler.CanHandle(IEvent @event) => @event is TEvent;

    Task IEventHandler.HandleAsync(IEvent @event, CancellationToken cancellationToken)
        => @event is TEvent typed ? HandleAsync(typed, cancellationToken) : Task.CompletedTask;
}