using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Infrastructure.Channels;

public class ChannelEventsHostedService(
    ChannelEventBus eventBus,
    IEventProcessor eventProcessor,
    EventBusSettings eventBusSettings,
    ILogger<ChannelEventsHostedService> logger
    ) : BackgroundService
{
    private readonly SemaphoreSlim semaphore = new(eventBusSettings.EventProcessorCapacity, eventBusSettings.EventProcessorCapacity);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("ChannelEventsHostedService started.");
        var reader = eventBus.Reader;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var @event = await reader.ReadAsync(cancellationToken);

                try
                {
                    await semaphore.WaitAsync(cancellationToken);
                    await eventProcessor.ProcessEventAsync(@event, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[EventsHostedService] Failed to publish event {EventType}", @event?.GetType().Name ?? "null");
                }
                finally
                {
                    semaphore.Release();
                }

                logger.LogInformation("[ChannelEventsHostedService] Received event of type {EventType} with Id {EventId}", @event?.GetType().Name, @event?.Id);
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
        logger.LogInformation("ChannelEventsHostedService stopped.");
    }
}