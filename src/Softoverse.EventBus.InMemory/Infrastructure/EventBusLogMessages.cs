using Microsoft.Extensions.Logging;

namespace Softoverse.EventBus.InMemory.Infrastructure;

/// <summary>
/// High-performance structured logging using LoggerMessage source generators.
/// Provides standardized log messages for all EventBus operations.
/// </summary>
internal static partial class EventBusLogMessages
{
    // ===== ChannelEventBus Logs =====
    
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Publishing event of type '{EventType}'")]
    public static partial void PublishingEvent(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Ignored null event of type '{EventType}'")]
    public static partial void IgnoredNullEvent(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Failed to publish event of type '{EventType}'")]
    public static partial void PublishEventFailed(this ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Bulk publishing {EventCount} events of type '{EventType}'")]
    public static partial void BulkPublishingEvents(this ILogger logger, int eventCount, string eventType);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Error,
        Message = "Failed to bulk publish events of type '{EventType}'")]
    public static partial void BulkPublishEventsFailed(this ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Scheduling event of type '{EventType}' for {ScheduledTime}")]
    public static partial void SchedulingEvent(this ILogger logger, string eventType, DateTimeOffset scheduledTime);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Error,
        Message = "Failed to schedule event of type '{EventType}'")]
    public static partial void ScheduleEventFailed(this ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Bulk scheduling {EventCount} events of type '{EventType}' for {ScheduledTime}")]
    public static partial void BulkSchedulingEvents(this ILogger logger, int eventCount, string eventType, DateTimeOffset scheduledTime);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Error,
        Message = "Failed to bulk schedule events of type '{EventType}'")]
    public static partial void BulkScheduleEventsFailed(this ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Warning,
        Message = "Ignored null event in InvokeAsync")]
    public static partial void IgnoredNullEventInvoke(this ILogger logger);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Invoking event of type '{EventType}' expecting result '{ResultType}'")]
    public static partial void InvokingEvent(this ILogger logger, string eventType, string resultType);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Error,
        Message = "Failed to invoke event of type '{EventType}'")]
    public static partial void InvokeEventFailed(this ILogger logger, Exception exception, string eventType);

    // ===== InMemoryEventProcessor Logs =====

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Warning,
        Message = "Ignored null event in ProcessEventAsync")]
    public static partial void IgnoredNullEventInProcessor(this ILogger logger);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Processing event of type '{EventType}'")]
    public static partial void ProcessingEvent(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Error,
        Message = "Failed to process event of type '{EventType}'")]
    public static partial void ProcessEventFailed(this ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Ignored null scheduled event")]
    public static partial void IgnoredNullScheduledEvent(this ILogger logger);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Scheduling event of type '{EventType}' for {ScheduledTime} (UTC)")]
    public static partial void SchedulingEventForProcessing(this ILogger logger, string eventType, DateTimeOffset scheduledTime);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Error,
        Message = "Failed to schedule event of type '{EventType}'")]
    public static partial void ScheduleEventForProcessingFailed(this ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Warning,
        Message = "No handlers found for event type '{EventType}'")]
    public static partial void NoHandlersFound(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "Found {HandlerCount} handler(s) for event type '{EventType}'")]
    public static partial void HandlersFound(this ILogger logger, int handlerCount, string eventType);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Warning,
        Message = "Ignored null event in InvokeAsync")]
    public static partial void IgnoredNullEventInProcessorInvoke(this ILogger logger);

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Information,
        Message = "Invoking event of type '{EventType}' in processor")]
    public static partial void InvokingEventInProcessor(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Warning,
        Message = "No handler found for event type '{EventType}' in InvokeAsync")]
    public static partial void NoHandlerFoundForInvoke(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Error,
        Message = "Failed to invoke event of type '{EventType}' in processor")]
    public static partial void InvokeEventInProcessorFailed(this ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        EventId = 2012,
        Level = LogLevel.Information,
        Message = "Handler '{HandlerType}' successfully processed event of type '{EventType}'")]
    public static partial void HandlerSucceeded(this ILogger logger, string handlerType, string eventType);

    [LoggerMessage(
        EventId = 2013,
        Level = LogLevel.Error,
        Message = "Handler '{HandlerType}' failed to process event of type '{EventType}'")]
    public static partial void HandlerFailed(this ILogger logger, Exception exception, string handlerType, string eventType);

    // ===== Background Services Logs =====

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "EventPublishingHostedService started")]
    public static partial void PublishingServiceStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "EventPublishingHostedService stopped")]
    public static partial void PublishingServiceStopped(this ILogger logger);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Received null event from publishing channel")]
    public static partial void ReceivedNullEventFromPublishingChannel(this ILogger logger);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Received event of type '{EventType}' from publishing channel")]
    public static partial void ReceivedEventFromPublishingChannel(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Error,
        Message = "Failed to process event of type '{EventType}' from publishing channel")]
    public static partial void PublishingChannelProcessFailed(this ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Error,
        Message = "Error while reading from publishing channel")]
    public static partial void PublishingChannelReadError(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Information,
        Message = "EventSchedulingHostedService started")]
    public static partial void SchedulingServiceStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Information,
        Message = "EventSchedulingHostedService stopped")]
    public static partial void SchedulingServiceStopped(this ILogger logger);

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Warning,
        Message = "Received null event from scheduling channel")]
    public static partial void ReceivedNullEventFromSchedulingChannel(this ILogger logger);

    [LoggerMessage(
        EventId = 3013,
        Level = LogLevel.Information,
        Message = "Received event of type '{EventType}' from scheduling channel")]
    public static partial void ReceivedEventFromSchedulingChannel(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = 3014,
        Level = LogLevel.Error,
        Message = "Failed to process event of type '{EventType}' from scheduling channel")]
    public static partial void SchedulingChannelProcessFailed(this ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        EventId = 3015,
        Level = LogLevel.Error,
        Message = "Error while reading from scheduling channel")]
    public static partial void SchedulingChannelReadError(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3020,
        Level = LogLevel.Information,
        Message = "ScheduledEventProcessingHostedService started")]
    public static partial void ScheduledEventProcessingServiceStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 3021,
        Level = LogLevel.Information,
        Message = "ScheduledEventProcessingHostedService stopped")]
    public static partial void ScheduledEventProcessingServiceStopped(this ILogger logger);

    [LoggerMessage(
        EventId = 3022,
        Level = LogLevel.Error,
        Message = "Error in scheduled event processing main loop")]
    public static partial void ScheduledEventProcessingMainLoopError(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3023,
        Level = LogLevel.Information,
        Message = "Found {EventCount} due event(s) to process")]
    public static partial void FoundDueEvents(this ILogger logger, int eventCount);

    [LoggerMessage(
        EventId = 3024,
        Level = LogLevel.Warning,
        Message = "Failed to mark event {EventId} as InProgress, skipping")]
    public static partial void FailedToMarkEventInProgress(this ILogger logger, Guid eventId);

    [LoggerMessage(
        EventId = 3025,
        Level = LogLevel.Information,
        Message = "Processing scheduled event {EventId} of type '{EventType}'")]
    public static partial void ProcessingScheduledEvent(this ILogger logger, Guid eventId, string eventType);

    [LoggerMessage(
        EventId = 3026,
        Level = LogLevel.Information,
        Message = "Successfully processed and removed scheduled event {EventId}")]
    public static partial void ScheduledEventProcessedSuccessfully(this ILogger logger, Guid eventId);

    [LoggerMessage(
        EventId = 3027,
        Level = LogLevel.Error,
        Message = "Failed to process scheduled event {EventId} of type '{EventType}'")]
    public static partial void ScheduledEventProcessingFailed(this ILogger logger, Exception exception, Guid eventId, string eventType);

    // ===== ScheduledEventStore Logs =====

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Debug,
        Message = "Added scheduled event {EventId} of type '{EventType}' for {ScheduledTime}")]
    public static partial void ScheduledEventAdded(this ILogger logger, Guid eventId, string eventType, DateTimeOffset scheduledTime);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "Failed to add scheduled event {EventId} (duplicate ID)")]
    public static partial void ScheduledEventAddFailed(this ILogger logger, Guid eventId);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Debug,
        Message = "Removed scheduled event {EventId} of type '{EventType}'")]
    public static partial void ScheduledEventRemoved(this ILogger logger, Guid eventId, string eventType);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Warning,
        Message = "Failed to remove scheduled event {EventId} (not found)")]
    public static partial void ScheduledEventRemoveFailed(this ILogger logger, Guid eventId);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Warning,
        Message = "Failed to update status for event {EventId} (not found)")]
    public static partial void ScheduledEventStatusUpdateFailed(this ILogger logger, Guid eventId);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Debug,
        Message = "Updated event {EventId} status from {OldStatus} to {NewStatus}")]
    public static partial void ScheduledEventStatusUpdated(this ILogger logger, Guid eventId, ScheduledEventStatus oldStatus, ScheduledEventStatus newStatus);
}

