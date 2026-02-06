# Softoverse.EventBus.InMemory

[![NuGet Version](https://img.shields.io/nuget/v/Softoverse.EventBus.InMemory.svg)](https://www.nuget.org/packages/Softoverse.EventBus.InMemory/)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

A lightweight, high-performance in-memory event bus implementation for .NET 10+ applications that enables decoupled communication through the publish-subscribe pattern. Perfect for implementing domain events, CQRS architectures, and microservice communication patterns within a single application instance.

## 📋 Table of Contents

- [Features](#-features)
- [Installation](#-installation)
- [Quick Start](#-quick-start)
- [Architecture & Design](#-architecture--design)
- [Configuration](#-configuration)
- [Usage](#-usage)
- [Processing Strategies](#-processing-strategies)
- [API Reference](#-api-reference)
- [Advanced Usage](#-advanced-usage)
- [Best Practices](#-best-practices)
- [Testing](#-testing)
- [Troubleshooting](#-troubleshooting)
- [Performance](#-performance-characteristics)
- [Migration Guide](#-migration-guide)
- [Contributing](#-contributing)
- [License](#-license)

## ✨ Features

- **⚡ High Performance**: Built on .NET 9's `System.Threading.Channels` for optimal throughput
- **🔄 Asynchronous Processing**: Non-blocking event publishing and handling
- **🛡️ Type-Safe**: Strongly-typed event contracts with compile-time safety
- **🔧 Flexible Configuration**: Choose between Channel-based or General event processing
- **🧵 Concurrency Control**: Built-in semaphore-based concurrency management
- **🧱 Resilient**: Configurable retry policies and error handling
- **📦 Bulk Operations**: Support for bulk event publishing
- **📝 Extensive Logging**: Comprehensive logging for monitoring and debugging
- **🧩 Dependency Injection**: First-class DI container support

## 📦 Installation

Install the package via NuGet Package Manager:

```bash
dotnet add package Softoverse.EventBus.InMemory
```

Or via Package Manager Console:

```powershell
Install-Package Softoverse.EventBus.InMemory
```

## 🚀 Quick Start

### 1. Define Your Events

Create events by implementing the `IEvent` interface:

```csharp
using Softoverse.EventBus.InMemory.Abstractions;

public class OrderCreatedEvent : IEvent
{
    public string OrderNumber { get; set; }
    public decimal Amount { get; set; }
    public string CustomerId { get; set; }
}

public class PaymentProcessedEvent : IEvent
{
    public string PaymentId { get; set; }
    public decimal Amount { get; set; }
}
```

**Note:** You can optionally add an `Id` property to your events if needed:

```csharp
public class OrderCreatedEvent : IEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string OrderNumber { get; set; }
    public decimal Amount { get; set; }
    public string CustomerId { get; set; }
}
```

### 2. Create Event Handlers

Implement event handlers using the `IEventHandler<T>` interface:

```csharp
using Softoverse.EventBus.InMemory.Abstractions;

public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedEventHandler> _logger;
    
    public OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing order {OrderNumber} for customer {CustomerId}", 
            @event.OrderNumber, @event.CustomerId);
        
        // Your business logic here
        await ProcessOrderAsync(@event);
    }
    
    private async Task ProcessOrderAsync(OrderCreatedEvent orderEvent)
    {
        // Implementation details...
        await Task.Delay(100); // Simulate work
    }
}
```

### 3. Implement Event Processor

Create an event processor that orchestrates event handling:

```csharp
using Softoverse.EventBus.InMemory.Abstractions;

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

    public async Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing event {EventType}", 
            @event.GetType().Name);
            
        await ProcessEventHandlersAsync(@event, cancellationToken);
    }

    public async Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler>();
        
        var applicableHandlers = handlers.Where(h => h.CanHandle(@event)).ToList();
        
        if (!applicableHandlers.Any())
        {
            _logger.LogWarning("No handlers found for event type {EventType}", @event.GetType().Name);
            return;
        }

        var handlerTasks = applicableHandlers.Select(handler => 
            SafeHandleAsync(handler, @event, cancellationToken));
            
        await Task.WhenAll(handlerTasks);
    }
    
    private async Task SafeHandleAsync(IEventHandler handler, IEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler {HandlerType} failed to process event {EventType}", 
                handler.GetType().Name, @event.GetType().Name);
        }
    }
}
```

## ⚙️ Configuration

### Dependency Injection Setup

Register the event bus in your `Program.cs`:

```csharp
using Softoverse.EventBus.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Register EventBus with assemblies containing your handlers
builder.Services.AddEventBus<DefaultEventProcessor>(
    builder.Configuration,
    [typeof(Program).Assembly] // Add assemblies containing your event handlers
);

var app = builder.Build();
```

`AddEventBus` registers `IEventBus` and `IEventProcessor` as scoped services. If you need to use them inside singletons or hosted services, resolve them through a scope (for example, via `IServiceScopeFactory`).

### Configuration Options

Configure the event bus behavior in your `appsettings.json`:

```json
{
  "EventBusSettings": {
    "EventBusType": "Channel",
    "MaxConcurrency": 1000,
    "ChannelCapacity": -1,
    "EventProcessorCapacity": 10,
    "ExecuteAfterSeconds": 2,
    "RetryAfterSeconds": 5,
    "RetryCount": 10,
    "EachRetryInterval": 3
  }
}
```

#### Configuration Parameters

| Parameter | Description | Default Value |
|-----------|-------------|---------------|
| `EventBusType` | Processing strategy: "Channel" or "General" | "Channel" |
| `MaxConcurrency` | Maximum concurrent operations | 1000 |
| `ChannelCapacity` | Channel buffer size (-1 = unbounded) | -1 |
| `EventProcessorCapacity` | Max concurrent event processors | 10 |
| `ExecuteAfterSeconds` | Delay before initial execution | 2 |
| `RetryAfterSeconds` | Delay before retry attempts | 5 |
| `RetryCount` | Maximum retry attempts | 10 |
| `EachRetryInterval` | Seconds between retry attempts | 3 |

## 📖 Usage

### Publishing Events

Inject `IEventBus` into your services and publish events:

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
        // Create order logic...
        var order = new Order(request);

        // Publish single event
        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderNumber = order.Number,
            Amount = order.Amount,
            CustomerId = order.CustomerId
        };

        await _eventBus.PublishAsync(orderCreatedEvent);

        // Publish multiple events
        var events = new List<OrderCreatedEvent>
        {
            orderCreatedEvent,
            // ... more events
        };

        await _eventBus.BulkPublishAsync(events);
    }
}
```

### Scheduling Events

Schedule events to be processed at a specific time in the future:

```csharp
public class SubscriptionService
{
    private readonly IEventBus _eventBus;

    public SubscriptionService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task CreateSubscriptionAsync(Subscription subscription)
    {
        // Create subscription logic...

        // Schedule a reminder event to be processed 7 days before expiry
        var reminderDate = subscription.ExpiryDate.AddDays(-7);
        var reminderEvent = new SubscriptionExpiryReminderEvent
        {
            SubscriptionId = subscription.Id,
            CustomerId = subscription.CustomerId,
            ExpiryDate = subscription.ExpiryDate
        };

        await _eventBus.ScheduleAsync(reminderEvent, reminderDate);

        // Schedule multiple reminder events at different times
        var reminders = new List<SubscriptionExpiryReminderEvent>
        {
            new() { SubscriptionId = subscription.Id, DaysRemaining = 7 },
            new() { SubscriptionId = subscription.Id, DaysRemaining = 3 },
            new() { SubscriptionId = subscription.Id, DaysRemaining = 1 }
        };

        var oneWeekBefore = subscription.ExpiryDate.AddDays(-7);
        await _eventBus.BulkScheduleAsync(reminders, oneWeekBefore);

        // Schedule an event using relative time
        var processingDeadline = DateTimeOffset.UtcNow.AddHours(24);
        await _eventBus.ScheduleAsync(
            new PaymentProcessingDeadlineEvent { OrderId = order.Id },
            processingDeadline
        );
    }
}
```

### Multiple Event Handlers

You can have multiple handlers for the same event:

```csharp
public class EmailNotificationHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Send email notification
    }
}

public class InventoryUpdateHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Update inventory
    }
}

public class AuditLogHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Log audit entry
    }
}
```

## 🧠 Processing Strategies

### Channel-based Processing (Recommended)

Uses `System.Threading.Channels` for high-throughput, asynchronous event processing with a background service:

- **Pros**: High performance, non-blocking publishing, automatic retry handling
- **Cons**: Events processed asynchronously (eventual consistency)
- **Best for**: High-volume scenarios, microservices, domain events

```json
{
  "EventBusSettings": {
    "EventBusType": "Channel"
  }
}
```

### General Processing

Processes events immediately and synchronously:

- **Pros**: Immediate processing, simpler debugging
- **Cons**: Blocking operations, lower throughput
- **Best for**: Simple scenarios, debugging, immediate consistency requirements

```json
{
  "EventBusSettings": {
    "EventBusType": "General"
  }
}
```

## 🏗️ Architecture & Design

### System Architecture

The EventBus architecture consists of four main components:

```
┌─────────────┐      ┌──────────────┐      ┌────────────────────┐      ┌──────────────┐
│   Publisher │─────▶│   IEventBus  │─────▶│  IEventProcessor   │─────▶│ IEventHandler│
│  (Service)  │      │ (Channel/    │      │ (Process & Route)  │      │  (Multiple)  │
└─────────────┘      │  General)    │      └────────────────────┘      └──────────────┘
                     └──────────────┘
                           │
                     ┌─────┴──────┐
                     ▼            ▼
        ┌───────────────────────────────────┐
        │ ChannelEventsPublishingHostedSvc  │ (Immediate Events)
        │      (Background Worker)          │
        └───────────────────────────────────┘
        ┌───────────────────────────────────┐
        │ ChannelEventsSchedulingHostedSvc  │ (Scheduled Events)
        │      (Background Worker)          │
        └───────────────────────────────────┘
```

### Channel-Based Implementation Details

#### 1. **Event Publishing Flow**
```csharp
// Immediate Publishing
Publisher → IEventBus.PublishAsync() → PublishingChannel.Writer.WriteAsync() → Returns immediately

// Scheduled Publishing
Publisher → IEventBus.ScheduleAsync() → SchedulingChannel.Writer.WriteAsync() → Returns immediately
```

The `ChannelEventBus` uses two separate channels configured based on `ChannelCapacity`:

- **PublishingChannel**: For immediate event processing
- **SchedulingChannel**: For scheduled event processing (holds event + scheduled time tuple)
- **Unbounded Channel** (`ChannelCapacity = -1`): No capacity limit, events never lost
- **Bounded Channel** (`ChannelCapacity > 0`): Fixed capacity with `BoundedChannelFullMode.Wait` to prevent event loss

#### 2. **Background Processing**
```csharp
// Immediate Processing
ChannelEventsPublishingHostedService → PublishingChannel.Reader.ReadAsync() → IEventProcessor.ProcessEventAsync()

// Scheduled Processing
ChannelEventsSchedulingHostedService → SchedulingChannel.Reader.ReadAsync() → IEventProcessor.ProcessScheduledEventAsync()
```

The EventBus uses two separate `BackgroundService` implementations:

**`ChannelEventsPublishingHostedService`**:
- Dedicated background service for immediate event processing
- Continuously reads from `PublishingChannel` and processes events immediately
- Uses `SemaphoreSlim` to control concurrent processing (limit = `EventProcessorCapacity`)
- Processes events through `IEventProcessor.ProcessEventAsync()`
- Handles errors gracefully without crashing the service

**`ChannelEventsSchedulingHostedService`**:
- Dedicated background service for scheduled event processing
- Continuously reads from `SchedulingChannel` and processes scheduled events
- Uses its own `SemaphoreSlim` for independent concurrency control (limit = `EventProcessorCapacity`)
- Processes events through `IEventProcessor.ProcessScheduledEventAsync()`
- Handles errors gracefully without crashing the service

Both services run independently and concurrently for maximum throughput and separation of concerns.

#### 3. **Channel Configuration Options**

```csharp
// Unbounded Channel Options
var options = new UnboundedChannelOptions()
{
    SingleReader = false,        // Multiple consumers for max concurrency
    SingleWriter = false,        // Multiple publishers can write simultaneously
    AllowSynchronousContinuations = true  // Better performance
};

// Bounded Channel Options
var options = new BoundedChannelOptions(capacity)
{
    FullMode = BoundedChannelFullMode.Wait,  // Blocks publisher until space available
    SingleReader = false,        // Multiple concurrent readers
    SingleWriter = false,        // Multiple concurrent writers
    AllowSynchronousContinuations = true
};
```

**Configuration Impact:**
- `SingleReader = false`: Enables maximum concurrency but slightly reduces performance
- `SingleWriter = false`: Better for multi-threaded publishing scenarios
- `AllowSynchronousContinuations = true`: Continuations run on current thread for better performance
- `FullMode = Wait`: Guarantees no event loss (events will wait until space is available)

#### 4. **Concurrency Control**

The system uses a `SemaphoreSlim` to limit concurrent event processing:

```csharp
private readonly SemaphoreSlim semaphore = new(
    eventBusSettings.EventProcessorCapacity,  // Initial count
    eventBusSettings.EventProcessorCapacity   // Maximum count
);
```

This prevents overwhelming the system with too many concurrent operations.

### General Implementation Details

The `GeneralEventBus` processes events synchronously:

```csharp
Publisher → IEventBus.PublishAsync() → IEventProcessor.ProcessEventAsync() → Handlers → Returns
```

- No channel or background service involved
- Direct processing on the calling thread
- Simpler model but blocks the publisher until all handlers complete

### Event Handler Registration

The `AddEventBus<TEventProcessor>()` extension method automatically:

1. Registers `IEventBus` and `IEventProcessor` as **Scoped** services
2. Scans provided assemblies for `IEventHandler` implementations
3. Registers handlers as **Scoped** services
4. Registers both generic (`IEventHandler<TEvent>`) and non-generic (`IEventHandler`) interfaces
5. Allows multiple handlers per event type

```csharp
// Handlers are resolved from DI container per scope
using var scope = _serviceProvider.CreateScope();
var handlers = scope.ServiceProvider.GetServices<IEventHandler>();
var applicableHandlers = handlers.Where(h => h.CanHandle(@event));
```

### Event Processing Pipeline

#### Immediate Event Processing

1. **Event Published**: `IEventBus.PublishAsync(event)`
2. **Enqueued** (Channel mode): Event written to `PublishingChannel`
3. **Background Worker** (Channel mode): Reads from `PublishingChannel`
4. **Semaphore Acquired**: Waits if at capacity limit
5. **Processor Invoked**: `IEventProcessor.ProcessEventAsync(event)`
6. **Handlers Resolved**: DI container provides all applicable handlers
7. **Parallel Execution**: All handlers run concurrently via `Task.WhenAll()`
8. **Error Handling**: Exceptions caught per handler, don't affect others
9. **Semaphore Released**: Next event can be processed
10. **Logged**: Comprehensive logging at each step

#### Scheduled Event Processing

1. **Event Scheduled**: `IEventBus.ScheduleAsync(event, scheduleTime)`
2. **Time Conversion**: `scheduleTime` automatically converted to UTC
3. **Enqueued** (Channel mode): Event + scheduled time tuple written to `SchedulingChannel`
4. **Background Worker** (Channel mode): Reads from `SchedulingChannel`
5. **Semaphore Acquired**: Waits if at capacity limit
6. **Processor Invoked**: `IEventProcessor.ProcessScheduledEventAsync(event, scheduledTime)`
7. **Delay Calculation**: Calculates wait time until scheduled time
8. **Wait Period**: Delays execution until scheduled time arrives
9. **Handlers Resolved**: DI container provides all applicable handlers
10. **Parallel Execution**: All handlers run concurrently via `Task.WhenAll()`
11. **Error Handling**: Exceptions caught per handler, don't affect others
12. **Semaphore Released**: Next event can be processed
13. **Logged**: Comprehensive logging at each step

**Note:** In General mode, scheduled events are processed immediately without respecting the scheduled time.

### Request-Response Pattern (InvokeAsync)

For scenarios requiring a response from event handlers:

```csharp
TResult result = await eventBus.InvokeAsync<TResult>(event);
```

This method:
- Bypasses the channel (even in Channel mode)
- Directly invokes the processor synchronously
- Returns the result from the first applicable handler
- Used for queries or synchronous operations

### Memory Management

- **Bounded Channels**: Fixed memory footprint based on `ChannelCapacity`
- **Unbounded Channels**: Dynamic memory allocation, grows with event volume
- **Event Lifetime**: Events are garbage collected after processing
- **Handler Scoping**: Handlers created per scope, disposed automatically

### Thread Safety

All implementations are thread-safe:
- Channel writers/readers are thread-safe by design
- Semaphore handles concurrent access
- Handler resolution uses scoped DI containers
- No shared mutable state in event bus implementations

## 📚 API Reference

### Core Interfaces

#### IEvent

The base marker interface for all events in the system.

```csharp
public interface IEvent;
```

**Note:** `IEvent` is a marker interface with no properties. Events can define their own properties as needed.

#### Creating Events

Events are created by implementing the `IEvent` interface:

```csharp
public class UserRegisteredEvent : IEvent
{
    public string UserId { get; set; }
    public string Email { get; set; }
}
```

**Note:** Since `IEvent` is a marker interface, you have full control over the properties and structure of your events. You can add an `Id` property if your use case requires it:

```csharp
public class UserRegisteredEvent : IEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string UserId { get; set; }
    public string Email { get; set; }
}
```

#### IEventBus

The main interface for publishing and invoking events.

```csharp
public interface IEventBus
{
    // Fire-and-forget publish: enqueue background job to process subscribers
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    ValueTask BulkPublishAsync<TEvent>(List<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    // Wait for the response
    ValueTask<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default);
}
```

**Methods:**

- **PublishAsync<TEvent>(event, cancellationToken)**
  - Publishes a single event to all registered handlers for immediate processing
  - Returns immediately (Channel mode) or after processing (General mode)
  - **Parameters:**
    - `event`: The event to publish
    - `cancellationToken`: Optional cancellation token
  - **Returns:** `ValueTask` (completes when event is enqueued or processed)
  - **Use Cases:** Immediate event processing, real-time notifications, synchronous workflows

- **BulkPublishAsync<TEvent>(events, cancellationToken)**
  - Publishes multiple events of the same type for immediate processing
  - Processes sequentially to maintain order
  - **Parameters:**
    - `events`: IEnumerable of events to publish
    - `cancellationToken`: Optional cancellation token
  - **Returns:** `ValueTask` (completes when all events are enqueued or processed)
  - **Use Cases:** Batch processing, bulk operations, importing data

- **ScheduleAsync<TEvent>(event, scheduleTime, cancellationToken)**
  - Schedules a single event to be processed at a specific time in the future
  - Time is automatically converted to UTC for consistent timezone handling
  - Event is held in a scheduling channel until the scheduled time
  - **Parameters:**
    - `event`: The event to schedule
    - `scheduleTime`: The time when the event should be processed (DateTimeOffset, auto-converted to UTC)
    - `cancellationToken`: Optional cancellation token
  - **Returns:** `ValueTask` (completes when event is enqueued for scheduling)
  - **Use Cases:** Delayed notifications, scheduled reminders, time-based workflows, subscription renewals, deadline tracking
  - **Note (Channel mode):** Uses a dedicated scheduling channel that's processed by the background service
  - **Note (General mode):** Events are processed immediately (scheduling is not enforced)

- **BulkScheduleAsync<TEvent>(events, scheduleTime, cancellationToken)**
  - Schedules multiple events of the same type to be processed at a specific time
  - All events are scheduled for the same time
  - Time is automatically converted to UTC for consistent timezone handling
  - **Parameters:**
    - `events`: IEnumerable of events to schedule
    - `scheduleTime`: The time when all events should be processed (DateTimeOffset, auto-converted to UTC)
    - `cancellationToken`: Optional cancellation token
  - **Returns:** `ValueTask` (completes when all events are enqueued for scheduling)
  - **Use Cases:** Batch scheduled notifications, recurring reminders, scheduled bulk operations
  - **Note (Channel mode):** Uses a dedicated scheduling channel that's processed by the background service
  - **Note (General mode):** Events are processed immediately (scheduling is not enforced)

- **InvokeAsync<TResult>(event, cancellationToken)**
  - Synchronously invokes event handlers and waits for a result
  - Bypasses the channel queue (even in Channel mode)
  - Returns result from the event processor
  - **Parameters:**
    - `event`: The event to invoke (of type `object`)
    - `cancellationToken`: Optional cancellation token
  - **Returns:** `ValueTask<TResult>` with the result from handlers
  - **Use Cases:** Query operations, request-response patterns, synchronous workflows

#### IEventHandler

The base interface for all event handlers.

```csharp
public interface IEventHandler
{
    bool CanHandle(IEvent @event);
    Task HandleAsync(IEvent @event, CancellationToken cancellationToken = default);
}
```

**Methods:**
- `CanHandle(event)`: Determines if this handler can process the given event
- `HandleAsync(event, cancellationToken)`: Processes the event

#### IEventHandler<TEvent>

Generic interface for type-safe event handlers.

```csharp
public interface IEventHandler<in TEvent> : IEventHandler where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
    
    // Bridge implementations (auto-implemented)
    bool IEventHandler.CanHandle(IEvent @event) => @event is TEvent;
    Task IEventHandler.HandleAsync(IEvent @event, CancellationToken cancellationToken)
        => @event is TEvent typed ? HandleAsync(typed, cancellationToken) : Task.CompletedTask;
}
```

**Implementation Example:**
```csharp
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        // Handle the event
    }
}
```

#### IEventProcessor

Interface for processing events and coordinating handler execution.

```csharp
public interface IEventProcessor
{
    Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default);
    Task ProcessScheduledEventAsync(IEvent @event, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default);
    Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default);
    Task<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default);
}
```

**Methods:**

- **ProcessEventAsync(event, cancellationToken)**
  - Main entry point for immediate event processing
  - Coordinates the overall processing workflow
  - Typically calls `ProcessEventHandlersAsync` internally

- **ProcessScheduledEventAsync(event, scheduledTime, cancellationToken)**
  - Processes a scheduled event at the specified time
  - Waits until the scheduled time before processing the event
  - **Parameters:**
    - `event`: The event to process
    - `scheduledTime`: The UTC time when the event should be processed (DateTimeKind.Utc)
    - `cancellationToken`: Optional cancellation token
  - **Implementation Note:** Should check if `scheduledTime` is in the future and delay processing accordingly

- **ProcessEventHandlersAsync(event, cancellationToken)**
  - Resolves and executes all applicable handlers for the event
  - Handles errors per handler without affecting others
  - Executes handlers in parallel using `Task.WhenAll()`

- **InvokeAsync<TResult>(event, cancellationToken)**
  - Processes event and returns a result
  - Used for request-response patterns
  - Returns result from the first applicable handler

### Extension Methods

#### AddEventBus<TEventProcessor>

Registers the event bus and all event handlers in the DI container.

```csharp
public static IServiceCollection AddEventBus<TEventProcessor>(
    this IServiceCollection services, 
    IConfiguration configuration, 
    params List<Assembly> assemblies)
    where TEventProcessor : class, IEventProcessor
```

**Parameters:**
- `services`: The service collection
- `configuration`: Application configuration
- `assemblies`: Assemblies to scan for event handlers

**Returns:** The service collection for chaining

**Usage:**
```csharp
builder.Services.AddEventBus<DefaultEventProcessor>(
    builder.Configuration,
    [typeof(Program).Assembly, typeof(OrderHandler).Assembly]
);
```

#### GetEventBusSettings

Retrieves event bus configuration settings.

```csharp
public static EventBusSettings GetEventBusSettings(this IConfiguration configuration)
```

**Returns:** `EventBusSettings` instance populated from configuration

### Configuration Classes

#### EventBusSettings

Configuration class for event bus behavior.

```csharp
public class EventBusSettings
{
    public const string SectionName = "EventBusSettings";
    
    public int MaxConcurrency { get; set; } = 1000;
    public int ChannelCapacity { get; set; } = -1;
    public int EventProcessorCapacity { get; set; } = 10;
    public int ExecuteAfterSeconds { get; set; } = 2;
    public int RetryAfterSeconds { get; set; } = 5;
    public int RetryCount { get; set; } = 10;
    public int EachRetryInterval { get; set; } = 3;
    public int[] RetryIntervals { get; }
}
```

**Properties:**
- `MaxConcurrency`: Maximum concurrent operations (default: 1000)
- `ChannelCapacity`: Channel buffer size, -1 for unbounded (default: -1)
- `EventProcessorCapacity`: Max concurrent event processors (default: 10)
- `ExecuteAfterSeconds`: Initial execution delay (default: 2)
- `RetryAfterSeconds`: Delay before retry attempts (default: 5)
- `RetryCount`: Maximum retry attempts (default: 10)
- `EachRetryInterval`: Seconds between retries (default: 3)
- `RetryIntervals`: Array of retry intervals based on `RetryCount` and `EachRetryInterval`

## 🧩 Advanced Usage

### Custom Event Processor

Create sophisticated event processing logic:

```csharp
public class RetryableEventProcessor : IEventProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EventBusSettings _settings;
    private readonly ILogger<RetryableEventProcessor> _logger;
    
    public RetryableEventProcessor(
        IServiceProvider serviceProvider,
        EventBusSettings settings,
        ILogger<RetryableEventProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
        _logger = logger;
    }

    public async Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        var retryIntervals = _settings.RetryIntervals;
        
        for (int attempt = 0; attempt <= _settings.RetryCount; attempt++)
        {
            try
            {
                await ProcessEventHandlersAsync(@event, cancellationToken);
                return; // Success
            }
            catch (Exception ex) when (attempt < _settings.RetryCount)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} failed for event {EventType}. Retrying...", 
                    attempt + 1, @event.GetType().Name);
                    
                await Task.Delay(TimeSpan.FromSeconds(retryIntervals[attempt]), cancellationToken);
            }
        }
    }

    public async Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        // Implementation similar to DefaultEventProcessor
    }
}
```

### Event Handler with Dependencies

```csharp
public class ComplexOrderHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEmailService _emailService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<ComplexOrderHandler> _logger;

    public ComplexOrderHandler(
        IOrderRepository orderRepository,
        IEmailService emailService,
        IPaymentService paymentService,
        ILogger<ComplexOrderHandler> logger)
    {
        _orderRepository = orderRepository;
        _emailService = emailService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            // Complex business logic with multiple dependencies
            var order = await _orderRepository.GetByNumberAsync(@event.OrderNumber);

            await _paymentService.InitializePaymentAsync(order.PaymentInfo);
            await _emailService.SendOrderConfirmationAsync(order.CustomerId, order);

            _logger.LogInformation("Successfully processed order {OrderNumber}", @event.OrderNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {OrderNumber}", @event.OrderNumber);
            throw; // Re-throw to trigger retry logic
        }
    }
}
```

### Custom Scheduled Event Processor

Create a custom event processor that properly handles scheduled events:

```csharp
public class ScheduledEventProcessor : IEventProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledEventProcessor> _logger;

    public ScheduledEventProcessor(
        IServiceProvider serviceProvider,
        ILogger<ScheduledEventProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing immediate event {EventType}", @event.GetType().Name);
        await ProcessEventHandlersAsync(@event, cancellationToken);
    }

    public async Task ProcessScheduledEventAsync(
        IEvent @event, 
        DateTimeOffset scheduledTime, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing scheduled event {EventType} scheduled for {ScheduledTime}", 
            @event.GetType().Name, 
            scheduledTime);

        // Convert to UTC if not already
        var scheduledTimeUtc = scheduledTime.ToUniversalTime();
        var now = DateTimeOffset.UtcNow;

        // Calculate delay
        var delay = scheduledTimeUtc - now;

        if (delay > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Event {EventType} will be delayed by {Delay} until {ScheduledTime}",
                @event.GetType().Name,
                delay,
                scheduledTimeUtc);

            // Wait until scheduled time
            await Task.Delay(delay, cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Scheduled time {ScheduledTime} is in the past. Processing immediately.",
                scheduledTimeUtc);
        }

        // Process the event
        await ProcessEventHandlersAsync(@event, cancellationToken);
    }

    public async Task ProcessEventHandlersAsync(
        IEvent @event, 
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler>();

        var applicableHandlers = handlers.Where(h => h.CanHandle(@event)).ToList();

        if (!applicableHandlers.Any())
        {
            _logger.LogWarning("No handlers found for event type {EventType}", @event.GetType().Name);
            return;
        }

        var handlerTasks = applicableHandlers.Select(handler => 
            SafeHandleAsync(handler, @event, cancellationToken));

        await Task.WhenAll(handlerTasks);
    }

    private async Task SafeHandleAsync(
        IEventHandler handler, 
        IEvent @event, 
        CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Handler {HandlerType} failed to process event {EventType}", 
                handler.GetType().Name, 
                @event.GetType().Name);
        }
    }

    public Task<TResult> InvokeAsync<TResult>(
        object @event, 
        CancellationToken cancellationToken = default)
    {
        // Implementation for request-response pattern
        throw new NotImplementedException();
    }
}
```

## 📈 Monitoring and Diagnostics

The event bus provides comprehensive logging for monitoring:

```csharp
// Enable detailed logging in appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Softoverse.EventBus.InMemory": "Information"
    }
  }
}
```

## 🧪 Testing

### Unit Testing Event Handlers

```csharp
[Test]
public async Task OrderCreatedEventHandler_Should_ProcessOrder()
{
    // Arrange
    var logger = Mock.Of<ILogger<OrderCreatedEventHandler>>();
    var handler = new OrderCreatedEventHandler(logger);
    var orderEvent = new OrderCreatedEvent
    {
        OrderNumber = "ORDER-001",
        Amount = 100.00m,
        CustomerId = "CUST-001"
    };

    // Act
    await handler.HandleAsync(orderEvent);

    // Assert
    // Verify expected behavior
}
```

### Integration Testing

```csharp
[Test]
public async Task EventBus_Should_DeliverEvents_ToAllHandlers()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEventBus<DefaultEventProcessor>(configuration, [typeof(OrderCreatedEventHandler).Assembly]);
    
    var serviceProvider = services.BuildServiceProvider();
    var eventBus = serviceProvider.GetRequiredService<IEventBus>();
    
    // Act
    var orderEvent = new OrderCreatedEvent { OrderNumber = "ORDER-001" };
    await eventBus.PublishAsync(orderEvent);
    
    // Wait for processing (Channel mode)
    await Task.Delay(1000);
    
    // Assert
    // Verify handlers were called
}
```

## 💡 Best Practices

### Event Design

#### 1. Event Naming Conventions

Use past-tense verbs to indicate something has happened:

```csharp
✅ Good:
public class OrderCreatedEvent : IEvent { }
public class PaymentProcessedEvent : IEvent { }
public class UserRegisteredEvent : IEvent { }

