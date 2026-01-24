# Quick Reference Guide

## Installation

```bash
dotnet add package Softoverse.EventBus.InMemory
```

## Basic Setup

### 1. Configure in Program.cs

```csharp
using Softoverse.EventBus.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Add EventBus
builder.Services.AddEventBus<DefaultEventProcessor>(
    builder.Configuration,
    [typeof(Program).Assembly]
);

var app = builder.Build();
app.Run();
```

### 2. Configure in appsettings.json

```json
{
  "EventBusSettings": {
    "EventBusType": "Channel",
    "ChannelCapacity": -1,
    "EventProcessorCapacity": 10
  }
}
```

## Define Events

### Using EventBase (Recommended)

```csharp
public class OrderCreatedEvent : EventBase
{
    public string OrderNumber { get; init; }
    public decimal Amount { get; init; }
}
```

### Using IEvent

```csharp
public class PaymentEvent : IEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string PaymentId { get; init; }
}
```

## Create Handlers

```csharp
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    
    public OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("Order {OrderNumber} created", @event.OrderNumber);
        // Your logic here
    }
}
```

## Publish Events

### Single Event

```csharp
public class OrderService
{
    private readonly IEventBus _eventBus;
    
    public OrderService(IEventBus eventBus) => _eventBus = eventBus;
    
    public async Task CreateOrderAsync(Order order)
    {
        await _eventBus.PublishAsync(new OrderCreatedEvent 
        { 
            OrderNumber = order.Number,
            Amount = order.Amount 
        });
    }
}
```

### Multiple Events

```csharp
var events = orders.Select(o => new OrderCreatedEvent 
{ 
    OrderNumber = o.Number 
}).ToList();

await _eventBus.BulkPublishAsync(events);
```

### Request-Response Pattern

```csharp
var result = await _eventBus.InvokeAsync<OrderQuery, OrderResult>(
    new OrderQuery { OrderId = "123" }
);
```

## Create Event Processor

```csharp
public class DefaultEventProcessor : IEventProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DefaultEventProcessor> _logger;
    
    public DefaultEventProcessor(
        IServiceProvider serviceProvider,
        ILogger<DefaultEventProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public async Task ProcessEventAsync(IEvent @event, CancellationToken ct = default)
    {
        await ProcessEventHandlersAsync(@event, ct);
    }
    
    public async Task ProcessEventHandlersAsync(IEvent @event, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler>()
            .Where(h => h.CanHandle(@event));
        
        var tasks = handlers.Select(h => SafeHandleAsync(h, @event, ct));
        await Task.WhenAll(tasks);
    }
    
    public Task<TResult> InvokeAsync<TResult>(IEvent @event, CancellationToken ct = default)
    {
        // Implement for request-response pattern
        throw new NotImplementedException();
    }
    
    private async Task SafeHandleAsync(IEventHandler handler, IEvent @event, CancellationToken ct)
    {
        try
        {
            await handler.HandleAsync(@event, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler {Handler} failed", handler.GetType().Name);
        }
    }
}
```

## Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| EventBusType | "Channel" | "Channel" or "General" |
| ChannelCapacity | -1 | -1 (unbounded) or positive number |
| EventProcessorCapacity | 10 | Max concurrent processors |
| MaxConcurrency | 1000 | Max concurrent operations |
| RetryCount | 10 | Max retry attempts |
| RetryAfterSeconds | 5 | Delay before retry |
| EachRetryInterval | 3 | Seconds between retries |

## Common Patterns

### Multiple Handlers for One Event

```csharp
// Email notification
public class EmailHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _emailService.SendAsync(@event);
    }
}

// Inventory update
public class InventoryHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _inventoryService.UpdateAsync(@event);
    }
}
```

### Handler with Dependencies

```csharp
public class ComplexHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IOrderRepository _repository;
    private readonly IEmailService _emailService;
    private readonly ILogger _logger;
    
    public ComplexHandler(
        IOrderRepository repository,
        IEmailService emailService,
        ILogger<ComplexHandler> logger)
    {
        _repository = repository;
        _emailService = emailService;
        _logger = logger;
    }
    
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        var order = await _repository.GetAsync(@event.OrderNumber);
        await _emailService.SendConfirmationAsync(order);
        _logger.LogInformation("Processed order {OrderNumber}", @event.OrderNumber);
    }
}
```

### Idempotent Handler

