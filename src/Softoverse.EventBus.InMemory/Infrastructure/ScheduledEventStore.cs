using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Softoverse.EventBus.InMemory.Abstractions;

namespace Softoverse.EventBus.InMemory.Infrastructure;

/// <summary>
/// Thread-safe in-memory store for scheduled events.
/// Events are stored with their scheduled execution time and a unique identifier.
/// </summary>
public class ScheduledEventStore(ILogger<ScheduledEventStore> logger)
{
    private readonly ConcurrentDictionary<Guid, ScheduledEventEntry> _scheduledEvents = new();

    /// <summary>
    /// Adds a scheduled event to the store.
    /// </summary>
    /// <param name="event">The event to schedule.</param>
    /// <param name="scheduledTime">The UTC time when the event should be processed.</param>
    /// <returns>The unique identifier for the scheduled event.</returns>
    public Guid AddScheduledEvent(IEvent @event, DateTimeOffset scheduledTime)
    {
        var id = Guid.NewGuid();
        var entry = new ScheduledEventEntry
        {
            Id = id,
            Event = @event,
            ScheduledTime = scheduledTime.ToUniversalTime(),
            AddedAt = DateTimeOffset.UtcNow
        };

        if (_scheduledEvents.TryAdd(id, entry))
        {
            logger.LogDebug(
                "[ScheduledEventStore] Added scheduled event {EventId} of type {EventType} for {ScheduledTime}",
                id,
                @event.GetType().Name,
                scheduledTime);
            return id;
        }

        logger.LogWarning(
            "[ScheduledEventStore] Failed to add scheduled event {EventId} (duplicate ID)",
            id);
        throw new InvalidOperationException($"Failed to add scheduled event with ID {id}");
    }

    /// <summary>
    /// Gets all events that are due for processing (scheduled time has passed).
    /// </summary>
    /// <returns>Collection of due scheduled events.</returns>
    public IReadOnlyCollection<ScheduledEventEntry> GetDueEvents()
    {
        var now = DateTimeOffset.UtcNow;
        return _scheduledEvents.Values
            .Where(e => e.ScheduledTime <= now)
            .OrderBy(e => e.ScheduledTime)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Removes a scheduled event from the store after it has been processed.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event to remove.</param>
    /// <returns>True if the event was removed, false if it was not found.</returns>
    public bool RemoveScheduledEvent(Guid eventId)
    {
        if (_scheduledEvents.TryRemove(eventId, out var entry))
        {
            logger.LogDebug(
                "[ScheduledEventStore] Removed scheduled event {EventId} of type {EventType}",
                eventId,
                entry.Event.GetType().Name);
            return true;
        }

        logger.LogWarning(
            "[ScheduledEventStore] Failed to remove scheduled event {EventId} (not found)",
            eventId);
        return false;
    }

    /// <summary>
    /// Gets the total count of scheduled events in the store.
    /// </summary>
    public int Count => _scheduledEvents.Count;

    /// <summary>
    /// Gets the next scheduled event time, if any.
    /// </summary>
    public DateTimeOffset? GetNextScheduledTime()
    {
        if (_scheduledEvents.IsEmpty)
            return null;

        return _scheduledEvents.Values
            .OrderBy(e => e.ScheduledTime)
            .FirstOrDefault()?.ScheduledTime;
    }
}

/// <summary>
/// Represents a scheduled event entry in the store.
/// </summary>
public class ScheduledEventEntry
{
    public required Guid Id { get; init; }
    public required IEvent Event { get; init; }
    public required DateTimeOffset ScheduledTime { get; init; }
    public required DateTimeOffset AddedAt { get; init; }
}