❌ Avoid:
public class CreateOrderEvent : IEvent { }
public class ProcessPaymentEvent : IEvent { }
public class RegisterUserEvent : IEvent { }
```

#### 2. Event Immutability

Make events immutable after creation:

```csharp
public class OrderCreatedEvent : IEvent
{
    public string OrderNumber { get; init; }  // Use 'init' instead of 'set'
    public decimal Amount { get; init; }
    public DateTime CreatedAt { get; init; }

    public OrderCreatedEvent(string orderNumber, decimal amount)
    {
        OrderNumber = orderNumber;
        Amount = amount;
        CreatedAt = DateTime.UtcNow;
    }
}
```

#### 3. Keep Events Focused

Each event should represent a single business occurrence:

```csharp
✅ Good - Separate concerns:
public class OrderCreatedEvent : IEvent { }
public class PaymentInitiatedEvent : IEvent { }
public class InventoryReservedEvent : IEvent { }

❌ Avoid - Too broad:
public class OrderProcessedEvent : IEvent 
{
    public Payment Payment { get; set; }
    public Inventory Inventory { get; set; }
    public Shipping Shipping { get; set; }
}
```

#### 4. Include Relevant Context

Events should contain all information handlers need:

```csharp
public class OrderCreatedEvent : IEvent
{
    public string OrderNumber { get; init; }
    public string CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public List<OrderItem> Items { get; init; }
    public Address ShippingAddress { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

### Handler Design

#### 1. Keep Handlers Independent

Each handler should work independently without depending on execution order:

```csharp
✅ Good - Independent handlers:
public class EmailNotificationHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Self-contained email logic
        await _emailService.SendAsync(@event.CustomerId, "Order Confirmation");
    }
}

public class InventoryHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Self-contained inventory logic
        await _inventoryService.ReserveItems(@event.Items);
    }
}
```

#### 2. Handle Errors Gracefully

Always handle errors to prevent one handler from affecting others:

```csharp
public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
{
    try
    {
        await ProcessOrderAsync(@event);
    }
    catch (TransientException ex)
    {
        _logger.LogWarning(ex, "Transient error, will retry");
        throw; // Trigger retry logic
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Permanent error, logging and continuing");
        await LogErrorAsync(ex, @event);
        // Don't throw - allow other handlers to process
    }
}
```

#### 3. Implement Idempotency

Handlers should be safe to execute multiple times:

```csharp
public class PaymentProcessingHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Check if already processed
        if (await _paymentService.IsProcessedAsync(@event.OrderNumber))
        {
            _logger.LogInformation("Payment already processed for {OrderNumber}", @event.OrderNumber);
            return;
        }
        
        // Process payment
        await _paymentService.ProcessAsync(@event);
        
        // Mark as processed
        await _paymentService.MarkAsProcessedAsync(@event.OrderNumber);
    }
}
```

#### 4. Use Scoped Dependencies

Handlers are scoped, so inject scoped or transient services:

```csharp
public class OrderHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IOrderRepository _repository;  // Scoped
    private readonly IDbContext _dbContext;         // Scoped
    
    public OrderHandler(IOrderRepository repository, IDbContext dbContext)
    {
        _repository = repository;
        _dbContext = dbContext;
    }
}
```

### Event Processor Design

#### 1. Implement Retry Logic

```csharp
public class RobustEventProcessor : IEventProcessor
{
    public async Task ProcessEventAsync(IEvent @event, CancellationToken ct)
    {
        var retryCount = 0;
        var maxRetries = 3;
        
        while (retryCount <= maxRetries)
        {
            try
            {
                await ProcessEventHandlersAsync(@event, ct);
                return;
            }
            catch (Exception ex) when (retryCount < maxRetries && IsTransient(ex))
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                _logger.LogWarning("Retry {Retry}/{Max} after {Delay}s", retryCount, maxRetries, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }
    
    private bool IsTransient(Exception ex) => 
        ex is TimeoutException or HttpRequestException or DbUpdateException;
}
```

#### 2. Add Circuit Breaker Pattern

```csharp
public class CircuitBreakerEventProcessor : IEventProcessor
{
    private readonly Polly.CircuitBreakerPolicy _circuitBreaker;
    
    public CircuitBreakerEventProcessor()
    {
        _circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1)
            );
    }
    
    public async Task ProcessEventAsync(IEvent @event, CancellationToken ct)
    {
        await _circuitBreaker.ExecuteAsync(async () =>
        {
            await ProcessEventHandlersAsync(@event, ct);
        });
    }
}
```

### Configuration Best Practices

#### 1. Environment-Specific Settings

```json
// appsettings.Development.json
{
  "EventBusSettings": {
    "EventBusType": "General",  // Simpler debugging
    "EventProcessorCapacity": 1
  }
}

// appsettings.Production.json
{
  "EventBusSettings": {
    "EventBusType": "Channel",  // High performance
    "EventProcessorCapacity": 50,
    "ChannelCapacity": 10000
  }
}
```

#### 2. Tune for Your Workload

- **High-volume, bursty traffic**: Use unbounded channels (`ChannelCapacity: -1`)
- **Memory-constrained**: Use bounded channels with appropriate capacity
- **CPU-bound handlers**: Lower `EventProcessorCapacity` (e.g., CPU core count)
- **I/O-bound handlers**: Higher `EventProcessorCapacity` (e.g., 50-100)

#### 3. Monitor and Adjust

```csharp
public class MetricsEventProcessor : IEventProcessor
{
    private readonly IMetricsCollector _metrics;
    
    public async Task ProcessEventAsync(IEvent @event, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await ProcessEventHandlersAsync(@event, ct);
            _metrics.RecordSuccess(@event.GetType().Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _metrics.RecordFailure(@event.GetType().Name, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### Testing Best Practices

#### 1. Test Handlers in Isolation

```csharp
[Fact]
public async Task Handler_Should_ProcessEvent_Successfully()
{
    // Arrange
    var mockService = new Mock<IOrderService>();
    var handler = new OrderCreatedHandler(mockService.Object);
    var @event = new OrderCreatedEvent { OrderNumber = "123" };
    
    // Act
    await handler.HandleAsync(@event);
    
    // Assert
    mockService.Verify(s => s.ProcessOrderAsync("123"), Times.Once);
}
```

#### 2. Test Event Processor Behavior

```csharp
[Fact]
public async Task Processor_Should_CallAllHandlers()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<IEventHandler, Handler1>();
    services.AddScoped<IEventHandler, Handler2>();
    var processor = new DefaultEventProcessor(services.BuildServiceProvider());
    
    // Act
    await processor.ProcessEventAsync(new TestEvent());
    
    // Assert - verify both handlers called
}
```

#### 3. Integration Tests with TestHost

```csharp
[Fact]
public async Task EventBus_Integration_Test()
{
    var host = Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            services.AddEventBus<DefaultEventProcessor>(
                context.Configuration,
                [typeof(TestHandler).Assembly]
            );
        })
        .Build();
        
    await host.StartAsync();
    
    var eventBus = host.Services.GetRequiredService<IEventBus>();
    await eventBus.PublishAsync(new TestEvent());
    
    await Task.Delay(100); // Wait for processing
    // Assert expectations
}
```

## 🔧 Troubleshooting

### Common Issues

#### 1. Events Not Being Processed

**Symptom:** Events are published but handlers never execute.

**Possible Causes & Solutions:**

**a) Handler Not Registered**
```csharp
// ❌ Problem: Assembly not included in registration
builder.Services.AddEventBus<DefaultEventProcessor>(
    builder.Configuration,
    [] // Empty list!
);

