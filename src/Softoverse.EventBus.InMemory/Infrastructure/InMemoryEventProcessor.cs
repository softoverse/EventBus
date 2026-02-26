using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;

namespace Softoverse.EventBus.InMemory.Infrastructure;

internal class InMemoryEventProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<InMemoryEventProcessor> logger,
    ScheduledEventStore scheduledEventStore) : IEventProcessor
{

    public async Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(EventBusDiagnostics.ActivityProcessEvent,
                                                                              ActivityKind.Consumer);

        if (@event == null!)
        {
            logger.IgnoredNullEventInProcessor();
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "ignored_null");
            return;
        }

        var eventType = @event.GetType().Name;
        activity?.SetTag(EventBusDiagnostics.TagEventType, eventType);

        var eventId = TryGetEventId(@event);
        if (eventId.HasValue)
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.Value.ToString());

        logger.ProcessingEvent(eventType);

        try
        {
            await ProcessEventHandlersAsync(@event, cancellationToken).ConfigureAwait(false);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
            activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.ProcessEventFailed(ex, eventType);
            throw;
        }
    }

    public Task ProcessScheduledEventAsync(IEvent @event, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default)
    {
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                              EventBusDiagnostics.ActivityProcessScheduledEvent,
                                                                              ActivityKind.Consumer);

        if (@event == null!)
        {
            logger.IgnoredNullScheduledEvent();
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "ignored_null");
            return Task.CompletedTask;
        }

        var scheduledTimeUtc = scheduledTime.ToUniversalTime();
        var eventType = @event.GetType().Name;

        activity?.SetTag(EventBusDiagnostics.TagEventType, eventType);
        activity?.SetTag(EventBusDiagnostics.TagScheduledTime, scheduledTimeUtc.ToString("O"));

        var eventId = TryGetEventId(@event);
        if (eventId.HasValue)
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.Value.ToString());

        logger.SchedulingEventForProcessing(eventType, scheduledTimeUtc);

        try
        {
            // Store the event in the in-memory store for background processing
            scheduledEventStore.AddScheduledEvent(@event, scheduledTimeUtc);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
            activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.ScheduleEventForProcessingFailed(ex, eventType);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null!)
        {
            logger.IgnoredNullEventInProcessor();
            return Task.CompletedTask;
        }

        // Capture the parent context BEFORE entering Task.Run so the child span
        // is correctly linked even after the caller's span has been disposed.
        var parentContext = Activity.Current?.Context ?? default;

        _ = Task.Run(async () =>
        {
            var eventType = @event.GetType().Name;

            using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
                EventBusDiagnostics.ActivityProcessHandlers,
                ActivityKind.Internal,
                parentContext);

            activity?.SetTag(EventBusDiagnostics.TagEventType, eventType);

            var eventId = TryGetEventId(@event);
            if (eventId.HasValue)
                activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.Value.ToString());

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handlers = scope.ServiceProvider.GetServices<IEventHandler>();

                var applicableHandlers = handlers.Where(h => h.CanHandle(@event)).ToList();

                if (applicableHandlers.Count == 0)
                {
                    logger.NoHandlersFound(eventType);
                    activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusNoHandlers);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return;
                }

                logger.HandlersFound(applicableHandlers.Count, eventType);
                activity?.SetTag(EventBusDiagnostics.TagHandlerCount, applicableHandlers.Count);

                // Pass the ProcessHandlers span context so each SafeHandleAsync child
                // span is correctly nested under it, not orphaned.
                var processHandlersContext = activity?.Context ?? default;

                var handlerTasks = applicableHandlers.Select(handler =>
                    SafeHandleAsync(handler, @event, processHandlersContext, cancellationToken));

                await Task.WhenAll(handlerTasks).ConfigureAwait(false);

                activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
                activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.ProcessEventFailed(ex, eventType);
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public async Task<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default)
    {
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                              EventBusDiagnostics.ActivityInvoke,
                                                                              ActivityKind.Internal);

        if (@event == null!)
        {
            logger.IgnoredNullEventInProcessorInvoke();
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "ignored_null");
            return default!;
        }

        var eventType = @event.GetType().Name;
        var resultType = typeof(TResult).Name;

        activity?.SetTag(EventBusDiagnostics.TagEventType, eventType);
        activity?.SetTag(EventBusDiagnostics.TagResultType, resultType);

        var eventId = TryGetEventId(@event);
        if (eventId.HasValue)
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.Value.ToString());

        logger.InvokingEventInProcessor(eventType);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var handlerType = typeof(IRequestHandler<,>)
                .MakeGenericType(@event.GetType(), typeof(TResult));

            object? handler = scope.ServiceProvider.GetService(handlerType);

            if (handler is IRequestHandler baseHandler &&
                baseHandler.CanHandle(@event))
            {
                activity?.SetTag(EventBusDiagnostics.TagHandlerType, handler.GetType().Name);

                object? result = await baseHandler
                    .HandleAsync(@event, cancellationToken);

                activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
                activity?.SetStatus(ActivityStatusCode.Ok);

                return (TResult?) result!;
            }

            logger.NoHandlerFoundForInvoke(eventType);
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusNoHandlers);
            return default!;

        }
        catch (Exception ex)
        {
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
            activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.InvokeEventInProcessorFailed(ex, eventType);
            throw;
        }
    }

    private async Task SafeHandleAsync(IEventHandler handler, IEvent @event, ActivityContext parentContext, CancellationToken cancellationToken)
    {
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
            EventBusDiagnostics.ActivityHandleEvent,
            ActivityKind.Internal,
            parentContext);

        var handlerType = handler.GetType().Name;
        var eventType = @event.GetType().Name;

        activity?.SetTag(EventBusDiagnostics.TagHandlerType, handlerType);
        activity?.SetTag(EventBusDiagnostics.TagEventType, eventType);

        var eventId = TryGetEventId(@event);
        if (eventId.HasValue)
            activity?.SetTag(EventBusDiagnostics.TagEventId, eventId.Value.ToString());

        try
        {
            await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);

            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
            activity?.SetStatus(ActivityStatusCode.Ok);

            logger.HandlerSucceeded(handlerType, eventType);
        }
        catch (Exception ex)
        {
            activity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
            activity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            logger.HandlerFailed(ex, handlerType, eventType);
            // Don't re-throw to allow other handlers to continue processing
        }
    }

    /// <summary>
    /// Centralised, reflection-based helper that returns the event's <c>Id</c> property
    /// value when it is a <see cref="Guid"/>, avoiding the repeated inline lookup.
    /// </summary>
    private static Guid? TryGetEventId(object @event)
    {
        var property = @event.GetType().GetProperty("Id");
        return property?.GetValue(@event) is Guid id ? id : null;
    }
}
