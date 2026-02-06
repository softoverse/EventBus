using EventBus.InMemory.Tests.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Softoverse.EventBus.InMemory;
using Softoverse.EventBus.InMemory.Abstractions;

namespace EventBus.InMemory.Tests
{
    public class UnitTest : IAsyncLifetime
    {
        private IHost _host;
        private IConfiguration _configuration;
        private IEventBus _eventBus;
        private readonly int _delaySeconds = 2;

        public async Task InitializeAsync()
        {
            var hostBuilder = Host.CreateApplicationBuilder();

            // Load appsettings.json into configuration
            hostBuilder.Configuration.AddJsonFile("appSetttings.json", optional: false, reloadOnChange: true);

            _configuration = hostBuilder.Configuration;
            hostBuilder.Services.AddEventBus<EventProcessor>(hostBuilder.Configuration);
            hostBuilder.Services.AddScoped<IEventHandler<TestEvent>, TestEventHandler>();

            _host = hostBuilder.Build();
            await _host.StartAsync();

            await using var scope = _host.Services.CreateAsyncScope(); // Ensure the scope is created before resolving services
            _eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            // Clear tracking before each test
            EventTracker.Clear();
        }

        public async Task DisposeAsync()
        {
            await _host.StopAsync();
        }

        [Fact]
        public async Task InvokeAsync()
        {
            // Arrange
            var testEvent = new TestEvent(7);

            // Act
            var result = await _eventBus.InvokeAsync<bool>(testEvent);

            // Assert
            Assert.True(result);
            Assert.Contains(7, EventTracker.InvokedEvents);
        }
        
        [Fact]
        public async Task PublishAsync()
        {
            // Arrange
            var testEvent = new TestEvent(1);

            // Act
            await _eventBus.PublishAsync(testEvent);

            // Give some time for the event to be processed (since it's fire-and-forget)
            await Task.Delay(500);

            // Assert
            Assert.Contains(1, EventTracker.ProcessedEvents);
        }

        [Fact]
        public async Task BulkPublishAsync()
        {
            // Arrange
            TestEvent[] events = [new TestEvent(2), new TestEvent(3)];

            // Act
            await _eventBus.BulkPublishAsync(events);

            // Give some time for the events to be processed
            await Task.Delay(500);

            // Assert
            Assert.Contains(2, EventTracker.ProcessedEvents);
            Assert.Contains(3, EventTracker.ProcessedEvents);
            Assert.True(EventTracker.ProcessedEvents.Count >= 2);
        }

        [Fact]
        public async Task ScheduleAsync()
        {
            // Arrange
            var testEvent = new TestEvent(4);
            var scheduleTime = DateTimeOffset.Now.AddSeconds(_delaySeconds);

            // Act
            await _eventBus.ScheduleAsync(testEvent, scheduleTime);

            // Give some time for the scheduled event to be registered
            await Task.Delay(_delaySeconds + 1);

            // Assert - Check that the event was scheduled
            Assert.Contains(4, EventTracker.ScheduledEvents);

            // Note: We don't wait for full execution here as it would take 10 seconds
            // In a real test, you might want to use a shorter delay or mock the time
        }

        [Fact]
        public async Task BulkScheduleAsync()
        {
            // Arrange
            var events = new[]
            {
                new TestEvent(5), new TestEvent(6)
            };
            var scheduleTime = DateTimeOffset.Now.AddSeconds(_delaySeconds);

            // Act
            await _eventBus.BulkScheduleAsync(events, scheduleTime);

            // Give some time for the scheduled events to be registered
            await Task.Delay(_delaySeconds + _delaySeconds + 1);

            // Assert - Check that events were scheduled
            Assert.True(EventTracker.ScheduledEvents.Count >= 1, $"Expected at least 1 scheduled event, but got {EventTracker.ScheduledEvents.Count}");
            Assert.True(EventTracker.ScheduledEvents.Contains(5) || EventTracker.ScheduledEvents.Contains(6), "Expected to find event 5 or 6 in scheduled events");
        }

        [Fact]
        public async Task PublishAsync_MultipleEvents_AllProcessed()
        {
            // Arrange & Act
            await _eventBus.PublishAsync(new TestEvent(8));
            await _eventBus.PublishAsync(new TestEvent(9));
            await _eventBus.PublishAsync(new TestEvent(10));

            // Give some time for all events to be processed
            await Task.Delay(1000);

            // Assert
            Assert.Contains(8, EventTracker.ProcessedEvents);
            Assert.Contains(9, EventTracker.ProcessedEvents);
            Assert.Contains(10, EventTracker.ProcessedEvents);
        }
    }
}