// ✅ Solution: Include assemblies with handlers
builder.Services.AddEventBus<DefaultEventProcessor>(
    builder.Configuration,
    [typeof(Program).Assembly, typeof(MyHandler).Assembly]
);
```

**b) Handler Doesn't Implement IEventHandler<T>**
```csharp
// ❌ Problem: Missing interface implementation
public class MyHandler
{
    public Task HandleAsync(MyEvent @event) { }
}

// ✅ Solution: Implement IEventHandler<T>
public class MyHandler : IEventHandler<MyEvent>
{
    public async Task HandleAsync(MyEvent @event, CancellationToken ct) { }
}
```

**c) Channel Mode - Background Service Not Started**
```csharp
// ✅ Ensure app is running to start background services
var app = builder.Build();
await app.RunAsync(); // This starts both background services
```

#### 2. Null Event Warning

**Symptom:** Log shows `"Ignored null event of type {EventType}"`

**Cause:** Bug in null check logic (checks `@event != null!` which is inverted)

**Current Implementation:**
```csharp
if (@event != null!)  // This checks if event is NOT null (bug)
{
    _logger.LogWarning("Ignored null event");
    return;
}
```

**Workaround:** Always ensure events are not null before publishing:
```csharp
if (@event != null)
{
    await _eventBus.PublishAsync(@event);
}
```

#### 3. Handlers Throwing Exceptions

**Symptom:** One handler's exception prevents other handlers from executing.

**Solution:** Ensure processor catches exceptions per handler:

```csharp
public async Task ProcessEventHandlersAsync(IEvent @event, CancellationToken ct)
{
    var handlers = GetHandlers(@event);
    
    // ✅ Good: Each handler wrapped in try-catch
    var tasks = handlers.Select(h => SafeHandleAsync(h, @event, ct));
    await Task.WhenAll(tasks);
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
        // Don't rethrow - let other handlers run
    }
}
```

#### 4. Channel Capacity Exceeded

**Symptom:** Application hangs when publishing events.

**Cause:** Bounded channel is full and using `FullMode.Wait`.

**Solutions:**

**a) Increase Channel Capacity**
```json
{
  "EventBusSettings": {
    "ChannelCapacity": 50000  // Increase from default
  }
}
```

**b) Use Unbounded Channel**
```json
{
  "EventBusSettings": {
    "ChannelCapacity": -1  // Unbounded
  }
}
```

**c) Increase Processor Capacity**
```json
{
  "EventBusSettings": {
    "EventProcessorCapacity": 50  // More concurrent processors
  }
}
```

#### 5. Memory Issues with Unbounded Channel

**Symptom:** High memory usage, possible OutOfMemoryException.

**Cause:** Unbounded channel accumulating events faster than they're processed.

**Solutions:**

**a) Switch to Bounded Channel**
```json
{
  "EventBusSettings": {
    "ChannelCapacity": 10000
  }
}
```

**b) Increase Processing Speed**
- Increase `EventProcessorCapacity`
- Optimize handler performance
- Add more application instances

#### 6. Handlers Not Receiving Events

**Symptom:** Handler is registered but `CanHandle()` returns false.

**Cause:** Type mismatch between published event and handler generic type.

```csharp
// ❌ Problem: Type mismatch
await _eventBus.PublishAsync(new OrderCreated());  // Different type
public class Handler : IEventHandler<OrderCreatedEvent> { }

