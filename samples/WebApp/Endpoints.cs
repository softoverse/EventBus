using EventBus.InMemory.Tests.Implementations;
using Softoverse.EventBus.InMemory.Abstractions;

namespace WebApp;

public static class Endpoints
{
    extension(WebApplication app)
    {
        public void MapEndpoints()
        {
            app.MapGet("invoke", InvokeAsync);
            app.MapGet("publish", PublishAsync);
            app.MapGet("bulk-publish", BulkPublishAsync);
            app.MapGet("schedule", ScheduleAsync);
            app.MapGet("bulk-schedule", BulkScheduleAsync);
        }
    }

    public static async Task<IResult> InvokeAsync(IEventBus eventBus, CancellationToken ct = default)
    {
        bool result = await eventBus.InvokeAsync<bool>(new TestRequest(1), ct);
        return Results.Ok(new
        {
            result
        });
    }

    public static async Task<IResult> PublishAsync(IEventBus eventBus, CancellationToken ct = default)
    {
        await eventBus.PublishAsync(new TestEvent(1), ct);
        return Results.Ok();
    }

    public static async Task<IResult> BulkPublishAsync(IEventBus eventBus, CancellationToken ct = default)
    {
        await eventBus.BulkPublishAsync([
                                            new TestEvent(1),
                                            new TestEvent(2)
                                        ],
                                        ct);
        return Results.Ok();
    }

    public static async Task<IResult> ScheduleAsync(IEventBus eventBus, CancellationToken ct = default)
    {
        await eventBus.ScheduleAsync(new TestScheduledEvent(1), DateTimeOffset.UtcNow.AddSeconds(10), ct);
        return Results.Ok();
    }

    public static async Task<IResult> BulkScheduleAsync(IEventBus eventBus, CancellationToken ct = default)
    {
        await eventBus.BulkScheduleAsync([
                                             new TestScheduledEvent(1),
                                             new TestScheduledEvent(1)
                                         ],
                                         DateTimeOffset.UtcNow.AddSeconds(10),
                                         ct);
        return Results.Ok();
    }
}
