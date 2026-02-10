namespace Softoverse.EventBus.InMemory.Abstractions;

/// <summary>
/// Non-generic contract for request handlers that return a value.
/// Used to resolve request handlers dynamically and to invoke them through a non-generic surface.
/// </summary>
public interface IRequestHandler
{
    /// <summary>
    /// Determines whether this handler can handle the provided request object.
    /// </summary>
    /// <param name="event">The request object to check.</param>
    /// <returns>True if the handler can process the request; otherwise false.</returns>
    bool CanHandle(object @event);

    /// <summary>
    /// Handles the provided request object and returns the result boxed as <see cref="object"/>.
    /// </summary>
    /// <param name="event">The request object to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The handler result boxed as <see cref="object"/>, or null if no result.</returns>
    Task<object?> HandleAsync(
        object @event,
        CancellationToken cancellationToken = default);
}

    
    
/// <summary>
/// Generic contract for request handlers that return a value of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TEvent">The type of the event.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface IRequestHandler<in TEvent, TResult> : IRequestHandler
    where TEvent : IRequest
{
    /// <summary>
    /// Handles a strongly-typed request and returns a typed result.
    /// </summary>
    /// <param name="event">The strongly-typed request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The typed result from the handler.</returns>
    Task<TResult> HandleAsync(
        TEvent @event,
        CancellationToken cancellationToken = default);

    // Bridge implementations
    bool IRequestHandler.CanHandle(object @event)
        => @event is TEvent;

    async Task<object?> IRequestHandler.HandleAsync(
        object @event,
        CancellationToken cancellationToken)
    {
        return @event is TEvent typed
            ? await HandleAsync(typed, cancellationToken)
            : default;
    }
}