// ✅ Solution: Use exact same type
await _eventBus.PublishAsync(new OrderCreatedEvent());
public class Handler : IEventHandler<OrderCreatedEvent> { }
```

#### 7. Slow Event Processing

**Symptom:** Events take too long to process.

**Diagnosis:**
```csharp
public class DiagnosticEventProcessor : IEventProcessor
{
    public async Task ProcessEventAsync(IEvent @event, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        await ProcessEventHandlersAsync(@event, ct);
        _logger.LogInformation("Event {Type} processed in {Ms}ms", 
            @event.GetType().Name, sw.ElapsedMilliseconds);
    }
}
```

**Solutions:**
- Profile handler code to find bottlenecks
- Use async I/O operations, not blocking calls
- Consider breaking into smaller events
- Increase `EventProcessorCapacity` for I/O-bound work

#### 8. Dependency Injection Issues

**Symptom:** `Unable to resolve service` exception in handlers.

**Causes & Solutions:**

**a) Service Not Registered**
```csharp
// ❌ Problem: Service not registered
public class MyHandler : IEventHandler<MyEvent>
{
    public MyHandler(IMyService service) { }  // Service not in DI
}

// ✅ Solution: Register dependencies
builder.Services.AddScoped<IMyService, MyService>();
```

**b) Singleton Depending on Scoped**
```csharp
// Problem: Singleton can't depend on scoped
public class EventPublisherHostedService : BackgroundService
{
    // IEventBus is scoped, so this is invalid
    public EventPublisherHostedService(IEventBus eventBus) { }
}

