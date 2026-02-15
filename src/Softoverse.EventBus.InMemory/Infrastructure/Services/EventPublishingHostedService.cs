using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Infrastructure.Services;

internal class EventPublishingHostedService(
    IServiceScopeFactory scopeFactory,
    EventBusSettings eventBusSettings,
    EventChannelProvider channelProvider,
    ILogger<EventPublishingHostedService> logger)
    : BackgroundService
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(eventBusSettings.EventProcessorCapacity, eventBusSettings.EventProcessorCapacity);

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.PublishingServiceStarted();
        using var scope = scopeFactory.CreateScope();
        var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();
        var reader = channelProvider.PublishingChannel.Reader;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var @event = await reader.ReadAsync(cancellationToken);

                if (@event == null)
                {
                    logger.ReceivedNullEventFromPublishingChannel();
                    continue;
                }

                var eventType = @event.GetType().Name;

                logger.ReceivedEventFromPublishingChannel(eventType);

                // Create activity only when processing actual events
                using var processActivity = EventBusDiagnostics.ActivitySource.StartActivity(
                                                                                             EventBusDiagnostics.ActivityChannelProcess,
                                                                                             ActivityKind.Consumer);

                processActivity?.SetTag(EventBusDiagnostics.TagEventType, eventType);
                processActivity?.SetTag(EventBusDiagnostics.TagChannelType, "Publishing");

                // Add event ID if available through reflection
                var eventIdProperty = @event.GetType().GetProperty("Id");
                if (eventIdProperty?.GetValue(@event) is Guid eventId)
                {
                    processActivity?.SetTag(EventBusDiagnostics.TagEventId, eventId.ToString());
                }

                try
                {
                    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    await eventProcessor.ProcessEventAsync(@event, cancellationToken).ConfigureAwait(false);

                    processActivity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusSuccess);
                    processActivity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception ex)
                {
                    processActivity?.SetTag(EventBusDiagnostics.TagProcessingStatus, EventBusDiagnostics.StatusFailed);
                    processActivity?.SetTag(EventBusDiagnostics.TagErrorType, ex.GetType().Name);
                    processActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    logger.PublishingChannelProcessFailed(ex, eventType);
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
                logger.PublishingChannelReadError(ex);
            }
        }
        logger.PublishingServiceStopped();
    }
}
