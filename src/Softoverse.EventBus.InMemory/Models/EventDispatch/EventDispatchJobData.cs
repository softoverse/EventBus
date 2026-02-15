namespace Softoverse.EventBus.InMemory.Models.EventDispatch;

public class EventDispatchJobData
{
    public required string EventJson { get; set; }
    public required string EventTypeName { get; set; }
    public DateTimeOffset? ExecuteAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
