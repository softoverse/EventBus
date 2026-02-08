using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Infrastructure.Services;

public class ChannelEventsSchedulingHostedService(
    IServiceScopeFactory scopeFactory,
    EventBusSettings eventBusSettings,
    EventChannelProvider channelProvider,
    ILogger<ChannelEventsSchedulingHostedService> logger)
    : BackgroundService
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(eventBusSettings.EventProcessorCapacity, eventBusSettings.EventProcessorCapacity);

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("ChannelEventsSchedulingHostedService started.");
        using var scope = scopeFactory.CreateScope();
        var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();
        var reader = channelProvider.SchedulingChannel.Reader;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var scheduledEvent = await reader.ReadAsync(cancellationToken);

                try
                {
                    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    await eventProcessor.ProcessScheduledEventAsync(scheduledEvent.Event, scheduledEvent.ScheduledTime, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[ChannelEventsSchedulingHostedService] Failed to publish event {EventType}", scheduledEvent.Event?.GetType().Name ?? "null");
                }
                finally
                {
                    _semaphore.Release();
                }

                logger.LogInformation("[ChannelEventsSchedulingHostedService] Received event of type {EventType}", scheduledEvent.Event?.GetType().Name);
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
        logger.LogInformation("ChannelEventsSchedulingHostedService stopped.");
    }
}
