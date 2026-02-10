namespace Softoverse.EventBus.InMemory.Abstractions;

public interface IEventBus
{
    // Fire-and-forget publish: enqueue a background job to process subscribers
    
    /// <summary>
    /// Publishes a single event to all its subscribers. This is a fire-and-forget operation, where the event is enqueued for processing without waiting for completion.
    /// </summary>
    /// <param name="event"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TEvent"></typeparam>
    /// <returns></returns>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>
    /// Publishes multiple events in bulk. This can be optimized by the implementation to reduce overhead, such as batching or parallel processing.
    /// </summary>
    /// <param name="events"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TEvent"></typeparam>
    /// <returns></returns>
    ValueTask BulkPublishAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
    
    /// <summary>
    /// Schedules an event to be processed at a specific time.
    /// </summary>
    /// <param name="event">The event to schedule.</param>
    /// <param name="scheduleTime">The time when the event should be processed (automatically converted to UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ScheduleAsync<TEvent>(TEvent @event, DateTimeOffset scheduleTime, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
    
    /// <summary>
    /// Schedules multiple events to be processed at a specific time.
    /// </summary>
    /// <param name="events">The events to schedule.</param>
    /// <param name="scheduleTime">The time when the events should be processed (automatically converted to UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask BulkScheduleAsync<TEvent>(IEnumerable<TEvent> events, DateTimeOffset scheduleTime, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>
    /// Invokes a request handler for the given event and returns the result.
    /// </summary>
    /// <param name="event">The type to execute as request</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TResult">Can be of any type</typeparam>
    /// <returns></returns>
    ValueTask<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default);
}