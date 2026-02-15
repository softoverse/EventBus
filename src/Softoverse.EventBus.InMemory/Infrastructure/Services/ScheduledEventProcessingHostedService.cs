using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Infrastructure.Services;

/// <summary>
/// Background service that periodically checks for scheduled events that are due
/// and processes them through the event processor.
/// </summary>
internal class ScheduledEventProcessingHostedService(
    IServiceScopeFactory scopeFactory,
    ScheduledEventStore scheduledEventStore,
    EventBusSettings eventBusSettings,
    ILogger<ScheduledEventProcessingHostedService> logger)
    : BackgroundService
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(eventBusSettings.EventProcessorCapacity, eventBusSettings.EventProcessorCapacity);

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[ScheduledEventProcessingHostedService] Started");

        // Use configurable check interval, defaulting to 1 second
        var checkInterval = TimeSpan.FromSeconds(eventBusSettings.ExecuteAfterSeconds > 0
                                                     ? eventBusSettings.ExecuteAfterSeconds
                                                     : 1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    await ProcessDueEventsAsync(stoppingToken);
                }, stoppingToken);

                // Wait before checking again
                await Task.Delay(checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ScheduledEventProcessingHostedService] Error in main loop");
                // Continue processing after logging the error
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("[ScheduledEventProcessingHostedService] Stopped");
    }

    private async Task ProcessDueEventsAsync(CancellationToken cancellationToken)
    {
        var dueEvents = scheduledEventStore.GetDueEvents();

        if (dueEvents.Count == 0)
        {
            // Don't create activity for empty checks to avoid trace noise
            return;
        }

        // Only create activity when there are events to process
        using var activity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                              EventBusDiagnostics.ActivityScheduledEventCheck,
                                                                              ActivityKind.Internal);

        activity?.SetTag(EventBusDiagnostics.TagEventCount, dueEvents.Count);


        logger.LogInformation(
                              "[ScheduledEventProcessingHostedService] Found {Count} due event(s) to process",
                              dueEvents.Count);

        await using var scope = scopeFactory.CreateAsyncScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var processedCount = 0;
        var failedCount = 0;

        foreach (var scheduledEvent in dueEvents)
        {
            using var eventActivity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                                       EventBusDiagnostics.ActivityProcessEvent,
                                                                                       ActivityKind.Consumer);

            var eventType = scheduledEvent.Event.GetType().Name;
            eventActivity?.SetTag(EventBusDiagnostics.TagEventType, eventType);
            eventActivity?.SetTag(EventBusDiagnostics.TagEventId, scheduledEvent.Id.ToString());
            eventActivity?.SetTag(EventBusDiagnostics.TagScheduledTime, scheduledEvent.ScheduledTime.ToString("O"));

            // Mark as in progress immediately to prevent duplicate execution
            if (!scheduledEventStore.MarkAsInProgress(scheduledEvent.Id))
            {
                logger.LogWarning(
                                  "[ScheduledEventProcessingHostedService] Failed to mark event {EventId} as InProgress, skipping",
                                  scheduledEvent.Id);
                eventActivity?.SetTag(EventBusDiagnostics.TagProcessingStatus, "skipped_in_progress");
                continue;
            }

            try
            {
                logger.LogInformation(
                                      "[ScheduledEventProcessingHostedService] Processing scheduled event {EventId} of type {EventType}",
                                      scheduledEvent.Id,
                                      eventType);

                await eventBus.PublishAsync(scheduledEvent.Event, cancellationToken);

                // Mark as done and remove from store after successful processing
                scheduledEventStore.MarkAsDone(scheduledEvent.Id);
                scheduledEventStore.RemoveScheduledEvent(scheduledEvent.Id);

                processedCount++;
                eventActivity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
                eventActivity?.SetStatus(ActivityStatusCode.Ok);

                logger.LogInformation(
                                      "[ScheduledEventProcessingHostedService] Successfully processed and removed event {EventId}",
                                      scheduledEvent.Id);
            }
            catch (Exception ex)
            {
                failedCount++;
                eventActivity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
                eventActivity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
                eventActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                logger.LogError(
                                ex,
                                "[ScheduledEventProcessingHostedService] Failed to process scheduled event {EventId} of type {EventType}",
                                scheduledEvent.Id,
                                eventType);

                // Mark as failed with the error message
                scheduledEventStore.MarkAsFailed(scheduledEvent.Id, ex.Message);

                // Remove the event to prevent infinite retries
                scheduledEventStore.RemoveScheduledEvent(scheduledEvent.Id);
            }
        }

        activity?.SetTag("eventbus.processed.count", processedCount);
        activity?.SetTag("eventbus.failed.count", failedCount);
        activity?.SetTag(EventBusDiagnostics.TagProcessingStatus,
                         failedCount == 0 ? EventBusDiagnostics.StatusSuccess : "partial_failure");

        if (failedCount == 0)
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        else
        {
            activity?.SetStatus(ActivityStatusCode.Error, $"{failedCount} out of {dueEvents.Count} events failed");
        }
    }

    public override void Dispose()
    {
        _semaphore.Dispose();
        base.Dispose();
    }
}
