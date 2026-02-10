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
            return;
        }

        logger.LogInformation(
                              "[ScheduledEventProcessingHostedService] Found {Count} due event(s) to process",
                              dueEvents.Count);

        await using var scope = scopeFactory.CreateAsyncScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        foreach (var scheduledEvent in dueEvents)
        {
            // Mark as in progress immediately to prevent duplicate execution
            if (!scheduledEventStore.MarkAsInProgress(scheduledEvent.Id))
            {
                logger.LogWarning(
                    "[ScheduledEventProcessingHostedService] Failed to mark event {EventId} as InProgress, skipping",
                    scheduledEvent.Id);
                continue;
            }
            
            try
            {
                logger.LogInformation(
                      "[ScheduledEventProcessingHostedService] Processing scheduled event {EventId} of type {EventType}",
                      scheduledEvent.Id,
                      scheduledEvent.Event.GetType().Name);

                await eventBus.PublishAsync(scheduledEvent.Event);

                // Mark as done and remove from store after successful processing
                scheduledEventStore.MarkAsDone(scheduledEvent.Id);
                scheduledEventStore.RemoveScheduledEvent(scheduledEvent.Id);

                logger.LogInformation(
                                      "[ScheduledEventProcessingHostedService] Successfully processed and removed event {EventId}",
                                      scheduledEvent.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(
                                ex,
                                "[ScheduledEventProcessingHostedService] Failed to process scheduled event {EventId} of type {EventType}",
                                scheduledEvent.Id,
                                scheduledEvent.Event.GetType().Name);

                // Mark as failed with the error message
                scheduledEventStore.MarkAsFailed(scheduledEvent.Id, ex.Message);
                
                // Remove the event to prevent infinite retries
                scheduledEventStore.RemoveScheduledEvent(scheduledEvent.Id);
            }
        }
    }

    private async Task ProcessScheduledEventAsync(ScheduledEventEntry scheduledEvent, CancellationToken cancellationToken)
    {
        try
        {
            // Mark as in progress immediately to prevent duplicate execution
            if (!scheduledEventStore.MarkAsInProgress(scheduledEvent.Id))
            {
                logger.LogWarning(
                    "[ScheduledEventProcessingHostedService] Failed to mark event {EventId} as InProgress, aborting",
                    scheduledEvent.Id);
                return;
            }
            
            await using var scope = scopeFactory.CreateAsyncScope();
            var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();

            logger.LogInformation(
                                  "[ScheduledEventProcessingHostedService] Processing scheduled event {EventId} of type {EventType}",
                                  scheduledEvent.Id,
                                  scheduledEvent.Event.GetType().Name);

            var delay = scheduledEvent.ScheduledTime - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero && delay < TimeSpan.FromMinutes(1))
            {
                // If the event is slightly in the future (within 1 minute), wait for it
                logger.LogDebug(
                                "[ScheduledEventProcessingHostedService] Waiting {Delay}ms for event {EventId}",
                                delay.TotalMilliseconds,
                                scheduledEvent.Id);
                await Task.Delay(delay, cancellationToken);
            }

            // Process the event handlers
            await eventProcessor.ProcessEventHandlersAsync(scheduledEvent.Event, cancellationToken);

            // Mark as done and remove from store after successful processing
            scheduledEventStore.MarkAsDone(scheduledEvent.Id);
            scheduledEventStore.RemoveScheduledEvent(scheduledEvent.Id);

            logger.LogInformation(
                                  "[ScheduledEventProcessingHostedService] Successfully processed and removed event {EventId}",
                                  scheduledEvent.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(
                            ex,
                            "[ScheduledEventProcessingHostedService] Failed to process scheduled event {EventId} of type {EventType}",
                            scheduledEvent.Id,
                            scheduledEvent.Event.GetType().Name);

            // Mark as failed with the error message
            scheduledEventStore.MarkAsFailed(scheduledEvent.Id, ex.Message);
            
            // Remove the event to prevent infinite retries
            scheduledEventStore.RemoveScheduledEvent(scheduledEvent.Id);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override void Dispose()
    {
        _semaphore.Dispose();
        base.Dispose();
    }
}
