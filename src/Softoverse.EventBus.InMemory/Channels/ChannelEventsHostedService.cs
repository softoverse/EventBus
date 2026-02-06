using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Channels;

public class ChannelEventsHostedService(
    IServiceScopeFactory scopeFactory,
    EventBusSettings eventBusSettings,
    EventChannelProvider channelProvider,
    ILogger<ChannelEventsHostedService> logger)
    : BackgroundService
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(eventBusSettings.EventProcessorCapacity, eventBusSettings.EventProcessorCapacity);

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("ChannelEventsHostedService started.");

        // Run both channel readers in parallel
        var publishingTask = ProcessPublishingChannelAsync(cancellationToken);
        var schedulingTask = ProcessSchedulingChannelAsync(cancellationToken);

        try
        {
            await Task.WhenAll(publishingTask, schedulingTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "One or more channel processing tasks failed.");
            throw;
        }
        finally
        {
            logger.LogInformation("ChannelEventsHostedService stopped.");
        }
    }

    private async Task ProcessPublishingChannelAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();
        var publishingChannelReader = channelProvider.PublishingChannel.Reader;

        logger.LogInformation("Publishing channel processor started.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var @event = await publishingChannelReader.ReadAsync(cancellationToken);

                try
                {
                    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    await eventProcessor.ProcessEventAsync(@event, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[EventsHostedService] Failed to publish event {EventType}", @event?.GetType().Name ?? "null");
                }
                finally
                {
                    _semaphore.Release();
                }

                logger.LogInformation("[ChannelEventsHostedService] Received event of type {EventType}", @event?.GetType().Name);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing event from publishing channel.");
            }
        }

        logger.LogInformation("Publishing channel processor stopped.");
    }

    private async Task ProcessSchedulingChannelAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();
        var schedulingChannelReader = channelProvider.SchedulingChannel.Reader;

        logger.LogInformation("Scheduling channel processor started.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var scheduledEvent = await schedulingChannelReader.ReadAsync(cancellationToken);

                try
                {
                    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    await eventProcessor.ProcessScheduledEventAsync(scheduledEvent.Event, scheduledEvent.ScheduledTime, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[EventsHostedService] Failed to schedule event {EventType}", scheduledEvent.Event?.GetType().Name ?? "null");
                }
                finally
                {
                    _semaphore.Release();
                }

                logger.LogInformation("[ChannelEventsHostedService] Received scheduled event of type {EventType}", scheduledEvent.Event?.GetType().Name);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing event from scheduling channel.");
            }
        }

        logger.LogInformation("Scheduling channel processor stopped.");
    }
}
