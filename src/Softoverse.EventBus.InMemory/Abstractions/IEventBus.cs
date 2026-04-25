namespace Softoverse.EventBus.InMemory.Abstractions;

/// <summary>
/// Publishes and schedules events and invokes request-style events.
/// Implementations are responsible for dispatching events to registered handlers, scheduling execution, and invoking request handlers.
/// </summary>
public interface IEventBus
{
    // Fire-and-forget publish: enqueue a background job to process subscribers
    
    /// <summary>
    /// Publishes a single event to all its subscribers. This is a fire-and-forget operation, where the event is enqueued for processing without waiting for completion.
    /// </summary>
    /// <typeparam name="TEvent">Type of the event. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="cancellationToken">A token to cancel the publish operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the publish request has been accepted by the bus.</returns>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>
    /// Publishes multiple events in bulk. This can be optimized by the implementation to reduce overhead, such as batching or parallel processing.
    /// </summary>
    /// <typeparam name="TEvent">Type of the events. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="events">The events to publish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the bulk publish request has been accepted by the bus.</returns>
    ValueTask BulkPublishAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
    
    /// <summary>
    /// Schedules an event to be processed at a specific time.
    /// </summary>
    /// <typeparam name="TEvent">Type of the event. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="event">The event to schedule.</param>
    /// <param name="scheduleTime">The time when the event should be processed (automatically converted to UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the schedule request has been accepted.</returns>
    ValueTask ScheduleAsync<TEvent>(TEvent @event, DateTimeOffset scheduleTime, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>
    /// Schedules an event to be processed after a relative delay.
    /// </summary>
    /// <typeparam name="TEvent">Type of the event. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="event">The event to schedule.</param>
    /// <param name="delay">The delay before the event should be processed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the schedule request has been accepted.</returns>
    ValueTask ScheduleAsync<TEvent>(TEvent @event, TimeSpan delay, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
    
    /// <summary>
    /// Schedules multiple events to be processed at a specific time.
    /// </summary>
    /// <typeparam name="TEvent">Type of the events. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="events">The events to schedule.</param>
    /// <param name="scheduleTime">The time when the events should be processed (automatically converted to UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the bulk schedule request has been accepted.</returns>
    ValueTask BulkScheduleAsync<TEvent>(IEnumerable<TEvent> events, DateTimeOffset scheduleTime, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>
    /// Schedules multiple events to be processed after a relative delay.
    /// </summary>
    /// <typeparam name="TEvent">Type of the events. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="events">The events to schedule.</param>
    /// <param name="delay">The delay before the events should be processed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the bulk schedule request has been accepted.</returns>
    ValueTask BulkScheduleAsync<TEvent>(IEnumerable<TEvent> events, TimeSpan delay, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>
    /// Invokes a request handler for the given event and returns the result.
    /// This call waits for the handler to execute and returns its result.
    /// </summary>
    /// <typeparam name="TResult">The expected type of the response.</typeparam>
    /// <param name="event">The request object (typically implementing <see cref="IRequest"/>).</param>
    /// <param name="cancellationToken">A token to cancel the invocation.</param>
    /// <returns>The handler result.</returns>
    ValueTask<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default);
}