// Solution: Use IServiceScopeFactory and create scopes
public class EventPublisherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new MyEvent(), stoppingToken);
    }
}
```

#### 9. Configuration Not Loading

**Symptom:** Settings always use default values.

**Check:**

```csharp
// 1. Verify appsettings.json section name
{
  "EventBusSettings": {  // Must match EventBusSettings.SectionName
    "ChannelCapacity": 5000
  }
}

// 2. Verify configuration is passed
builder.Services.AddEventBus<DefaultEventProcessor>(
    builder.Configuration,  // Make sure this is provided
    [typeof(Program).Assembly]
);

// 3. Check configuration provider order
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
```

### Debugging Tips

#### 1. Enable Detailed Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Softoverse.EventBus.InMemory": "Debug",
      "Softoverse.EventBus.InMemory.Infrastructure.Channels": "Trace"
    }
  }
}
```

#### 2. Add Diagnostic Middleware

```csharp
public class DiagnosticEventProcessor : IEventProcessor
{
    private readonly IEventProcessor _innerProcessor;
    private readonly ILogger _logger;

    public async Task ProcessEventAsync(IEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("📨 Event received: {Type}", 
            @event.GetType().Name);
            
        var sw = Stopwatch.StartNew();
        try
        {
            await _innerProcessor.ProcessEventAsync(@event, ct);
            _logger.LogInformation("✅ Event processed in {Ms}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Event processing failed after {Ms}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }
}
```

