using Microsoft.Extensions.DependencyInjection;

using Softoverse.EventBus.InMemory.Abstractions;

namespace EventBus.InMemory.Tests.Implementations;

internal class EventProcessor(
    IEventHandler<TestEvent> testEventHandler,
    IEventHandler<TestScheduledEvent> testScheduledHandler,
    EventTracker eventTracker) : IEventProcessor
{
    public async Task<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default)
    {
        var result = await new TestRequestHandler(eventTracker).HandleAsync(@event as TestRequest, cancellationToken);
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
        eventTracker.ScheduledEvents.Add((@event as TestEvent)?.Id ?? 0);
        int delayMs = (int)(scheduledTime - DateTimeOffset.UtcNow).TotalMilliseconds;
        await Task.Delay(delayMs, cancellationToken);
        await testScheduledHandler.HandleAsync(@event, cancellationToken);
    }
}
