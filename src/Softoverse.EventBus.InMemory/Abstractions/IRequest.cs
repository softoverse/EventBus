namespace Softoverse.EventBus.InMemory.Abstractions
{
    /// <summary>
    /// Marker interface indicating an event is a request and expects a response when invoked.
    /// Use with <see cref="IRequestHandler{TEvent, TResult}"/> and <see cref="IEventBus.InvokeAsync{TResult}(object, CancellationToken)"/>.
    /// </summary>
    public interface IRequest;
}
