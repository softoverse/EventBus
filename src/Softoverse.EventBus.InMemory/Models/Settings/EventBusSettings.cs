namespace Softoverse.EventBus.InMemory.Models.Settings;

public class EventBusSettings
{
    public const string SectionName = "EventBusSettings";

    public int MaxConcurrency { get; set; } = 1000;
    public int ChannelCapacity { get; set; } = -1;
    public int EventProcessorCapacity { get; set; } = 10;

    public int ExecuteAfterSeconds { get; set; } = 2;
    public int RetryAfterSeconds { get; set; } = 5;

    public int RetryCount { get; set; } = 10;
    public int EachRetryInterval { get; set; } = 3;
    public int[] RetryIntervals { get => [.. Enumerable.Range(1, RetryCount).Select(x => EachRetryInterval)]; }
}