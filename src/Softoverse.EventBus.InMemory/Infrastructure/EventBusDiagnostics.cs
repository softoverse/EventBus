using System.Diagnostics;

namespace Softoverse.EventBus.InMemory.Infrastructure;

/// <summary>
/// Provides centralized diagnostics infrastructure for OpenTelemetry tracing.
/// Users can register the ActivitySource name "Softoverse.EventBus.InMemory" in their OpenTelemetry configuration
/// to enable distributed tracing for all event bus operations.
/// </summary>
internal static class EventBusDiagnostics
{
    /// <summary>
    /// The name of the ActivitySource for OpenTelemetry tracing.
    /// Register this name in your OpenTelemetry configuration to enable tracing.
    /// </summary>
    public const string ActivitySourceName = "Softoverse.EventBus.InMemory";

    /// <summary>
    /// The version of the library for telemetry purposes.
    /// </summary>
    public const string Version = "10.7.0";

    /// <summary>
    /// The ActivitySource instance used for creating activities (spans) throughout the library.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version);

    // Activity Names - Following OpenTelemetry semantic conventions
    public const string ActivityPublish = "EventBus.Publish";
    public const string ActivityBulkPublish = "EventBus.BulkPublish";
    public const string ActivitySchedule = "EventBus.Schedule";
    public const string ActivityBulkSchedule = "EventBus.BulkSchedule";
    public const string ActivityInvoke = "EventBus.Invoke";
    public const string ActivityProcessEvent = "EventBus.ProcessEvent";
    public const string ActivityProcessScheduledEvent = "EventBus.ProcessScheduledEvent";
    public const string ActivityHandleEvent = "EventBus.HandleEvent";
    public const string ActivityChannelRead = "EventBus.Channel.Read";
    public const string ActivityChannelProcess = "EventBus.Channel.Process";
    public const string ActivityScheduledEventCheck = "EventBus.ScheduledEvent.Check";

    // Tag Names - Standard OpenTelemetry attributes
    public const string TagEventType = "eventbus.event.type";
    public const string TagEventId = "eventbus.event.id";
    public const string TagEventCount = "eventbus.event.count";
    public const string TagHandlerType = "eventbus.handler.type";
    public const string TagHandlerCount = "eventbus.handler.count";
    public const string TagScheduledTime = "eventbus.scheduled_time";
    public const string TagResultType = "eventbus.result.type";
    public const string TagProcessorCapacity = "eventbus.processor.capacity";
    public const string TagChannelType = "eventbus.channel.type";
    public const string TagQueueDepth = "eventbus.queue.depth";
    public const string TagProcessingStatus = "eventbus.processing.status";
    public const string TagErrorType = "eventbus.error.type";
    public const string TagRetryAttempt = "eventbus.retry.attempt";

    // Status values
    public const string StatusSuccess = "success";
    public const string StatusFailed = "failed";
    public const string StatusNoHandlers = "no_handlers";
    public const string StatusCancelled = "cancelled";
}
