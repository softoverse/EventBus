using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Softoverse.EventBus.InMemory.Abstractions;

namespace Softoverse.EventBus.InMemory.Infrastructure;

/// <summary>
/// Represents the execution status of a scheduled event.
/// </summary>
public enum ScheduledEventStatus
{
    /// <summary>
    /// Event is pending and waiting to be processed.
    /// </summary>
    Pending,
    
    /// <summary>
    /// Event is currently being processed.
    /// </summary>
    InProgress,
    
    /// <summary>
    /// Event has been successfully processed.
    /// </summary>
    Done,
    
    /// <summary>
    /// Event processing failed.
    /// </summary>
    Failed,
    
    /// <summary>
    /// Event was skipped and will not be processed.
    /// </summary>
    Skipped
}

/// <summary>
/// Thread-safe in-memory store for scheduled events.
/// Events are stored with their scheduled execution time and a unique identifier.
/// </summary>
public class ScheduledEventStore(ILogger<ScheduledEventStore> logger)
{
    private readonly ConcurrentDictionary<Guid, ScheduledEventEntry> _scheduledEvents = new();
    
    /// <summary>
    /// Maximum time an event can stay in InProgress state before being considered stale (in minutes).
    /// Default is 5 minutes. After this timeout, the event can be retried.
    /// </summary>
    public int InProgressTimeoutMinutes { get; set; } = 5;

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
            AddedAt = DateTimeOffset.UtcNow,
            Status = ScheduledEventStatus.Pending,
            StatusUpdatedAt = DateTimeOffset.UtcNow
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
    /// Only returns events in Pending status or InProgress status that have exceeded the timeout.
    /// </summary>
    /// <returns>Collection of due scheduled events.</returns>
    public IReadOnlyCollection<ScheduledEventEntry> GetDueEvents()
    {
        var now = DateTimeOffset.UtcNow;
        var inProgressTimeout = TimeSpan.FromMinutes(InProgressTimeoutMinutes);
        
        return _scheduledEvents.Values
            .Where(e => e.ScheduledTime <= now && 
                       (e.Status == ScheduledEventStatus.Pending ||
                        (e.Status == ScheduledEventStatus.InProgress && 
                         now - e.StatusUpdatedAt > inProgressTimeout)))
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
    /// Updates the status of a scheduled event.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="status">The new status.</param>
    /// <param name="remarks">Optional remarks about the status change (e.g., error message for Failed status).</param>
    /// <returns>True if the status was updated, false if the event was not found.</returns>
    public bool UpdateEventStatus(Guid eventId, ScheduledEventStatus status, string? remarks = null)
    {
        if (!_scheduledEvents.TryGetValue(eventId, out var entry))
        {
            logger.LogWarning(
                "[ScheduledEventStore] Failed to update status for event {EventId} (not found)",
                eventId);
            return false;
        }

        var oldStatus = entry.Status;
        entry.Status = status;
        entry.StatusUpdatedAt = DateTimeOffset.UtcNow;
        entry.Remarks = remarks;

        logger.LogDebug(
            "[ScheduledEventStore] Updated event {EventId} status from {OldStatus} to {NewStatus}",
            eventId,
            oldStatus,
            status);

        return true;
    }
    
    /// <summary>
    /// Marks a scheduled event as in progress.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <returns>True if the status was updated, false if the event was not found.</returns>
    public bool MarkAsInProgress(Guid eventId)
    {
        return UpdateEventStatus(eventId, ScheduledEventStatus.InProgress, "Processing started");
    }
    
    /// <summary>
    /// Marks a scheduled event as completed successfully.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="remarks">Optional remarks about the completion.</param>
    /// <returns>True if the status was updated, false if the event was not found.</returns>
    public bool MarkAsDone(Guid eventId, string? remarks = null)
    {
        return UpdateEventStatus(eventId, ScheduledEventStatus.Done, remarks ?? "Processing completed successfully");
    }
    
    /// <summary>
    /// Marks a scheduled event as failed.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="failureReason">The reason for the failure.</param>
    /// <returns>True if the status was updated, false if the event was not found.</returns>
    public bool MarkAsFailed(Guid eventId, string failureReason)
    {
        return UpdateEventStatus(eventId, ScheduledEventStatus.Failed, failureReason);
    }
    
    /// <summary>
    /// Marks a scheduled event as skipped.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="remarks">Optional remarks about why the event was skipped.</param>
    /// <returns>True if the status was updated, false if the event was not found.</returns>
    public bool MarkAsSkipped(Guid eventId, string? remarks = null)
    {
        return UpdateEventStatus(eventId, ScheduledEventStatus.Skipped, remarks ?? "Event skipped");
    }
    
    /// <summary>
    /// Gets all events with a specific status.
    /// </summary>
    /// <param name="status">The status to filter by.</param>
    /// <returns>Collection of events with the specified status.</returns>
    public IReadOnlyCollection<ScheduledEventEntry> GetEventsByStatus(ScheduledEventStatus status)
    {
        return _scheduledEvents.Values
            .Where(e => e.Status == status)
            .OrderBy(e => e.ScheduledTime)
            .ToList()
            .AsReadOnly();
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
    
    /// <summary>
    /// The current status of the scheduled event.
    /// </summary>
    public ScheduledEventStatus Status { get; set; }
    
    /// <summary>
    /// The timestamp when the status was last updated.
    /// </summary>
    public DateTimeOffset StatusUpdatedAt { get; set; }
    
    /// <summary>
    /// Optional remarks about the event's status.
    /// For Failed status, this contains the failure reason.
    /// For other statuses, it can contain any relevant information.
    /// </summary>
    public string? Remarks { get; set; }
}

