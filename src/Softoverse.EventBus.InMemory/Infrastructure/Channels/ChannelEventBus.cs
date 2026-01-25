using Microsoft.Extensions.Logging;
using Softoverse.EventBus.InMemory.Abstractions;

namespace Softoverse.EventBus.InMemory.Infrastructure.Channels;

public class ChannelEventBus : IEventBus
{
    private readonly ILogger<ChannelEventBus> _logger;
    private readonly IEventProcessor _eventProcessor;
    private readonly EventChannelProvider _channelProvider;

    public ChannelEventBus(ILogger<ChannelEventBus> logger, EventChannelProvider channelProvider, IEventProcessor eventProcessor)
    {
        _logger = logger;
        _eventProcessor = eventProcessor;
        _channelProvider = channelProvider;
    }

    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        if (@event == null!)
        {
            _logger.LogWarning("[ChannelEventBus] Ignored null event of type {EventType}", typeof(TEvent).Name);
            return;
        }
        _logger.LogInformation("[ChannelEventBus] Publishing {EventType}", typeof(TEvent).Name);
        await _channelProvider.Channel.Writer.WriteAsync(@event!, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask BulkPublishAsync<TEvent>(List<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        foreach (var @event in events)
        {
            await PublishAsync(@event, cancellationToken).ConfigureAwait(false);
        }
        
        // var publishTasks = events.Select(@event => PublishAsync(@event, cancellationToken).AsTask());
        // await Task.WhenAll(publishTasks);
    }

    public async ValueTask<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default)
    {
        if (@event == null!)
        {
            _logger.LogWarning("[ChannelEventBus] Ignored null event");
        }
        try
        {
            return await _eventProcessor.InvokeAsync<TResult>(@event!, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChannelEventBus] Failed to invoke event {EventType}", @event?.GetType().Name ?? "null");
        }
        return default!;
    }
}