#### 3. Test with General Mode First

When debugging, switch to General mode for simpler synchronous execution:

```json
{
  "EventBusSettings": {
    "EventBusType": "General"  // Easier to debug
  }
}
```

### FAQ

**Q: Can I use this library across multiple processes?**  
A: No, this is an in-memory implementation for single-process use. For distributed scenarios, consider message brokers like RabbitMQ, Azure Service Bus, or Apache Kafka.

**Q: Are events guaranteed to be processed in order?**  
A: In Channel mode with `SingleReader = false`, order is not guaranteed due to parallel processing. For ordered processing, use General mode or implement ordering in your handlers.

**Q: Can I have multiple event buses in one application?**  
A: Yes, but you'll need to register them manually with different lifetimes or use named registrations.

**Q: How do I handle long-running operations in handlers?**  
A: Consider:
- Increase `EventProcessorCapacity` for I/O-bound operations
- Use background jobs (Hangfire, Quartz) for truly long-running work
- Break into smaller events and use saga patterns

**Q: Can handlers publish new events?**  
A: Yes! Just inject `IEventBus` into your handler and publish:
```csharp
public class OrderHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IEventBus _eventBus;

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Process order...
        await _eventBus.PublishAsync(new OrderProcessedEvent());
    }
}
```

**Q: How does event scheduling work in Channel mode vs General mode?**  
A: 
- **Channel Mode**: Events are written to a dedicated `SchedulingChannel` with the scheduled time. A background service continuously reads from this channel and processes events via `ProcessScheduledEventAsync`, which delays execution until the scheduled time.
- **General Mode**: Scheduled events are processed immediately without respecting the scheduled time. If you need true scheduling, use Channel mode.

**Q: What happens to scheduled events if the application restarts?**  
A: Scheduled events in the channel are lost on restart. For persistent scheduling, consider:
- Using a persistent message broker (RabbitMQ, Azure Service Bus)
- Implementing a durable job scheduler (Hangfire, Quartz.NET)
- Storing scheduled events in a database with a polling mechanism

**Q: Can I schedule events with different times in bulk?**  
A: `BulkScheduleAsync` schedules all events for the same time. For different times, call `ScheduleAsync` multiple times:
```csharp
await _eventBus.ScheduleAsync(event1, time1);
await _eventBus.ScheduleAsync(event2, time2);
await _eventBus.ScheduleAsync(event3, time3);
```

**Q: How accurate is the scheduled event timing?**  
A: Timing accuracy depends on:
- System load and available resources
- `EventProcessorCapacity` setting (higher = more concurrent processing)
- Handler execution time
- Channel capacity and processing queue depth

Typical accuracy is within seconds for normal workloads. For millisecond precision, consider specialized timing libraries.

**Q: What happens if the application crashes?**  
A: Events in both channels (publishing and scheduling) are lost. For durability, consider:
- Persisting events before publishing
- Using durable message brokers
- Implementing outbox pattern
- Using persistent job schedulers for scheduled events

## 📊 Performance Characteristics

- **Throughput**: Handles thousands of events per second in Channel mode
- **Memory Usage**: Efficient memory management with bounded channels
- **Latency**: Sub-millisecond event publishing, configurable processing delays
- **Scalability**: Horizontal scaling through concurrency controls

### Benchmark Results

Based on typical workloads:

| Scenario | Events/sec | Latency (p50) | Latency (p99) | Memory |
|----------|-----------|---------------|---------------|--------|
| Channel - Unbounded | 50,000+ | <1ms publish | <5ms | ~100MB for 10K events |
| Channel - Bounded (10K) | 40,000+ | <1ms publish | <10ms | Fixed ~50MB |
| General - Sync | 5,000+ | 2-5ms | 20-50ms | Minimal |

**Factors affecting performance:**
- Handler complexity and execution time
- Number of concurrent processors (`EventProcessorCapacity`)
- Channel configuration (bounded vs unbounded)
- Number of handlers per event
- Hardware (CPU cores, memory)

## 🔄 Migration Guide

### Migrating from Other Event Bus Libraries

#### From MediatR

**MediatR** is focused on in-process messaging with CQRS patterns, while **Softoverse.EventBus** is optimized for event-driven architectures.

**Key Differences:**

| Feature | MediatR | Softoverse.EventBus |
|---------|---------|---------------------|
| Processing | Synchronous pipeline | Async channel-based or direct |
| Multiple Handlers | Sequential via Pipeline | Parallel execution |
| Background Processing | Manual (BackgroundService) | Built-in (Channel mode) |
| Notifications | `INotification` / `INotificationHandler` | `IEvent` / `IEventHandler<T>` |

**Migration Steps:**

1. **Replace Notification Interfaces:**

```csharp
// MediatR
public class OrderCreated : INotification
{
    public string OrderId { get; set; }
}

public class Handler : INotificationHandler<OrderCreated>
{
    public async Task Handle(OrderCreated notification, CancellationToken ct)
    {
        // Handle
    }
}

// Softoverse.EventBus
public class OrderCreatedEvent : EventBase
{
    public string OrderId { get; init; }
}

public class Handler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Handle
    }
}
```

2. **Replace Publisher:**

```csharp
// MediatR
await _mediator.Publish(new OrderCreated { OrderId = "123" });

// Softoverse.EventBus
await _eventBus.PublishAsync(new OrderCreatedEvent { OrderId = "123" });
```

3. **Update DI Registration:**

```csharp
// MediatR
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Softoverse.EventBus
services.AddEventBus<DefaultEventProcessor>(
    configuration,
    [typeof(Program).Assembly]
);
```

#### From MassTransit (In-Memory)

**MassTransit** is a distributed application framework, while **Softoverse.EventBus** is simpler and focused on in-memory scenarios.

**Migration Steps:**

1. **Simplify Event Definitions:**

```csharp
// MassTransit
public class OrderCreated
{
    public Guid CorrelationId { get; set; }
    public string OrderId { get; set; }
}

// Softoverse.EventBus
public class OrderCreatedEvent : EventBase
{
    public string OrderId { get; init; }
    // Id property inherited from EventBase
}
```

2. **Replace Consumers with Handlers:**

```csharp
// MassTransit
public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        // Handle
    }
}

// Softoverse.EventBus
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Handle
    }
}
```

3. **Simplify Configuration:**

```csharp
// MassTransit
services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

// Softoverse.EventBus
services.AddEventBus<DefaultEventProcessor>(
    configuration,
    [typeof(Program).Assembly]
);
```

#### From Custom Event Aggregator

If you have a custom event aggregator pattern:

**Before:**
```csharp
public interface IEventAggregator
{
    void Subscribe<T>(Action<T> handler);
    void Publish<T>(T @event);
}
```

**After:**
```csharp
// Define events
public class MyEvent : EventBase
{
    public string Data { get; init; }
}

// Create handler
public class MyEventHandler : IEventHandler<MyEvent>
{
    public async Task HandleAsync(MyEvent @event, CancellationToken ct)
    {
        // Handle event
    }
}

// Publish
await _eventBus.PublishAsync(new MyEvent { Data = "test" });
```

### Upgrading Between Versions

