using System.Threading.Channels;
using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Infrastructure.Channels;

public sealed class EventChannelProvider
{
    public Channel<IEvent> Channel { get; }

    public EventChannelProvider(EventBusSettings settings)
    {
        if (settings.ChannelCapacity <= 0)
        {
            // Unbounded channel: no event loss, unlimited capacity.
            var options = new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = true
            };
            Channel = System.Threading.Channels.Channel.CreateUnbounded<IEvent>(options);
        }
        else
        {
            // Bounded channel: fixed capacity, wait to guarantee no event loss.
            var options = new BoundedChannelOptions(settings.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = true
            };
            Channel = System.Threading.Channels.Channel.CreateBounded<IEvent>(options);
        }
    }
}
