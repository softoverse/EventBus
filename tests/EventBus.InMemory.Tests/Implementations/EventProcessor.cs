using Microsoft.Extensions.DependencyInjection;

using Softoverse.EventBus.InMemory.Abstractions;

namespace EventBus.InMemory.Tests.Implementations;

internal class EventProcessor(IEventHandler<TestEvent> testEventHandler) : IEventProcessor
{
    public async Task<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default)
    {
        var result = await new TestInvokeHandler().HandleAsync(@event as TestEvent, cancellationToken);
        return result as dynamic;
    }

    public async Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        await ProcessEventHandlersAsync(@event, cancellationToken);
    }

    public async Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        await testEventHandler.HandleAsync(@event as TestEvent, cancellationToken);
    }

    public async Task ProcessScheduledEventAsync(IEvent @event, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default)
    {
        EventTracker.ScheduledEvents.Add((@event as TestEvent)?.Id ?? 0);
        await Task.Delay(scheduledTime - DateTimeOffset.UtcNow, cancellationToken);
        await testEventHandler.HandleAsync(@event, cancellationToken);
    }
}