#### From 1.x to 10.x

**Breaking Changes:**
- Target framework changed from .NET 9 to .NET 10
- Updated dependencies to Microsoft.Extensions.* version 10.0.2

**Migration Steps:**

1. **Update Target Framework:**

```xml
<!-- Before -->
<TargetFramework>net9.0</TargetFramework>

<!-- After -->
<TargetFramework>net10.0</TargetFramework>
```

2. **Update Package Reference:**

```xml
<PackageReference Include="Softoverse.EventBus.InMemory" Version="10.0.0" />
```

3. **Test Your Application:**
   - No API changes required
   - Configuration remains compatible
   - Test all event handlers

### Version Compatibility

| EventBus Version | .NET Version | Status |
|-----------------|--------------|--------|
| 10.x | .NET 10+ | Current |
| 1.x | .NET 9+ | Legacy |

### Feature Comparison

| Feature | v1.x | v10.x |
|---------|------|-------|
| Channel Processing | ✅ | ✅ |
| General Processing | ✅ | ✅ |
| InvokeAsync | ✅ | ✅ |
| BulkPublishAsync | ✅ | ✅ |
| Configurable Settings | ✅ | ✅ |
| .NET 10 Support | ❌ | ✅ |

## 💡 Use Cases

### Domain Events in DDD

```csharp
// Aggregate publishes domain events
public class Order : AggregateRoot
{
    public void Create(string customerId, List<OrderItem> items)
    {
        // Business logic
        var @event = new OrderCreatedEvent
        {
            OrderId = Id,
            CustomerId = customerId,
            Items = items
        };
        
        AddDomainEvent(@event);
    }
}

// Event handlers for side effects
public class EmailNotificationHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _emailService.SendOrderConfirmation(@event.CustomerId);
    }
}
```

### CQRS Command Side Effects

```csharp
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand>
{
    private readonly IEventBus _eventBus;
    
    public async Task Handle(CreateOrderCommand command, CancellationToken ct)
    {
        // Execute command
        var order = new Order(command.CustomerId, command.Items);
        await _repository.SaveAsync(order);
        
        // Publish event
        await _eventBus.PublishAsync(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId
        });
    }
}
```

### Microservices Communication (In-Process)

```csharp
// Order Service
await _eventBus.PublishAsync(new OrderCreatedEvent { OrderId = orderId });

// Inventory Service Handler (same process)
public class InventoryHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _inventoryService.ReserveItems(@event.Items);
    }
}

// Shipping Service Handler (same process)
public class ShippingHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        await _shippingService.CreateShipment(@event.OrderId);
    }
}
```

### Background Job Coordination

```csharp
// Trigger background processing
await _eventBus.PublishAsync(new DataImportRequestedEvent
{
    FileUrl = fileUrl,
    BatchSize = 1000
});

// Handler processes asynchronously
public class DataImportHandler : IEventHandler<DataImportRequestedEvent>
{
    public async Task HandleAsync(DataImportRequestedEvent @event, CancellationToken ct)
    {
        await _importService.ImportFromUrlAsync(@event.FileUrl, @event.BatchSize);

        // Publish completion event
        await _eventBus.PublishAsync(new DataImportCompletedEvent
        {
            FileUrl = @event.FileUrl,
            RecordsProcessed = result.Count
        });
    }
}
```

### Scheduled Reminders and Notifications

```csharp
public class SubscriptionService
{
    private readonly IEventBus _eventBus;

    public async Task CreateSubscriptionAsync(Subscription subscription)
    {
        // Save subscription...

        // Schedule reminder emails before expiration
        var sevenDaysBeforeExpiry = subscription.ExpiryDate.AddDays(-7);
        await _eventBus.ScheduleAsync(
            new SubscriptionExpiryReminderEvent
            {
                SubscriptionId = subscription.Id,
                CustomerId = subscription.CustomerId,
                DaysRemaining = 7
            },
            sevenDaysBeforeExpiry
        );

        var oneDayBeforeExpiry = subscription.ExpiryDate.AddDays(-1);
        await _eventBus.ScheduleAsync(
            new SubscriptionExpiryReminderEvent
            {
                SubscriptionId = subscription.Id,
                CustomerId = subscription.CustomerId,
                DaysRemaining = 1
            },
            oneDayBeforeExpiry
        );
    }
}

public class SubscriptionReminderHandler : IEventHandler<SubscriptionExpiryReminderEvent>
{
    private readonly IEmailService _emailService;
    private readonly ISubscriptionRepository _repository;

    public async Task HandleAsync(
        SubscriptionExpiryReminderEvent @event, 
        CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(@event.SubscriptionId);

        if (subscription.IsActive)
        {
            await _emailService.SendReminderAsync(
                @event.CustomerId,
                $"Your subscription expires in {@event.DaysRemaining} days"
            );
        }
    }
}
```

### Trial Period and Grace Period Management

```csharp
public class TrialService
{
    private readonly IEventBus _eventBus;

    public async Task StartTrialAsync(User user)
    {
        var trial = new Trial
        {
            UserId = user.Id,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddDays(14)
        };

        // Schedule trial ending events
        await _eventBus.ScheduleAsync(
            new TrialEndingEvent { UserId = user.Id, TrialId = trial.Id },
            trial.EndDate
        );

        // Schedule warning at day 10
        await _eventBus.ScheduleAsync(
            new TrialEndingWarningEvent { UserId = user.Id, DaysRemaining = 4 },
            trial.EndDate.AddDays(-4)
        );
    }
}
```

### Delayed Task Execution

```csharp
public class OrderService
{
    private readonly IEventBus _eventBus;

    public async Task CreateOrderAsync(Order order)
    {
        // Save order...

        // Schedule automatic cancellation if not paid within 30 minutes
        var cancellationTime = DateTimeOffset.UtcNow.AddMinutes(30);
        await _eventBus.ScheduleAsync(
            new OrderAutoCancellationCheckEvent { OrderId = order.Id },
            cancellationTime
        );
    }
}

public class OrderAutoCancellationHandler : IEventHandler<OrderAutoCancellationCheckEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventBus _eventBus;

    public async Task HandleAsync(
        OrderAutoCancellationCheckEvent @event, 
        CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(@event.OrderId);

        if (order.Status == OrderStatus.Pending)
        {
            order.Cancel("Payment timeout");
            await _orderRepository.UpdateAsync(order);

            await _eventBus.PublishAsync(new OrderCancelledEvent
            {
                OrderId = order.Id,
                Reason = "Payment timeout"
            });
        }
    }
}
```

### Recurring Scheduled Tasks

```csharp
public class ReportingService
{
    private readonly IEventBus _eventBus;

    public async Task ScheduleDailyReportAsync()
    {
        // Schedule next day's report at 8 AM
        var tomorrow8AM = DateTimeOffset.UtcNow.Date.AddDays(1).AddHours(8);

        await _eventBus.ScheduleAsync(
            new GenerateDailyReportEvent { ReportDate = tomorrow8AM.Date },
            tomorrow8AM
        );
    }
}

public class DailyReportHandler : IEventHandler<GenerateDailyReportEvent>
{
    private readonly IReportGenerator _reportGenerator;
    private readonly IEventBus _eventBus;

    public async Task HandleAsync(
        GenerateDailyReportEvent @event, 
        CancellationToken ct)
    {
        // Generate the report
        await _reportGenerator.GenerateDailyReportAsync(@event.ReportDate);

        // Schedule next day's report
        var nextReportTime = @event.ReportDate.AddDays(1).AddHours(8);
        await _eventBus.ScheduleAsync(
            new GenerateDailyReportEvent { ReportDate = nextReportTime.Date },
            nextReportTime
        );
    }
}
```

### Event Sourcing (Simple)

```csharp
public class EventStore
{
    private readonly IEventBus _eventBus;
    private readonly List<IEvent> _events = new();

    public async Task AppendAsync(IEvent @event)
    {
        _events.Add(@event);
        await _eventBus.PublishAsync(@event);
    }

    public IEnumerable<IEvent> GetEvents()
    {
        return _events;
    }
}
```

## 🔗 Related Resources

