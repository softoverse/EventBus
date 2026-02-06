namespace Softoverse.EventBus.InMemory.Abstractions;

public interface IEventProcessor
{
    Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Processes a scheduled event at the specified time.
    /// </summary>
    /// <param name="event">The event to process.</param>
    /// <param name="scheduledTime">The UTC time when the event should be processed (DateTimeKind.Utc).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessScheduledEventAsync(IEvent @event, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default);

    Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default);
    
    Task<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default);
}