```csharp
public class IdempotentHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IProcessedEventsRepository _processed;
    
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Check if already processed
        if (await _processed.ExistsAsync(@event.Id))
        {
            _logger.LogInformation("Event {Id} already processed", @event.Id);
            return;
        }
        
        // Process
        await ProcessOrderAsync(@event);
        
        // Mark as processed
        await _processed.AddAsync(@event.Id);
    }
}
```

### Publishing Events from Handler

```csharp
public class OrderHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IEventBus _eventBus;
    
    public OrderHandler(IEventBus eventBus) => _eventBus = eventBus;
    
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Process order
        await ProcessOrderAsync(@event);
        
        // Publish follow-up event
        await _eventBus.PublishAsync(new OrderProcessedEvent 
        { 
            OrderNumber = @event.OrderNumber 
        });
    }
}
```

## Testing

### Unit Test Handler

```csharp
[Fact]
public async Task Handler_Should_ProcessEvent()
{
    // Arrange
    var mockService = new Mock<IOrderService>();
    var handler = new OrderCreatedHandler(mockService.Object);
    var @event = new OrderCreatedEvent { OrderNumber = "123" };
    
    // Act
    await handler.HandleAsync(@event);
    
    // Assert
    mockService.Verify(s => s.ProcessAsync("123"), Times.Once);
}
```

### Integration Test

```csharp
[Fact]
public async Task EventBus_Should_DeliverToAllHandlers()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEventBus<DefaultEventProcessor>(configuration, [typeof(Handler).Assembly]);
    var sp = services.BuildServiceProvider();
    var eventBus = sp.GetRequiredService<IEventBus>();
    
    // Act
    await eventBus.PublishAsync(new TestEvent());
    await Task.Delay(100); // Wait for channel processing
    
    // Assert
    // Verify handlers were called
}
```

## Troubleshooting

### Events Not Processing

```csharp
// ✅ Ensure handlers are registered
builder.Services.AddEventBus<DefaultEventProcessor>(
    builder.Configuration,
    [typeof(Program).Assembly]  // Include handler assembly
);

// ✅ Ensure handler implements interface
public class MyHandler : IEventHandler<MyEvent>  // Must implement this
{
    public async Task HandleAsync(MyEvent @event, CancellationToken ct) { }
}

// ✅ For Channel mode, ensure app is running
await app.RunAsync();  // Starts background service
```

### Enable Debug Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Softoverse.EventBus.InMemory": "Debug"
    }
  }
}
```

### Check Configuration

```csharp
var settings = builder.Configuration.GetEventBusSettings();
Console.WriteLine($"EventBusType: {settings.EventBusType}");
Console.WriteLine($"EventProcessorCapacity: {settings.EventProcessorCapacity}");
```

## Performance Tips

### High Throughput

```json
{
  "EventBusSettings": {
    "EventBusType": "Channel",
    "ChannelCapacity": -1,
    "EventProcessorCapacity": 50
  }
}
```

### Memory Constrained

```json
{
  "EventBusSettings": {
    "EventBusType": "Channel",
    "ChannelCapacity": 1000,
    "EventProcessorCapacity": 10
  }
}
```

### CPU-Bound Handlers

```json
{
  "EventBusSettings": {
    "EventProcessorCapacity": 4  // Number of CPU cores
  }
}
```

### I/O-Bound Handlers

```json
{
  "EventBusSettings": {
    "EventProcessorCapacity": 50  // Higher for I/O operations
  }
}
```

## Best Practices

✅ **DO**:
- Use `EventBase` for events
- Make events immutable (use `init` accessors)
- Keep handlers independent
- Implement idempotency
- Use scoped services in handlers
- Log errors in handlers
- Test handlers in isolation

❌ **DON'T**:
- Share mutable state between handlers
- Block in async methods
- Throw exceptions without logging
- Use singleton services with scoped dependencies
- Ignore cancellation tokens
- Create handlers with side effects in constructor

## Quick Command Reference

```bash
# Install package
dotnet add package Softoverse.EventBus.InMemory

# Build
dotnet build

# Run
dotnet run

# Pack
dotnet pack -c Release
```

## Links

- [Full Documentation](../README.md)
- [Architecture](ARCHITECTURE.md)
- [Contributing](../CONTRIBUTING.md)
- [GitHub](https://github.com/mahmudabir/EventBus)
- [NuGet](https://www.nuget.org/packages/Softoverse.EventBus.InMemory/)
