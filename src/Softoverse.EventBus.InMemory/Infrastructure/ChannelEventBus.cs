using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;

namespace Softoverse.EventBus.InMemory.Infrastructure;

internal class ChannelEventBus(
    EventChannelProvider channelProvider,
    IEventProcessor eventProcessor,
    ILogger<ChannelEventBus> logger)
    : IEventBus
{

    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                              EventBusDiagnostics.ActivityPublish,
                                                                              ActivityKind.Producer);

        var eventType = typeof(TEvent).Name;
        activity?.SetTag(EventBusDiagnostics.TagEventType, eventType);

        if (@event == null!)
        {
            logger.LogWarning("[ChannelEventBus] Ignored null event of type {EventType}", eventType);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "ignored_null");
            return;
        }

        // Add event ID if available through reflection
        var eventIdProperty = @event.GetType().GetProperty("Id");
        if (eventIdProperty?.GetValue(@event) is Guid eventId)
        {
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.ToString());
        }

        logger.LogInformation("[ChannelEventBus] Publishing {EventType}", eventType);

        try
        {
            await channelProvider.PublishingChannel.Writer.WriteAsync(@event, cancellationToken).ConfigureAwait(false);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
            activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "[ChannelEventBus] Failed to publish event {EventType}", eventType);
            throw;
        }
    }

    public async ValueTask BulkPublishAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                              EventBusDiagnostics.ActivityBulkPublish,
                                                                              ActivityKind.Producer);

        var eventType = typeof(TEvent).Name;
        var eventsList = events.ToList();
        var eventCount = eventsList.Count;

        activity?.SetTag(EventBusDiagnostics.TagEventType, eventType);
        activity?.SetTag(EventBusDiagnostics.TagEventCount, eventCount);

        logger.LogInformation("[ChannelEventBus] Bulk publishing {EventCount} events of type {EventType}", eventCount, eventType);

        try
        {
            foreach (var @event in eventsList)
            {
                await PublishAsync(@event, cancellationToken).ConfigureAwait(false);
            }

            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
            activity?.SetStatus(ActivityStatusCode.Ok);

            // var publishTasks = events.Select(@event => PublishAsync(@event, cancellationToken).AsTask());
            // await Task.WhenAll(publishTasks);
        }
        catch (Exception ex)
        {
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
            activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "[ChannelEventBus] Failed to bulk publish events of type {EventType}", eventType);
            throw;
        }
    }

    public async ValueTask ScheduleAsync<TEvent>(TEvent @event, DateTimeOffset scheduleTime, CancellationToken cancellationToken = default) where TEvent : class, IEvent
    {
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                              EventBusDiagnostics.ActivitySchedule,
                                                                              ActivityKind.Producer);

        var eventType = typeof(TEvent).Name;
        activity?.SetTag(EventBusDiagnostics.TagEventType, eventType);
        activity?.SetTag(EventBusDiagnostics.TagScheduledTime, scheduleTime.ToString("O"));

        if (@event == null!)
        {
            logger.LogWarning("[ChannelEventBus] Ignored null event of type {EventType}", eventType);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "ignored_null");
            return;
        }

        // Add event ID if available through reflection
        var eventIdProperty = @event.GetType().GetProperty("Id");
        if (eventIdProperty?.GetValue(@event) is Guid eventId)
        {
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.ToString());
        }

        logger.LogInformation("[ChannelEventBus] Scheduling {EventType} for {ScheduledTime}", eventType, scheduleTime);

        try
        {
            await channelProvider.SchedulingChannel.Writer.WriteAsync((@event!, scheduleTime), cancellationToken).ConfigureAwait(false);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
            activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "[ChannelEventBus] Failed to schedule event {EventType}", eventType);
            throw;
        }
    }

    public async ValueTask BulkScheduleAsync<TEvent>(IEnumerable<TEvent> events, DateTimeOffset scheduleTime, CancellationToken cancellationToken = default) where TEvent : class, IEvent
    {
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                              EventBusDiagnostics.ActivityBulkSchedule,
                                                                              ActivityKind.Producer);

        var eventType = typeof(TEvent).Name;
        var eventsList = events.ToList();
        var eventCount = eventsList.Count;

        activity?.SetTag(EventBusDiagnostics.TagEventType, eventType);
        activity?.SetTag(EventBusDiagnostics.TagEventCount, eventCount);
        activity?.SetTag(EventBusDiagnostics.TagScheduledTime, scheduleTime.ToString("O"));

        logger.LogInformation("[ChannelEventBus] Bulk scheduling {EventCount} events of type {EventType} for {ScheduledTime}",
                              eventCount, eventType, scheduleTime);

        try
        {
            foreach (var @event in eventsList)
            {
                await ScheduleAsync(@event, scheduleTime, cancellationToken).ConfigureAwait(false);
            }

            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
            activity?.SetStatus(ActivityStatusCode.Ok);

            // var publishTasks = events.Select(@event => ScheduleAsync(@event, scheduleTime, cancellationToken).AsTask());
            // await Task.WhenAll(publishTasks);
        }
        catch (Exception ex)
        {
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
            activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "[ChannelEventBus] Failed to bulk schedule events of type {EventType}", eventType);
            throw;
        }
    }

    public async ValueTask<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default)
    {
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                              EventBusDiagnostics.ActivityInvoke,
                                                                              ActivityKind.Client);

        var eventType = @event?.GetType().Name;
        var resultType = typeof(TResult).Name;

        activity?.SetTag(EventBusDiagnostics.TagEventType, eventType ?? "null");
        activity?.SetTag(EventBusDiagnostics.TagResultType, resultType);

        if (@event == null!)
        {
            logger.LogWarning("[ChannelEventBus] Ignored null event");
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "ignored_null");
            return default!;
        }

        // Add event ID if available through reflection
        var eventIdProperty = @event.GetType().GetProperty("Id");
        if (eventIdProperty?.GetValue(@event) is Guid eventId)
        {
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.ToString());
        }

        logger.LogInformation("[ChannelEventBus] Invoking {EventType} expecting result {ResultType}", eventType, resultType);

        try
        {
            var result = await eventProcessor.InvokeAsync<TResult>(@event, cancellationToken).ConfigureAwait(false);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
            activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "[ChannelEventBus] Failed to invoke event {EventType}", eventType);
            throw;
        }
    }
}
