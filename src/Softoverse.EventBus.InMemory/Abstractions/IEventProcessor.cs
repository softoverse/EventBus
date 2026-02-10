namespace Softoverse.EventBus.InMemory.Abstractions;

/// <summary>
/// Coordinates event processing by resolving handlers and executing them.
/// Implementations may provide retry, scheduling and isolation behavior.
/// </summary>
public interface IEventProcessor
{
    /// <summary>
    /// Processes an event immediately by resolving and invoking applicable handlers.
    /// </summary>
    /// <param name="event">The event to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Processes a scheduled event at the specified time.
    /// Implementations should respect the provided <paramref name="scheduledTime"/> (UTC) and delay processing until that time.
    /// </summary>
    /// <param name="event">The event to process.</param>
    /// <param name="scheduledTime">The UTC time when the event should be processed (DateTimeKind.Utc).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessScheduledEventAsync(IEvent @event, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves and invokes all applicable event handlers for the provided event.
    /// This method typically executes handlers in parallel and isolates failures per handler.
    /// </summary>
    /// <param name="event">The event whose handlers should be executed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Invokes a request-style handler and returns its result.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="event">The request object to invoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The handler result.</returns>
    Task<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default);
}