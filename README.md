# Softoverse.EventBus.InMemory

[![NuGet Version](https://img.shields.io/nuget/v/Softoverse.EventBus.InMemory.svg)](https://www.nuget.org/packages/Softoverse.EventBus.InMemory/)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

A lightweight, high-performance in-memory event bus for .NET 10+ applications. Enables decoupled communication through the publish-subscribe pattern with built-in support for scheduled events. Perfect for domain events, CQRS, and event-driven architectures within a single application.

## 📋 Table of Contents

- [Features](#-features)
- [Installation](#-installation)
- [Quick Start](#-quick-start)
- [Configuration](#-configuration)
- [Usage Examples](#-usage-examples)
- [Processing Modes](#-processing-modes)
- [Advanced Scenarios](#-advanced-scenarios)
- [Best Practices](#-best-practices)
- [Contributing](#-contributing)

## ✨ Features

- ⚡ **High Performance**: Built on `System.Threading.Channels` for optimal throughput
- 🔄 **Async First**: Non-blocking event publishing and processing
- 📅 **Event Scheduling**: Schedule events to be processed at specific times
- 🛡️ **Type-Safe**: Strongly-typed contracts with compile-time safety
- 🔧 **Flexible**: Extensible architecture with custom processors
- 🧵 **Concurrency Control**: Built-in capacity management
- 📝 **Rich Logging**: Comprehensive logging for monitoring
- 🧩 **DI Native**: First-class dependency injection support
- 🎯 **Request-Response**: Support for query patterns via `InvokeAsync`

## 📦 Installation

```bash
dotnet add package Softoverse.EventBus.InMemory
```

## 🚀 Quick Start

### Step 1: Define Your Events

```csharp
using Softoverse.EventBus.InMemory.Abstractions;

public class OrderCreatedEvent : IEvent
{
    public string OrderId { get; set; }
    public decimal Amount { get; set; }
    public string CustomerId { get; set; }
}
```

### Step 2: Create Event Handlers

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
        _logger.LogInformation("Processing order {OrderId}", @event.OrderId);
        
        // Your business logic here
        await Task.CompletedTask;
    }
}
```

### Step 3: Register in Program.cs

```csharp
using Softoverse.EventBus.InMemory;
using Softoverse.EventBus.InMemory.Infrastructure.Processors;

var builder = WebApplication.CreateBuilder(args);

// Register with InMemoryEventProcessor (recommended for most cases)
builder.Services.AddEventBus(
    builder.Configuration,
    [typeof(Program).Assembly]  // Assemblies containing your handlers
);

var app = builder.Build();
app.Run();
```

### Step 4: Configure appsettings.json

```json
{
  "EventBusSettings": {
    "EventProcessorCapacity": 10
  }
}
```

### Step 5: Publish Events

```csharp
public class OrderService
{
    private readonly IEventBus _eventBus;

    public OrderService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        // Your order creation logic...
        
        // Publish event
        await _eventBus.PublishAsync(new OrderCreatedEvent
        {
            OrderId = order.Id,
            Amount = order.Amount,
            CustomerId = order.CustomerId
        });
    }
}
```

## ⚙️ Configuration

### Configuration Options

| Property | Description | Default |
|----------|-------------|---------|
| `EventProcessorCapacity` | Max concurrent event processors | 10 |
| `ChannelCapacity` | Channel buffer size (-1 = unbounded) | -1 |
| `ExecuteAfterSeconds` | Interval for checking scheduled events | 2 |
| `RetryCount` | Maximum retry attempts (for custom processors) | 10 |
| `EachRetryInterval` | Seconds between retries | 3 |

### Example Configuration

```json
{
  "EventBusSettings": {
    "EventProcessorCapacity": 10,
    "ChannelCapacity": -1,
    "ExecuteAfterSeconds": 2,
    "RetryCount": 10,
    "EachRetryInterval": 3
  }
}
```

## 📖 Usage Examples

### Publishing Events

#### Single Event

```csharp
await _eventBus.PublishAsync(new OrderCreatedEvent { OrderId = "123" });
```

#### Multiple Events

```csharp
var events = new List<OrderCreatedEvent>
{
    new() { OrderId = "123" },
    new() { OrderId = "124" },
    new() { OrderId = "125" }
};

await _eventBus.BulkPublishAsync(events);
```

### Scheduling Events

#### Schedule Single Event

```csharp
var reminderDate = DateTimeOffset.UtcNow.AddDays(7);
await _eventBus.ScheduleAsync(
    new SubscriptionReminderEvent { SubscriptionId = "sub-123" },
    reminderDate
);
```

#### Schedule Multiple Events

```csharp
var reminders = new List<SubscriptionReminderEvent>
{
    new() { SubscriptionId = "sub-123", DaysRemaining = 7 },
    new() { SubscriptionId = "sub-124", DaysRemaining = 7 }
};

var scheduleTime = DateTimeOffset.UtcNow.AddDays(7);
await _eventBus.BulkScheduleAsync(reminders, scheduleTime);
```

### Scheduled Event Status Tracking

The EventBus includes comprehensive status tracking for scheduled events to prevent duplicate execution and provide visibility into event processing:

#### Status Types

- **Pending**: Event is waiting to be processed
- **InProgress**: Event is currently being processed
- **Done**: Event completed successfully
- **Failed**: Event processing failed (with failure reason)
- **Skipped**: Event was skipped and won't be processed

#### Automatic Status Management

The system automatically manages event statuses to prevent duplicate execution:

1. When an event becomes due, it's immediately marked as **InProgress**
2. If processing succeeds, it's marked as **Done** and removed from the store
3. If processing fails, it's marked as **Failed** with the error message and removed
4. Events stuck in **InProgress** for too long (default: 5 minutes) are considered stale and can be retried

#### Configuring InProgress Timeout

You can control how long an event stays in InProgress state before being considered stale:

```csharp
// In your startup/configuration
builder.Services.AddSingleton<ScheduledEventStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ScheduledEventStore>>();
    var store = new ScheduledEventStore(logger)
    {
        InProgressTimeoutMinutes = 10  // Default is 5 minutes
    };
    return store;
});
```

#### Querying Event Status

You can query events by status for monitoring or debugging:

```csharp
// Get the ScheduledEventStore from DI
var store = serviceProvider.GetRequiredService<ScheduledEventStore>();

// Get all pending events
var pendingEvents = store.GetEventsByStatus(ScheduledEventStatus.Pending);

// Get all failed events to investigate issues
var failedEvents = store.GetEventsByStatus(ScheduledEventStatus.Failed);
foreach (var entry in failedEvents)
{
    Console.WriteLine($"Event {entry.Id} failed: {entry.Remarks}");
}

// Get all in-progress events
var inProgressEvents = store.GetEventsByStatus(ScheduledEventStatus.InProgress);

// Manually mark an event as skipped
store.MarkAsSkipped(eventId, "Cancelled by user");

// Get total count of scheduled events
int totalEvents = store.Count;

// Get next scheduled event time
DateTimeOffset? nextEventTime = store.GetNextScheduledTime();
```

#### Why Status Tracking Matters

**Problem**: When debugging handlers (e.g., using breakpoints), the background service continues polling and may pick up the same event multiple times, causing duplicate execution.

**Solution**: Status tracking ensures:
- Events marked as **InProgress** won't be picked up again (unless they time out)
- Failed events are tracked with failure reasons for troubleshooting
- You can query and monitor the state of all scheduled events
- Prevents race conditions in distributed scenarios

#### Example: Monitoring Scheduled Events

```csharp
public class ScheduledEventMonitor : BackgroundService
{
    private readonly ScheduledEventStore _store;
    private readonly ILogger<ScheduledEventMonitor> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pending = _store.GetEventsByStatus(ScheduledEventStatus.Pending);
            var inProgress = _store.GetEventsByStatus(ScheduledEventStatus.InProgress);
            var failed = _store.GetEventsByStatus(ScheduledEventStatus.Failed);

            _logger.LogInformation(
                "Scheduled Events - Pending: {Pending}, InProgress: {InProgress}, Failed: {Failed}",
                pending.Count, inProgress.Count, failed.Count);

            // Alert if events are stuck in InProgress for too long
            foreach (var evt in inProgress)
            {
                var stuckDuration = DateTimeOffset.UtcNow - evt.StatusUpdatedAt;
                if (stuckDuration > TimeSpan.FromMinutes(15))
                {
                    _logger.LogWarning(
                        "Event {EventId} has been InProgress for {Duration} minutes",
                        evt.Id, stuckDuration.TotalMinutes);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

### Request-Response Pattern

For queries that need responses:

```csharp
// 1. Define a request handler
public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto>
{
    public async Task<OrderDto> HandleAsync(GetOrderQuery query, CancellationToken ct)
    {
        // Fetch and return order
        return await _repository.GetOrderAsync(query.OrderId);
    }
}

// 2. Use InvokeAsync
var result = await _eventBus.InvokeAsync<OrderDto>(new GetOrderQuery 
{ 
    OrderId = "123" 
});
```

### Multiple Handlers for Same Event

```csharp
// Handler 1: Send email
public class OrderEmailHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _emailService.SendOrderConfirmationAsync(@event.CustomerId);
    }
}

// Handler 2: Update inventory
public class OrderInventoryHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _inventoryService.ReserveItemsAsync(@event.OrderId);
    }
}

// Handler 3: Log audit
public class OrderAuditHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _auditService.LogOrderCreatedAsync(@event);
    }
}

// All three handlers execute in parallel when the event is published
```

## 🔄 Processing Mode

The EventBus uses a Channel-based architecture for high-performance asynchronous processing with background workers.

**Benefits:**
- Non-blocking publishers (immediate return)
- High throughput via background processing
- Built-in concurrency control
- Automatic scheduling support

**Characteristics:**
- High event volume support
- Optimized for performance
- Eventual consistency model

## 🚀 Advanced Scenarios

### Custom Event Processor

If you need custom behavior (retry logic, circuit breakers, etc.), create a custom processor:

```csharp
public class CustomEventProcessor : IEventProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public CustomEventProcessor(
        IServiceScopeFactory scopeFactory, 
        ILogger<CustomEventProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ProcessEventAsync(IEvent @event, CancellationToken ct)
    {
        // Custom pre-processing logic
        _logger.LogInformation("Custom processing for {EventType}", @event.GetType().Name);
        
        await ProcessEventHandlersAsync(@event, ct);
    }

    public async Task ProcessScheduledEventAsync(
        IEvent @event, 
        DateTimeOffset scheduledTime, 
        CancellationToken ct)
    {
        // Calculate delay
        var delay = scheduledTime.ToUniversalTime() - DateTimeOffset.UtcNow;
        
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ct);
        }
        
        await ProcessEventHandlersAsync(@event, ct);
    }

    public async Task ProcessEventHandlersAsync(IEvent @event, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler>();
        
        var applicableHandlers = handlers.Where(h => h.CanHandle(@event));
        
        var tasks = applicableHandlers.Select(h => 
            SafeHandleAsync(h, @event, ct));
        
        await Task.WhenAll(tasks);
    }

    public async Task<TResult> InvokeAsync<TResult>(
        object @event, 
        CancellationToken ct)
    {
        // Custom invoke logic
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        var handlerType = typeof(IRequestHandler<,>)
            .MakeGenericType(@event.GetType(), typeof(TResult));
        
        var handler = scope.ServiceProvider.GetService(handlerType);
        
        if (handler is IRequestHandler baseHandler)
        {
            var result = await baseHandler.HandleAsync(@event, ct);
            return (TResult)result!;
        }
        
        return default!;
    }

    private async Task SafeHandleAsync(
        IEventHandler handler, 
        IEvent @event, 
        CancellationToken ct)
    {
        try
        {
            await handler.HandleAsync(@event, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler {Handler} failed", handler.GetType().Name);
            // Don't rethrow - isolate failures
        }
    }
}
```

Register your custom processor:

```csharp
builder.Services.AddEventBus<CustomEventProcessor>(
    builder.Configuration,
    [typeof(Program).Assembly]
);
```

### Handler with Scoped Dependencies

```csharp
public class OrderHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IOrderRepository _repository;
    private readonly IEmailService _emailService;
    private readonly ILogger _logger;

    public OrderHandler(
        IOrderRepository repository,
        IEmailService emailService,
        ILogger<OrderHandler> logger)
    {
        _repository = repository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(@event.OrderId);
        
        if (order != null)
        {
            await _emailService.SendConfirmationAsync(order.CustomerId, order);
            _logger.LogInformation("Order confirmation sent for {OrderId}", order.Id);
        }
    }
}
```

### Conditional Handling

Handlers can implement conditional logic:

```csharp
public class PremiumCustomerHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ICustomerService _customerService;

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        var customer = await _customerService.GetByIdAsync(@event.CustomerId);
        
        // Only process for premium customers
        if (customer.IsPremium)
        {
            // Apply special premium processing
            await ApplyPremiumBenefitsAsync(customer, @event);
        }
    }
}
```

## 🎯 Best Practices

### 1. **Keep Handlers Focused**

Each handler should have a single responsibility:

```csharp
// ✅ Good - Single responsibility
public class SendOrderEmailHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _emailService.SendOrderConfirmationAsync(@event);
    }
}

// ❌ Bad - Multiple responsibilities
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _emailService.SendEmail(@event);
        await _inventoryService.UpdateInventory(@event);
        await _analyticsService.TrackOrder(@event);
        await _loyaltyService.AddPoints(@event);
    }
}
```

### 2. **Use Channel Mode for Production**

For production workloads, use Channel mode for better performance:

```json
{
  "EventBusSettings": {
    "EventBusType": "Channel",
    "EventProcessorCapacity": 10
  }
}
```

### 3. **Handle Exceptions Gracefully**

Don't let one handler failure affect others:

```csharp
public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
{
    try
    {
        await _service.ProcessOrderAsync(@event);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process order {OrderId}", @event.OrderId);
        // Consider: Dead letter queue, retry logic, alerting, etc.
    }
}
```

### 4. **Use Descriptive Event Names**

```csharp
// ✅ Good
public class OrderCreatedEvent : IEvent { }
public class PaymentProcessedEvent : IEvent { }
public class InventoryReservedEvent : IEvent { }

// ❌ Bad
public class Event1 : IEvent { }
public class DataChanged : IEvent { }
```

### 5. **Leverage Scheduled Events**

Use scheduling for time-based workflows:

```csharp
// Schedule reminder 7 days before expiry
var reminderDate = subscription.ExpiryDate.AddDays(-7);
await _eventBus.ScheduleAsync(
    new SubscriptionExpiryReminderEvent { SubscriptionId = subscription.Id },
    reminderDate
);
```

### 6. **Test with Both Modes**

Test with General mode for easier debugging, then use Channel mode for production:

```json
// appsettings.Development.json
{
  "EventBusSettings": {
    "EventBusType": "General"
  }
}

// appsettings.Production.json
{
  "EventBusSettings": {
    "EventBusType": "Channel",
    "EventProcessorCapacity": 20
  }
}
```

### 7. **Monitor Event Processing**

Add structured logging to track event flow:

```csharp
public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
{
    using var scope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["OrderId"] = @event.OrderId,
        ["EventType"] = nameof(OrderCreatedEvent)
    });
    
    _logger.LogInformation("Processing order creation");
    await ProcessOrderAsync(@event);
    _logger.LogInformation("Order processing completed");
}
```

## 🛠️ Troubleshooting

### Events Not Processing

1. **Check Configuration**: Ensure `EventBusSettings` is in `appsettings.json`
2. **Verify Registration**: Handlers must be in assemblies passed to `AddEventBus()`
3. **Check Logs**: Look for errors in application logs

### Scheduled Events Not Executing

1. **Verify InMemoryEventProcessor**: Use the parameterless `AddEventBus()` overload
2. **Check Time**: Ensure scheduled time is in the future
3. **Review Logs**: Check `ScheduledEventProcessingHostedService` logs
4. **Check Status**: Query `ScheduledEventStore.GetEventsByStatus()` to see if events are stuck in InProgress or Failed state

### Events Executing Multiple Times

1. **Status Tracking**: The system automatically marks events as InProgress to prevent duplicate execution
2. **Timeout Configuration**: If events take longer to process, increase `InProgressTimeoutMinutes` on `ScheduledEventStore`
3. **Check for Errors**: Review failed events using `GetEventsByStatus(ScheduledEventStatus.Failed)` to identify issues

### Performance Issues

1. **Increase Capacity**: Adjust `EventProcessorCapacity` based on load
2. **Use Channel Mode**: Ensure using "Channel" mode for production
3. **Optimize Handlers**: Profile slow handlers and optimize them

## 📚 Additional Resources

- **Architecture Documentation**: See [ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed implementation details
- **GitHub Repository**: [https://github.com/softoverse/EventBus](https://github.com/softoverse/EventBus)
- **NuGet Package**: [https://www.nuget.org/packages/Softoverse.EventBus.InMemory/](https://www.nuget.org/packages/Softoverse.EventBus.InMemory/)

## 🤝 Contributing

Contributions are welcome! Please see [ARCHITECTURE.md](docs/ARCHITECTURE.md) to understand the codebase, then:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

---

**Made with ❤️ by Softoverse**
