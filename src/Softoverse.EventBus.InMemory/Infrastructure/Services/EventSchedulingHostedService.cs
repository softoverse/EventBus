using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Infrastructure.Services;

internal class EventSchedulingHostedService(
    IServiceScopeFactory scopeFactory,
    EventBusSettings eventBusSettings,
    EventChannelProvider channelProvider,
    ILogger<EventSchedulingHostedService> logger)
    : BackgroundService
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(eventBusSettings.EventProcessorCapacity, eventBusSettings.EventProcessorCapacity);

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("EventSchedulingHostedService started.");
        using var scope = scopeFactory.CreateScope();
        var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();
        var reader = channelProvider.SchedulingChannel.Reader;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var scheduledEvent = await reader.ReadAsync(cancellationToken);

                if (scheduledEvent.Event == null)
                {
                    logger.LogWarning("[EventSchedulingHostedService] Received null event from channel");
                    continue;
                }

                var eventType = scheduledEvent.Event.GetType().Name;

                logger.LogInformation("[EventSchedulingHostedService] Received event of type {EventType}", eventType);

                // Create activity only when processing actual events
                using var processActivity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                                             EventBusDiagnostics.ActivityChannelProcess,
                                                                                             ActivityKind.Consumer);

                processActivity?.SetTag(EventBusDiagnostics.TagEventType, eventType);
                processActivity?.SetTag(EventBusDiagnostics.TagChannelType, "Scheduling");
                processActivity?.SetTag(EventBusDiagnostics.TagScheduledTime, scheduledEvent.ScheduledTime.ToString("O"));

                // Add event ID if available through reflection
                var eventIdProperty = scheduledEvent.Event.GetType().GetProperty("Id");
                if (eventIdProperty?.GetValue(scheduledEvent.Event) is Guid eventId)
                {
                    processActivity?.SetTag(EventBusDiagnostics.TagEventId, eventId.ToString());
                }

                try
                {
                    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    await eventProcessor.ProcessScheduledEventAsync(scheduledEvent.Event, scheduledEvent.ScheduledTime, cancellationToken).ConfigureAwait(false);

                    processActivity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
                    processActivity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception ex)
                {
                    processActivity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
                    processActivity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
                    processActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    logger.LogError(ex, "[EventSchedulingHostedService] Failed to publish event {EventType}", eventType);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing event from channel.");
            }
        }
        logger.LogInformation("EventSchedulingHostedService stopped.");
    }
}
