using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;

namespace Softoverse.EventBus.InMemory.Infrastructure.Processors;

public class DefaultEventProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<DefaultEventProcessor> logger,
    ScheduledEventStore scheduledEventStore) : IEventProcessor
{

    public async Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null!)
        {
            logger.LogWarning("[DefaultEventProcessor] Ignored null event");
            return;
        }

        logger.LogInformation("[DefaultEventProcessor] Processing event {EventType}", @event.GetType().Name);

        try
        {
            await ProcessEventHandlersAsync(@event, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DefaultEventProcessor] Failed to process event {EventType}", @event.GetType().Name);
            throw;
        }
    }

    public async Task ProcessScheduledEventAsync(IEvent @event, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default)
    {
        if (@event == null!)
        {
            logger.LogWarning("[DefaultEventProcessor] Ignored null scheduled event");
            return;
        }

        var scheduledTimeUtc = scheduledTime.ToUniversalTime();

        logger.LogInformation(
                              "[DefaultEventProcessor] Scheduling event {EventType} for {ScheduledTime} (UTC)",
                              @event.GetType().Name,
                              scheduledTimeUtc);

        // Store the event in the in-memory store for background processing
        scheduledEventStore.AddScheduledEvent(@event, scheduledTimeUtc);

        await Task.CompletedTask;
    }

    public Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null!)
        {
            logger.LogWarning("[DefaultEventProcessor] Ignored null event in ProcessEventHandlersAsync");
            return Task.CompletedTask;
        }
    
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handlers = scope.ServiceProvider.GetServices<IEventHandler>();
    
            var applicableHandlers = handlers.Where(h => h.CanHandle(@event)).ToList();
    
            if (applicableHandlers.Count == 0)
            {
                logger.LogWarning("[DefaultEventProcessor] No handlers found for event type {EventType}", @event.GetType().Name);
                return;
            }
    
            logger.LogInformation(
                                  "[DefaultEventProcessor] Found {HandlerCount} handler(s) for event type {EventType}",
                                  applicableHandlers.Count,
                                  @event.GetType().Name);
    
            var handlerTasks = applicableHandlers.Select(handler =>
                                                             SafeHandleAsync(handler, @event, cancellationToken));
    
            await Task.WhenAll(handlerTasks).ConfigureAwait(false); 
        }, cancellationToken);
        return Task.CompletedTask;
    }

    public async Task<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default)
    {
        if (@event == null!)
        {
            logger.LogWarning("[DefaultEventProcessor] Ignored null event in InvokeAsync");
            return default!;
        }

        logger.LogInformation("[DefaultEventProcessor] Invoking event {EventType}", @event.GetType().Name);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var handlerType = typeof(IRequestHandler<,>)
                .MakeGenericType(@event.GetType(), typeof(TResult));

            object? handler = scope.ServiceProvider.GetService(handlerType);

            if (handler is IRequestHandler baseHandler &&
                baseHandler.CanHandle(@event))
            {
                object? result = await baseHandler
                    .HandleAsync(@event, cancellationToken);

                return (TResult?)result!;
            }

            logger.LogWarning("[DefaultEventProcessor] No handler found for event type {EventType}", @event.GetType().Name);
            return default!;

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DefaultEventProcessor] Failed to invoke event {EventType}", @event.GetType().Name);
            throw;
        }
    }

    private async Task SafeHandleAsync(IEventHandler handler, IEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                                  "[DefaultEventProcessor] Handler {HandlerType} successfully processed event {EventType}",
                                  handler.GetType().Name,
                                  @event.GetType().Name);
        }
        catch (Exception ex)
        {
            logger.LogError(
                            ex,
                            "[DefaultEventProcessor] Handler {HandlerType} failed to process event {EventType}",
                            handler.GetType().Name,
                            @event.GetType().Name);
            // Don't re-throw to allow other handlers to continue processing
        }
    }
}
