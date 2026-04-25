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
            logger.IgnoredNullEvent(eventType);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "ignored_null");
            return;
        }

        // Add event ID if available through reflection
        var eventIdProperty = @event.GetType().GetProperty("Id");
        if (eventIdProperty?.GetValue(@event) is Guid eventId)
        {
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.ToString());
        }

        logger.PublishingEvent(eventType);

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
            logger.PublishEventFailed(ex, eventType);
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

        logger.BulkPublishingEvents(eventCount, eventType);

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
            logger.BulkPublishEventsFailed(ex, eventType);
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
            logger.IgnoredNullEvent(eventType);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "ignored_null");
            return;
        }

        // Add event ID if available through reflection
        var eventIdProperty = @event.GetType().GetProperty("Id");
        if (eventIdProperty?.GetValue(@event) is Guid eventId)
        {
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.ToString());
        }

        logger.SchedulingEvent(eventType, scheduleTime);

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
            logger.ScheduleEventFailed(ex, eventType);
            throw;
        }
    }

    public ValueTask ScheduleAsync<TEvent>(TEvent @event, TimeSpan delay, CancellationToken cancellationToken = default) where TEvent : class, IEvent
    {
        return ScheduleAsync(@event, DateTimeOffset.UtcNow.Add(delay), cancellationToken);
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

        logger.BulkSchedulingEvents(eventCount, eventType, scheduleTime);

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
            logger.BulkScheduleEventsFailed(ex, eventType);
            throw;
        }
    }

    public ValueTask BulkScheduleAsync<TEvent>(IEnumerable<TEvent> events, TimeSpan delay, CancellationToken cancellationToken = default) where TEvent : class, IEvent
    {
        return BulkScheduleAsync(events, DateTimeOffset.UtcNow.Add(delay), cancellationToken);
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
            logger.IgnoredNullEventInvoke();
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "ignored_null");
            return default!;
        }

        // Add event ID if available through reflection
        var eventIdProperty = @event.GetType().GetProperty("Id");
        if (eventIdProperty?.GetValue(@event) is Guid eventId)
        {
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.ToString());
        }

        logger.InvokingEvent(eventType!, resultType);

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
            logger.InvokeEventFailed(ex, eventType!);
            throw;
        }
    }
}
