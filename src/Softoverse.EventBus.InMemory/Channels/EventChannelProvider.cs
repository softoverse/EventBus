using System.Threading.Channels;
using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory.Channels;

public sealed class EventChannelProvider
{
    public Channel<IEvent> PublishingChannel { get; }
    public Channel<(IEvent Event, DateTimeOffset ScheduledTime)> SchedulingChannel { get; }

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
            PublishingChannel = Channel.CreateUnbounded<IEvent>(options);
            SchedulingChannel = Channel.CreateUnbounded<(IEvent Event, DateTimeOffset ScheduledTime)>(options);
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
            PublishingChannel = Channel.CreateBounded<IEvent>(options);
            SchedulingChannel = Channel.CreateBounded<(IEvent Event, DateTimeOffset ScheduledTime)>(options);
        }
    }
}
