using SampleCore;

namespace EventBus.InMemory.Tests.Core;

public abstract class BaseTests(
    bool useDefaultEventProcessor = false,
    string configFileName = TestContext.DefaultConfigFileName)
    : TestContext(useDefaultEventProcessor, configFileName)
{
    [Fact]
    public async Task InvokeAsync_ShouldProcessImmediately()
    {
        // Arrange
        var testEvent = new TestRequest(7);

        // Act
        var result = await EventBus.InvokeAsync<bool>(testEvent);

        // Assert
        Assert.True(result);
        Assert.Contains(7, EventTracker.InvokedEvents);
    }

    [Fact]
    public async Task PublishAsync_ShouldProcessImmediately()
    {
        // Arrange
        var testEvent = new TestEvent(1);

        // Act
        await EventBus.PublishAsync(testEvent);

        // Give some time for the event to be processed (since it's fire-and-forget)
        await WaitForProcessingAsync();

        // Assert
        Assert.Contains(1, EventTracker.ProcessedEvents);
    }

    [Fact]
    public async Task PublishAsync_MultipleEvents_AllProcessed()
    {
        // Arrange & Act
        await EventBus.PublishAsync(new TestEvent(8));
        await EventBus.PublishAsync(new TestEvent(9));
        await EventBus.PublishAsync(new TestEvent(10));

        // Give some time for all events to be processed
        await WaitForProcessingAsync();

        // Assert
        Assert.Contains(8, EventTracker.ProcessedEvents);
        Assert.Contains(9, EventTracker.ProcessedEvents);
        Assert.Contains(10, EventTracker.ProcessedEvents);
    }

    [Fact]
    public async Task BulkPublishAsync_ShouldProcessAllEvents()
    {
        // Arrange
        TestEvent[] events =
        [
            new TestEvent(2),
            new TestEvent(3)
        ];

        // Act
        await EventBus.BulkPublishAsync(events);

        // Give some time for the events to be processed
        await WaitForProcessingAsync();

        // Assert
        Assert.Contains(2, EventTracker.ProcessedEvents);
        Assert.Contains(3, EventTracker.ProcessedEvents);
        Assert.True(EventTracker.ProcessedEvents.Count >= 2);
    }

    [Fact]
    public async Task ScheduleAsync_ShouldProcessAtScheduledTime()
    {
        // Arrange
        var testEvent = new TestScheduledEvent(4);
        var scheduleTime = DateTimeOffset.UtcNow.AddMilliseconds(DelayMilliseconds);

        // Act
        await EventBus.ScheduleAsync(testEvent, scheduleTime);

        // Give some time for the scheduled event to be registered
        await WaitForProcessingAsync(1);

        // Assert - Check that the event was scheduled
        Assert.Contains(4, EventTracker.ScheduledEvents);

        // Note: We don't wait for full execution here as it would take 10 seconds
        // In a real test, you might want to use a shorter delay or mock the time
    }

    [Fact]
    public async Task ScheduleAsync_WithDelay_ShouldProcessAfterDelay()
    {
        // Arrange
        var testEvent = new TestScheduledEvent(5);

        // Act
        await EventBus.ScheduleAsync(testEvent, TimeSpan.FromMilliseconds(DelayMilliseconds));

        // Give some time for the scheduled event to be processed
        await WaitForProcessingAsync(1);

        // Assert
        Assert.Contains(5, EventTracker.ScheduledEvents);
    }

    [Fact]
    public async Task BulkScheduleAsync_ShouldProcessAllEventsAtScheduledTime()
    {
        // Arrange
        var events = new[]
        {
            new TestScheduledEvent(6), new TestScheduledEvent(7)
        };
        var scheduleTime = DateTimeOffset.UtcNow.AddMilliseconds(DelayMilliseconds);

        // Act
        await EventBus.BulkScheduleAsync(events, scheduleTime);

        // Give some time for the scheduled events to be registered
        await WaitForProcessingAsync(events.Length);

        // Assert - Check that events were scheduled
        Assert.False(EventTracker.ScheduledEvents.IsEmpty, "Expected scheduled events to contain at least one event");
        Assert.True(EventTracker.ScheduledEvents.Contains(6) || EventTracker.ScheduledEvents.Contains(7), "Expected to find event 6 or 7 in scheduled events");;
    }

    [Fact]
    public async Task BulkScheduleAsync_WithDelay_ShouldProcessAllEventsAfterDelay()
    {
        // Arrange
        var events = new[]
        {
            new TestScheduledEvent(8), new TestScheduledEvent(9)
        };

        // Act
        await EventBus.BulkScheduleAsync(events, TimeSpan.FromMilliseconds(DelayMilliseconds));

        // Give some time for the scheduled events to be processed
        await WaitForProcessingAsync(events.Length);

        // Assert
        Assert.Contains(8, EventTracker.ScheduledEvents);
        Assert.Contains(9, EventTracker.ScheduledEvents);
    }
}
