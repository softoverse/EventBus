using System.Threading.Channels;

using Microsoft.Extensions.Logging;

using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Infrastructure.Channels;

public class ChannelEventBus : IEventBus
{
    private readonly Channel<IEvent> _eventChannel;
    private readonly ILogger<ChannelEventBus> _logger;

    public ChannelEventBus(ILogger<ChannelEventBus> logger, EventBusSettings settings)
    {
        _logger = logger;
        if (settings.ChannelCapacity <= 0)
        {
            // UNBOUNDED CHANNEL - No event loss, unlimited capacity
            var options = new UnboundedChannelOptions()
            {
                // SingleReader: true = Only one consumer can read from channel (better performance)
                // SingleReader: false = Multiple consumers can read simultaneously (MAXIMUM CONCURRENCY for processing)
                SingleReader = false, // Changed to false for maximum concurrency

                // SingleWriter: false = Multiple threads can write simultaneously (better concurrency)
                // SingleWriter: true = Only one thread can write at a time (slightly better performance for single writer scenarios)
                SingleWriter = false,

                // AllowSynchronousContinuations: true = Continuations run on current thread (better performance, potential blocking)
                // AllowSynchronousContinuations: false = Continuations run on thread pool (safer but slower)
                AllowSynchronousContinuations = true
            };
            _eventChannel = Channel.CreateUnbounded<IEvent>(options);
        }
        else
        {
            // BOUNDED CHANNEL - Fixed capacity, potential for event loss depending on FullMode
            var options = new BoundedChannelOptions(settings.ChannelCapacity)
            {
                // FullMode options when channel reaches capacity:
                // - Wait: Publisher blocks until space available (GUARANTEES NO EVENT LOSS but can cause delays)
                // - DropWrite: New events are dropped when full (POTENTIAL EVENT LOSS)
                // - DropOldest: Oldest events are dropped to make room (POTENTIAL EVENT LOSS)
                // - DropNewest: Newest events are dropped (POTENTIAL EVENT LOSS)
                FullMode = BoundedChannelFullMode.Wait, // Wait to guarantee no event loss

                // SingleReader: true = Only one consumer can read from channel (better performance)
                // SingleReader: false = Multiple consumers can read simultaneously (MAXIMUM CONCURRENCY for processing)
                SingleReader = false, // Changed to false for maximum concurrency

                // SingleWriter: false = Multiple threads can write simultaneously (better concurrency)
                // SingleWriter: true = Only one thread can write at a time (slightly better performance for single writer scenarios)
                SingleWriter = false,

                // AllowSynchronousContinuations: true = Continuations run on current thread (better performance, potential blocking)
                // AllowSynchronousContinuations: false = Continuations run on thread pool (safer but slower)
                AllowSynchronousContinuations = true
            };
            _eventChannel = Channel.CreateBounded<IEvent>(options);
        }
    }

    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        if (@event is null)
        {
            _logger.LogWarning("[ChannelEventBus] Ignored null event of type {EventType}", typeof(TEvent).Name);
            return ValueTask.CompletedTask;
        }
        _logger.LogInformation("[ChannelEventBus] Publishing {EventType}", typeof(TEvent).Name);
        return _eventChannel.Writer.WriteAsync(@event, cancellationToken);
    }

    public async ValueTask BulkPublishAsync<TEvent>(List<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        foreach (var @event in events)
        {
            await PublishAsync(@event);
        }
    }

    public ChannelReader<IEvent> Reader => _eventChannel.Reader;
}