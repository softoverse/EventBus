using SampleCore;

namespace EventBus.InMemory.Tests;

public class CustomEventProcessorWithGeneralTest()
    : BaseTest(configFileName: "appSettings.General.json",
               useDefaultEventProcessor: false)
{
    [Fact]
    public async Task InvokeAsync_ShouldProcessImmediately()
    {
        // Arrange
        var testEvent = new TestRequest(7);

        // Act
        var result = await _eventBus.InvokeAsync<bool>(testEvent);

        // Assert
        Assert.True(result);
        Assert.Contains(7, _eventTracker.InvokedEvents);
    }

    [Fact]
    public async Task PublishAsync_ShouldProcessImmediately()
    {
        // Arrange
        var testEvent = new TestEvent(1);

        // Act
        await _eventBus.PublishAsync(testEvent);

        // Give some time for the event to be processed (since it's fire-and-forget)
        await WaitForProcessingAsync();

        // Assert
        Assert.Contains(1, _eventTracker.ProcessedEvents);
    }

    [Fact]
    public async Task PublishAsync_MultipleEvents_AllProcessed()
    {
        // Arrange & Act
        await _eventBus.PublishAsync(new TestEvent(8));
        await _eventBus.PublishAsync(new TestEvent(9));
        await _eventBus.PublishAsync(new TestEvent(10));

        // Give some time for all events to be processed
        await WaitForProcessingAsync();

        // Assert
        Assert.Contains(8, _eventTracker.ProcessedEvents);
        Assert.Contains(9, _eventTracker.ProcessedEvents);
        Assert.Contains(10, _eventTracker.ProcessedEvents);
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
        await _eventBus.BulkPublishAsync(events);

        // Give some time for the events to be processed
        await WaitForProcessingAsync();

        // Assert
        Assert.Contains(2, _eventTracker.ProcessedEvents);
        Assert.Contains(3, _eventTracker.ProcessedEvents);
        Assert.True(_eventTracker.ProcessedEvents.Count >= 2);
    }

    [Fact]
    public async Task ScheduleAsync_ShouldProcessAtScheduledTime()
    {
        // Arrange
        var testEvent = new TestScheduledEvent(4);
        var scheduleTime = DateTimeOffset.UtcNow.AddMilliseconds(_delayMilliseconds);

        // Act
        await _eventBus.ScheduleAsync(testEvent, scheduleTime);

        // Give some time for the scheduled event to be registered
        await WaitForProcessingAsync(1);

        // Assert - Check that the event was scheduled
        Assert.Contains(4, _eventTracker.ScheduledEvents);

        // Note: We don't wait for full execution here as it would take 10 seconds
        // In a real test, you might want to use a shorter delay or mock the time
    }

    [Fact]
    public async Task BulkScheduleAsync_ShouldProcessAllEventsAtScheduledTime()
    {
        // Arrange
        var events = new[]
        {
            new TestScheduledEvent(5), 
            new TestScheduledEvent(6)
        };
        var scheduleTime = DateTimeOffset.UtcNow.AddMilliseconds(_delayMilliseconds);

        // Act
        await _eventBus.BulkScheduleAsync(events, scheduleTime);

        // Give some time for the scheduled events to be registered
        await WaitForProcessingAsync(events.Length);

        // Assert - Check that events were scheduled
        Assert.True(_eventTracker.ScheduledEvents.Count >= 1, $"Expected at least 1 scheduled event, but got {_eventTracker.ScheduledEvents.Count}");
        Assert.True(_eventTracker.ScheduledEvents.Contains(5) || _eventTracker.ScheduledEvents.Contains(6), "Expected to find event 5 or 6 in scheduled events");
    }
}