### Official Documentation
- [.NET Channels Guide](https://docs.microsoft.com/en-us/dotnet/core/extensions/channels)
- [Dependency Injection in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Background Services](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)

### Design Patterns
- [Domain Events Pattern](https://martinfowler.com/eaaDev/DomainEvent.html)
- [Publish-Subscribe Pattern](https://en.wikipedia.org/wiki/Publish%E2%80%93subscribe_pattern)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html)

### Alternative Libraries
- **MediatR**: For CQRS and request/response patterns
- **MassTransit**: For distributed messaging
- **NServiceBus**: For enterprise messaging
- **Rebus**: For service bus implementations

## 👥 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

### How to Contribute

1. **Fork the Repository**
   ```bash
   git clone https://github.com/mahmudabir/EventBus.git
   cd EventBus
   ```

2. **Create a Feature Branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make Your Changes**
   - Follow existing code style and conventions
   - Add tests for new features
   - Update documentation as needed
   - Ensure all tests pass

4. **Commit Your Changes**
   ```bash
   git commit -m "Add: Brief description of your changes"
   ```

5. **Push and Create Pull Request**
   ```bash
   git push origin feature/your-feature-name
   ```

### Contribution Guidelines

- **Code Style**: Follow C# coding conventions and use meaningful names
- **Testing**: Add unit tests for new features and bug fixes
- **Documentation**: Update README.md and XML documentation comments
- **Commit Messages**: Use clear, descriptive commit messages
- **Pull Requests**: Provide a clear description of changes and reasoning

### Areas for Contribution

- 🐛 Bug fixes and issue resolutions
- ✨ New features and enhancements
- 📚 Documentation improvements
- 🧪 Additional test coverage
- ⚡ Performance optimizations
- 🌐 Examples and tutorials

### Development Setup

```bash
# Clone the repository
git clone https://github.com/mahmudabir/EventBus.git
cd EventBus

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run tests (if available)
dotnet test

# Create NuGet package
dotnet pack -c Release
```

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

### Apache License 2.0 Summary

✅ **Permissions:**
- Commercial use
- Modification
- Distribution
- Patent use
- Private use

⚠️ **Conditions:**
- License and copyright notice
- State changes

❌ **Limitations:**
- Liability
- Warranty

For the full license text, see [LICENSE](LICENSE) or visit https://www.apache.org/licenses/LICENSE-2.0

## 🔗 Links

### Package & Repository
- [📦 NuGet Package](https://www.nuget.org/packages/Softoverse.EventBus.InMemory/)
- [💻 GitHub Repository](https://github.com/mahmudabir/EventBus)
- [🐛 Issue Tracker](https://github.com/mahmudabir/EventBus/issues)
- [📋 Pull Requests](https://github.com/mahmudabir/EventBus/pulls)

### Documentation
- [Quick Start Guide](#-quick-start)
- [API Reference](#-api-reference)
- [Best Practices](#-best-practices)
- [Troubleshooting](#-troubleshooting)

### Community
- [Discussions](https://github.com/mahmudabir/EventBus/discussions)
- [Report a Bug](https://github.com/mahmudabir/EventBus/issues/new?template=bug_report.md)
- [Request a Feature](https://github.com/mahmudabir/EventBus/issues/new?template=feature_request.md)

## 📝 Changelog

### Version 10.1.0 (Current)

**🎯 Feature Release - Event Scheduling Support**

**New Features:**
- ⏰ **Event Scheduling**: Schedule events to be processed at a specific time in the future
  - `ScheduleAsync<TEvent>(event, scheduleTime, cancellationToken)` - Schedule a single event
  - `BulkScheduleAsync<TEvent>(events, scheduleTime, cancellationToken)` - Schedule multiple events
- 📋 **Dual-Channel Architecture**: Separate channels for immediate and scheduled events
  - `PublishingChannel` - For immediate event processing
  - `SchedulingChannel` - For scheduled event processing
- 🔄 **Parallel Processing**: Background service processes both channels concurrently
- 🕐 **Automatic UTC Conversion**: Scheduled times are automatically converted to UTC
- 🎯 **IEventProcessor Enhancement**: New `ProcessScheduledEventAsync` method for handling scheduled events

**Use Cases:**
- ⏱️ Delayed notifications and reminders
- 📅 Trial period and subscription management
- ⚠️ Time-based alerts and warnings
- 🔁 Recurring scheduled tasks
- ⏳ Deadline tracking and auto-cancellation
- 📊 Scheduled report generation

**Architecture Updates:**
- Separate dedicated background services: `ChannelEventsPublishingHostedService` and `ChannelEventsSchedulingHostedService`
- Independent semaphores for concurrency control in each service
- Enhanced `EventChannelProvider` with scheduling channel support

**Documentation:**
- 📚 Complete API reference for scheduling methods
- 💡 Comprehensive usage examples and best practices
- 🧩 Advanced custom processor examples
- ❓ FAQ section covering scheduling behavior
- 🔧 Troubleshooting guide updated

**Notes:**
- Fully backward compatible with existing code
- Channel mode required for true scheduling (General mode processes immediately)
- Scheduled events are not persisted (lost on application restart)

**Usage Example:**
```csharp
// Schedule a reminder 7 days before subscription expiry
var reminderDate = subscription.ExpiryDate.AddDays(-7);
await _eventBus.ScheduleAsync(
    new SubscriptionExpiryReminderEvent 
    { 
        SubscriptionId = subscription.Id,
        DaysRemaining = 7
    },
    reminderDate
);
```

---

### Version 10.0.0 (2026-01-24)

**🎯 Major Release - .NET 10 Support**

**Breaking Changes:**
- ⬆️ Target framework upgraded from .NET 9.0 to .NET 10.0
- 📦 Updated Microsoft.Extensions.* dependencies to version 10.0.2
  - `Microsoft.Extensions.Configuration.Binder` → 10.0.2
  - `Microsoft.Extensions.Hosting.Abstractions` → 10.0.2
  - `Microsoft.Extensions.Logging.Abstractions` → 10.0.2

**Improvements:**
- 🚀 Enhanced performance with .NET 10 optimizations
- 📚 Comprehensive documentation with architecture details
- 🔧 Improved troubleshooting guide with common issues and solutions
- 💡 Added best practices and design patterns section
- 🧪 Enhanced testing examples
- 🔄 Added migration guide from other event bus libraries
- 📊 Performance benchmarks and characteristics documented

**Notes:**
- No API changes - fully compatible with existing code after framework upgrade
- Configuration remains backward compatible
- All features from v1.x are preserved

**Migration:**
```xml
<!-- Update your project file -->
<TargetFramework>net10.0</TargetFramework>
<PackageReference Include="Softoverse.EventBus.InMemory" Version="10.0.0" />
```

---

### Version 1.0.0 (Initial Release)

**🎉 First Public Release**

**Core Features:**
- ✅ In-memory event bus implementation
- 🔄 Two processing strategies:
  - Channel-based (high-performance, async)
  - General (simple, synchronous)
- 📡 Publish-subscribe pattern support
- 🎯 Type-safe event handling with `IEventHandler<TEvent>`
- 🔧 Comprehensive dependency injection integration
- ⚙️ Configurable settings via `appsettings.json`
- 📝 Extensive logging support
- 🔁 Retry policy configuration
- 📦 Bulk event publishing support
- 🔀 Request-response pattern with `InvokeAsync<TResult>`

**Components:**
- `IEvent` / `EventBase` - Event contracts
- `IEventBus` - Event publishing interface
- `IEventHandler<T>` - Type-safe event handlers
- `IEventProcessor` - Event processing coordination
- `ChannelEventBus` - Channel-based implementation
- `GeneralEventBus` - Direct processing implementation
- `ChannelEventsPublishingHostedService` - Background service for immediate events
- `ChannelEventsSchedulingHostedService` - Background service for scheduled events
- `EventBusSettings` - Configuration model

**Configuration Options:**
- `EventBusType` - Choose processing strategy
- `MaxConcurrency` - Concurrent operation limits
- `ChannelCapacity` - Buffer size configuration
- `EventProcessorCapacity` - Concurrent processor limits
- `RetryCount` / `RetryAfterSeconds` - Retry policy settings

**Target Framework:**
- .NET 9.0

---

## 🙏 Acknowledgments

Special thanks to:
- The .NET team for the excellent `System.Threading.Channels` library
- The open-source community for inspiration and feedback
- All contributors who help improve this project

---

## 📞 Support

### Getting Help

- 📖 **Documentation**: Start with this README and the [Quick Start](#-quick-start) guide
- 🐛 **Bug Reports**: [Create an issue](https://github.com/mahmudabir/EventBus/issues/new?template=bug_report.md)
- 💡 **Feature Requests**: [Request a feature](https://github.com/mahmudabir/EventBus/issues/new?template=feature_request.md)
- 💬 **Questions**: [GitHub Discussions](https://github.com/mahmudabir/EventBus/discussions)

### Commercial Support

For enterprise support, training, or consulting services, please contact the maintainers through GitHub.

---

## ⭐ Show Your Support

If you find this project useful, please consider:
- ⭐ Starring the repository
- 🐛 Reporting bugs
- 💡 Suggesting new features
- 📝 Improving documentation
- 🔀 Contributing code

---

<div align="center">

**[⬆ Back to Top](#softoverseeventbusinmemory)**

Made with ❤️ by [mahmudabir](https://github.com/mahmudabir)

</div>
