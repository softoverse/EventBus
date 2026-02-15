namespace Softoverse.EventBus.InMemory.Models.EventDispatch;

public class EventHandlerDispatchJobData : EventDispatchJobData
{
    public required string HandlerTypeName { get; set; }
}
