using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;

namespace Softoverse.EventBus.InMemory.Infrastructure.General;

public class GeneralEventBus(
    IEventProcessor eventProcessor,
    ILogger<GeneralEventBus> logger)
    : IEventBus
{
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        if (@event == null!)
        {
            logger.LogWarning("[GeneralEventBus] Ignored null event of type {EventType}", typeof(TEvent).Name);
            return;
        }
        try
        {
            await eventProcessor.ProcessEventAsync(@event!, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[GeneralEventBus] Failed to process event {EventType}", @event?.GetType().Name ?? "null");
        }
    }

    public async ValueTask BulkPublishAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        foreach (var @event in events)
        {
            await PublishAsync(@event, cancellationToken).ConfigureAwait(false);
        }

        // var publishTasks = events.Select(@event => PublishAsync(@event, cancellationToken).AsTask());
        // await Task.WhenAll(publishTasks);
    }

    public async ValueTask ScheduleAsync<TEvent>(TEvent @event, DateTimeOffset scheduleTime, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        if (@event == null!)
        {
            logger.LogWarning("[GeneralEventBus] Ignored null event of type {EventType}", typeof(TEvent).Name);
            return;
        }
        try
        {
            await eventProcessor.ProcessScheduledEventAsync(@event!, scheduleTime, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[GeneralEventBus] Failed to process event {EventType}", @event?.GetType().Name ?? "null");
        }
    }

    public async ValueTask BulkScheduleAsync<TEvent>(IEnumerable<TEvent> events, DateTimeOffset scheduleTime, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        foreach (var @event in events)
        {
            await ScheduleAsync(@event, scheduleTime, cancellationToken).ConfigureAwait(false);
        }

        // var publishTasks = events.Select(@event => ScheduleAsync(@event, scheduleTime, cancellationToken).AsTask());
        // await Task.WhenAll(publishTasks);
    }

    public async ValueTask<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default)
    {
        if (@event == null!)
        {
            logger.LogWarning("[GeneralEventBus] Ignored null event of type {EventType}", @event.GetType().Name);
        }
        try
        {
            return await eventProcessor.InvokeAsync<TResult>(@event!, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[GeneralEventBus] Failed to invoke event {EventType}", @event?.GetType().Name ?? "null");
        }
        return default!;
    }
}
