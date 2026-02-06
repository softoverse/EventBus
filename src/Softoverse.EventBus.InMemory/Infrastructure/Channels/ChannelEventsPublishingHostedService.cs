using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Infrastructure.Channels;

public class ChannelEventsPublishingHostedService(
    IServiceScopeFactory scopeFactory,
    EventBusSettings eventBusSettings,
    EventChannelProvider channelProvider,
    ILogger<ChannelEventsPublishingHostedService> logger)
    : BackgroundService
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(eventBusSettings.EventProcessorCapacity, eventBusSettings.EventProcessorCapacity);

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("ChannelEventsPublishingHostedService started.");
        using var scope = scopeFactory.CreateScope();
        var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();
        var reader = channelProvider.PublishingChannel.Reader;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var @event = await reader.ReadAsync(cancellationToken);

                try
                {
                    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    await eventProcessor.ProcessEventAsync(@event, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[ChannelEventsPublishingHostedService] Failed to publish event {EventType}", @event?.GetType().Name ?? "null");
                }
                finally
                {
                    _semaphore.Release();
                }

                logger.LogInformation("[ChannelEventsPublishingHostedService] Received event of type {EventType}", @event?.GetType().Name);
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
        logger.LogInformation("ChannelEventsPublishingHostedService stopped.");
    }
}
