﻿# Architecture Documentation

> **For Contributors**: This document describes the internal architecture, design decisions, and implementation details of the Softoverse.EventBus.InMemory library. If you're a **user** looking to integrate this library into your application, please refer to [README.md](../README.md) for usage guides and examples.

## Overview

Softoverse.EventBus.InMemory is designed as a lightweight, high-performance in-memory event bus for .NET applications. This document provides a deep dive into:

- **Core architecture and component interactions**
- **Design decisions and trade-offs**
- **Implementation details and patterns**
- **Extension points for customization**
- **Performance characteristics and optimizations**
- **Guidelines for contributors**

This documentation is intended for:
- Contributors who want to understand the codebase
- Developers who need to extend or customize the library
- Architects evaluating the library for their projects

## Core Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Application Layer                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐           │
│  │   Service    │  │   Service    │  │   Service    │           │
│  │      A       │  │      B       │  │      C       │           │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘           │
│         │                 │                 │                   │
│         └─────────────────┼─────────────────┘                   │
│                           │                                     │
└───────────────────────────┼─────────────────────────────────────┘
                            ▼
              ┌──────────────────────────────┐
              │         IEventBus            │
              │    (Abstraction Layer)       │
              └──────────────┬───────────────┘
                             │
                    ┌────────┴────────┐
                    │                 │
         ┌──────────▼─────────┐  ┌────▼─────────────┐
         │  ChannelEventBus   │  │ GeneralEventBus  │
         │  (Async/Channel)   │  │  (Sync/Direct)   │
         └──────────┬─────────┘  └───┬──────────────┘
                    │                │
                    ▼                ▼
         ┌──────────────────────────────────┐
         │       IEventProcessor            │
         │  (Processing Coordination)       │
         └──────────┬───────────────────────┘
                    │
                    ▼
         ┌──────────────────────────────────┐
         │      IEventHandler<TEvent>       │
         │  (Handler Implementations)       │
         └──────────────────────────────────┘
```

## Key Components

### 1. IEvent & EventBase

**Purpose**: Define event contracts and provide base implementation.

**Design Decisions**:
- Use `Guid.CreateVersion7()` for event IDs (time-ordered, better for indexing)
- Abstract base class provides convenience while interface allows flexibility
- Immutability recommended but not enforced (allows for flexibility)

```csharp
public interface IEvent
{
    Guid Id { get; set; }
}

public abstract class EventBase : IEvent
{
    public Guid Id { get; set; }
    
    protected EventBase(Guid? id = null)
    {
        Id = id ?? Guid.CreateVersion7();
    }
}
```

### 2. IEventBus

**Purpose**: Main entry point for publishing events.

**Design Decisions**:
- `ValueTask` return type for better performance (avoids allocation in synchronous paths)
- Separate methods for single/bulk publishing (optimization opportunity)
- `InvokeAsync<TResult>` for request-response pattern (separate from fire-and-forget)

**Implementations**:

#### ChannelEventBus (Recommended)

Uses `System.Threading.Channels` for high-throughput async processing.

**Flow**:
```
Publisher
   │
   ▼
PublishAsync() ────► Channel.Writer.WriteAsync()
   │                      │
   │                      ▼
   │              [Bounded/Unbounded Channel]
   │                      │
   │              ┌───────┴────────┐
   │              ▼                ▼
   │   ChannelEventsPublishingHostedService  ChannelEventsSchedulingHostedService
   │              │                          │
   │              ▼                          ▼
   │   Channel.Reader.ReadAsync()  Channel.Reader.ReadAsync()
   │              │                          │
   │              ▼                          ▼
   │   Semaphore.WaitAsync()       Semaphore.WaitAsync()
   │              │                          │
   │              ▼                          ▼
   │   IEventProcessor.           IEventProcessor.
   │   ProcessEventAsync()        ProcessScheduledEventAsync()
   │              │                          │
   │              ▼                          ▼
   │   [Handler Resolution & Execution]   [Handler Resolution & Execution]
   │              │                          │
   │              ▼                          ▼
   │   Semaphore.Release()        Semaphore.Release()
   │
   └─► Returns immediately (non-blocking)
```

**Configuration Options**:

```csharp
// Unbounded Channel (ChannelCapacity = -1)
new UnboundedChannelOptions
{
    SingleReader = false,        // Multiple concurrent readers
    SingleWriter = false,        // Multiple concurrent writers
    AllowSynchronousContinuations = true  // Performance optimization
}

// Bounded Channel (ChannelCapacity > 0)
new BoundedChannelOptions(capacity)
{
    FullMode = BoundedChannelFullMode.Wait,  // Block on full
    SingleReader = false,
    SingleWriter = false,
    AllowSynchronousContinuations = true
}
```

#### GeneralEventBus

Direct synchronous processing without channels.

**Flow**:
```
Publisher
   │
   ▼
PublishAsync() ────► IEventProcessor.ProcessEventAsync()
   │                      │
   │                      ▼
   │              [Handler Resolution & Execution]
   │                      │
   │                      ▼
   └─────────────────► Returns after processing
```

### 3. IEventHandler<TEvent>

**Purpose**: Define handler contracts with type safety.

**Design Decisions**:
- Generic interface for type safety
- Non-generic base for container resolution
- Bridge pattern implementation for seamless integration
- `CanHandle()` method allows dynamic filtering

**Bridge Pattern**:
```csharp
public interface IEventHandler<in TEvent> : IEventHandler 
    where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
    
    // Bridge implementations
    bool IEventHandler.CanHandle(IEvent @event) => @event is TEvent;
    Task IEventHandler.HandleAsync(IEvent @event, CancellationToken ct)
        => @event is TEvent typed 
            ? HandleAsync(typed, ct) 
            : Task.CompletedTask;
}
```

### 4. IEventProcessor

**Purpose**: Coordinate event processing and handler execution.

**Responsibilities**:
1. Resolve applicable handlers from DI container
2. Execute handlers (typically in parallel)
3. Handle exceptions per handler
4. Provide extension points for retry logic, circuit breakers, etc.
5. Support scheduled event processing with delay handling

**Interface Definition**:
```csharp
public interface IEventProcessor
{
    Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default);
    Task ProcessScheduledEventAsync(IEvent @event, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default);
    Task ProcessEventHandlersAsync(IEvent @event, CancellationToken cancellationToken = default);
    Task<TResult> InvokeAsync<TResult>(object @event, CancellationToken cancellationToken = default);
}
```

#### InMemoryEventProcessor

The library provides a built-in `InMemoryEventProcessor` with the following features:

**Key Features**:
- **In-Memory Scheduling**: Built-in scheduled event support via `ScheduledEventStore`
- **Fire-and-Forget Execution**: Uses `Task.Run` for non-blocking handler execution
- **Error Isolation**: Each handler's errors are isolated and logged
- **Request-Response Support**: Implements `InvokeAsync` for query patterns via `IRequestHandler<,>`
- **Parallel Execution**: All applicable handlers execute concurrently

**Components**:

1. **ScheduledEventStore**: Thread-safe in-memory storage for scheduled events
2. **ScheduledEventProcessingHostedService**: Background service that periodically checks for due events

**Typical Implementation Pattern**:
```csharp
public async Task ProcessEventHandlersAsync(IEvent @event, CancellationToken ct)
{
    // Fire-and-forget approach for non-blocking execution
    _ = Task.Run(async () =>
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        // Resolve all handlers
        var handlers = scope.ServiceProvider.GetServices<IEventHandler>();

        // Filter to applicable handlers
        var applicableHandlers = handlers.Where(h => h.CanHandle(@event)).ToList();

        if (applicableHandlers.Count == 0)
        {
            _logger.LogWarning("No handlers found for event type {EventType}", @event.GetType().Name);
            return;
        }

        // Execute in parallel with error isolation
        var tasks = applicableHandlers.Select(h => SafeHandleAsync(h, @event, ct));
        await Task.WhenAll(tasks);
    }, ct);
}

private async Task SafeHandleAsync(IEventHandler handler, IEvent @event, CancellationToken ct)
{
    try
    {
        await handler.HandleAsync(@event, ct);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Handler failed");
        // Don't rethrow - isolate failures
    }
}
```

**Scheduled Event Processing**:
```csharp
public async Task ProcessScheduledEventAsync(IEvent @event, DateTimeOffset scheduledTime, CancellationToken ct)
{
    // Store event in ScheduledEventStore
    _scheduledEventStore.AddScheduledEvent(@event, scheduledTime.ToUniversalTime());

    // Background service will process when due
}
```

### 5. Background Services for Event Processing

The EventBus uses three dedicated background services for processing events, providing separation of concerns and independent lifecycle management.

#### ChannelEventsPublishingHostedService

**Purpose**: Background service for processing immediate events from the publishing channel.

**Design Decisions**:
- Implements `BackgroundService` for automatic lifecycle management
- Uses `SemaphoreSlim` to control concurrency for immediate events
- Graceful shutdown on cancellation
- Error isolation (one event failure doesn't crash service)
- Independent from scheduled event processing

**Processing Loop**:
```csharp
protected override async Task ExecuteAsync(CancellationToken ct)
{
    var reader = _channelProvider.PublishingChannel.Reader;

    while (!ct.IsCancellationRequested)
    {
        try
        {
            // Read from publishing channel (blocks if empty)
            var @event = await reader.ReadAsync(ct);

            // Wait for available slot (concurrency control)
            await _semaphore.WaitAsync(ct);

            try
            {
                // Process event immediately
                await _eventProcessor.ProcessEventAsync(@event, ct);
            }
            finally
            {
                // Release slot
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            break; // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event");
            // Continue processing next event
        }
    }
}
```

#### ChannelEventsSchedulingHostedService

**Purpose**: Background service for processing scheduled events from the scheduling channel.

**Design Decisions**:
- Implements `BackgroundService` for automatic lifecycle management
- Uses its own `SemaphoreSlim` to control concurrency for scheduled events
- Graceful shutdown on cancellation
- Error isolation (one event failure doesn't crash service)
- Independent from immediate event processing

**Processing Loop**:
```csharp
protected override async Task ExecuteAsync(CancellationToken ct)
{
    var reader = _channelProvider.SchedulingChannel.Reader;

    while (!ct.IsCancellationRequested)
    {
        try
        {
            // Read from scheduling channel (blocks if empty)
            var scheduledEvent = await reader.ReadAsync(ct);

            // Wait for available slot (concurrency control)
            await _semaphore.WaitAsync(ct);

            try
            {
                // Process scheduled event (respects scheduled time)
                await _eventProcessor.ProcessScheduledEventAsync(
                    scheduledEvent.Event, 
                    scheduledEvent.ScheduledTime, 
                    ct);
            }
            finally
            {
                // Release slot
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            break; // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled event");
            // Continue processing next event
        }
    }
}
```

#### ScheduledEventProcessingHostedService

**Purpose**: Background service for processing in-memory scheduled events (used with `InMemoryEventProcessor`).

**Design Decisions**:
- Implements `BackgroundService` for automatic lifecycle management
- Periodically checks `ScheduledEventStore` for due events
- Uses `SemaphoreSlim` to control concurrency
- Independent from channel-based services
- Only registered when using `InMemoryEventProcessor`

**Key Features**:
- **Periodic Polling**: Checks for due events at configurable intervals
- **Delay Handling**: Supports small delays (< 1 minute) for precise timing
- **Automatic Cleanup**: Removes processed events from store
- **Error Handling**: Removes failed events to prevent infinite retries

**Processing Loop**:
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var checkInterval = TimeSpan.FromSeconds(_settings.ExecuteAfterSeconds > 0 
        ? _settings.ExecuteAfterSeconds 
        : 1);

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await ProcessDueEventsAsync(stoppingToken);
            await Task.Delay(checkInterval, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in scheduled event processing loop");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}

private async Task ProcessDueEventsAsync(CancellationToken ct)
{
    var dueEvents = _scheduledEventStore.GetDueEvents();

    if (!dueEvents.Any())
        return;

    foreach (var scheduledEvent in dueEvents)
    {
        await _semaphore.WaitAsync(ct);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();

                // Small delay for precise timing
                var delay = scheduledEvent.ScheduledTime - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero && delay < TimeSpan.FromMinutes(1))
                {
                    await Task.Delay(delay, ct);
                }

                // Process handlers
                await processor.ProcessEventHandlersAsync(scheduledEvent.Event, ct);

                // Remove from store
                _scheduledEventStore.RemoveScheduledEvent(scheduledEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scheduled event {EventId}", scheduledEvent.Id);
                _scheduledEventStore.RemoveScheduledEvent(scheduledEvent.Id);
            }
            finally
            {
                _semaphore.Release();
            }
        }, ct);
    }
}
```

**Benefits of Three Services**:
- **Separation of Concerns**: Each service has a single, clear responsibility
- **Independent Scaling**: Different `EventProcessorCapacity` could be configured per service in future versions
- **Better Observability**: Separate logging and monitoring for immediate, channel-scheduled, and in-memory scheduled events
- **Fault Isolation**: Failure in one service doesn't affect the others
- **Clearer Code**: Simpler, more maintainable implementation
- **Flexibility**: In-memory scheduling works independently of channel mode

## Concurrency Model

### Channel Mode

**Concurrency Levels**:

1. **Channel Writers**: Multiple concurrent publishers (controlled by `SingleWriter = false`)
2. **Channel Readers**: Multiple concurrent readers (controlled by `SingleReader = false`)
3. **Event Processors**: Each background service has its own `SemaphoreSlim` for independent concurrency control (configured via `EventProcessorCapacity`)
4. **Handler Execution**: All applicable handlers run in parallel via `Task.WhenAll()`

**Configuration Impact**:
```csharp
EventProcessorCapacity = 10              // Up to 10 events processing simultaneously per service
Handlers per event = 3                   // Each event triggers 3 handlers
Total concurrent handlers per service = 30  // 10 events × 3 handlers

// With three services (when using InMemoryEventProcessor):
// - ChannelEventsPublishingHostedService: 30 handlers
// - ChannelEventsSchedulingHostedService: 30 handlers  
// - ScheduledEventProcessingHostedService: 30 handlers
Total concurrent handlers (all) = 90     // 30 + 30 + 30

// With two services (custom processor without in-memory scheduling):
// - ChannelEventsPublishingHostedService: 30 handlers
// - ChannelEventsSchedulingHostedService: 30 handlers
Total concurrent handlers (both) = 60    // 30 + 30
```

**Benefits of Separate Services**:
- **Independent Concurrency**: Each service can process up to `EventProcessorCapacity` events concurrently
- **Better Resource Distribution**: Immediate, channel-scheduled, and in-memory scheduled events don't compete for the same processing slots
- **Fault Isolation**: A failure in one service doesn't affect the others' processing capacity

### General Mode

**Concurrency**:
- Limited by caller's concurrency
- If multiple threads call `PublishAsync`, multiple events process concurrently
- No built-in concurrency limits

### In-Memory Scheduling (InMemoryEventProcessor)

**How it Works**:

1. **Event Storage**: 
   - Events stored in `ScheduledEventStore` (thread-safe `ConcurrentDictionary`)
   - Each event has unique ID, scheduled time (UTC), and added timestamp

2. **Background Processing**:
   - `ScheduledEventProcessingHostedService` polls store periodically
   - Configurable interval via `ExecuteAfterSeconds` (default: 1 second)
   - Retrieves due events ordered by scheduled time

3. **Execution**:
   - Uses `SemaphoreSlim` for concurrency control (limit: `EventProcessorCapacity`)
   - Small delays (< 1 minute) handled precisely via `Task.Delay`
   - Events removed from store after processing

4. **Error Handling**:
   - Failed events removed from store to prevent infinite retries
   - Errors logged with event details
   - Processing continues for other events

**Example Flow**:
```
1. ScheduleAsync(event, scheduledTime) 
   ↓
2. InMemoryEventProcessor.ProcessScheduledEventAsync()
   ↓
3. ScheduledEventStore.AddScheduledEvent(event, time)
   ↓
4. Background service polls every N seconds
   ↓
5. ScheduledEventStore.GetDueEvents() 
   ↓
6. If due: Process handlers + Remove from store
```

**Thread Safety**:
- `ConcurrentDictionary` ensures thread-safe storage
- Multiple events can be added/removed concurrently
- Polling and processing don't block event scheduling

## Dependency Injection

### Registration Strategy

The library provides two registration overloads:

#### 1. With InMemoryEventProcessor (Recommended)

```csharp
builder.Services.AddEventBus(
    builder.Configuration,
    [typeof(Program).Assembly]
);
```

**Registers**:
- `IEventBus` (ChannelEventBus or GeneralEventBus based on config)
- `IEventProcessor` as `InMemoryEventProcessor`
- `ScheduledEventStore` (Singleton)
- `ScheduledEventProcessingHostedService` (BackgroundService)
- `ChannelEventsPublishingHostedService` (if Channel mode)
- `ChannelEventsSchedulingHostedService` (if Channel mode)
- All `IEventHandler<>` implementations from provided assemblies

#### 2. With Custom Event Processor

```csharp
builder.Services.AddEventBus<CustomEventProcessor>(
    builder.Configuration,
    [typeof(Program).Assembly]
);
```

**Registers**:
- `IEventBus` (ChannelEventBus or GeneralEventBus based on config)
- `IEventProcessor` as `CustomEventProcessor`
- `ChannelEventsPublishingHostedService` (if Channel mode)
- `ChannelEventsSchedulingHostedService` (if Channel mode)
- All `IEventHandler<>` implementations from provided assemblies

**Note**: `ScheduledEventProcessingHostedService` is **not** registered with custom processors. If you need in-memory scheduling with a custom processor, register it manually:

```csharp
builder.Services.AddEventBus<CustomEventProcessor>(configuration, [assemblies]);
builder.Services.AddSingleton<ScheduledEventStore>();
builder.Services.AddHostedService<ScheduledEventProcessingHostedService>();
```

**Handler Lifetime**: Scoped (created per event processing scope)

**Rationale**:
- Allows handlers to use scoped services (DbContext, etc.)
- Proper disposal of resources
- Isolation between event processing

**Handler Registration Code**:
```csharp
// Scan assemblies for event handlers
foreach (var assembly in assemblies)
{
    var handlers = assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && 
                    typeof(IEventHandler).IsAssignableFrom(t));

    foreach (var handler in handlers)
    {
        // Register as IEventHandler (non-generic)
        services.AddScoped(typeof(IEventHandler), handler);

        // Register generic interfaces
        var genericInterfaces = handler.GetInterfaces()
            .Where(i => i.IsGenericType && 
                        i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

        foreach (var iface in genericInterfaces)
        {
            services.AddScoped(iface, handler);
            services.AddScoped(handler); // Also register concrete type
        }
    }
}

// Same process for IRequestHandler<,> implementations
```

### Service Lifetimes

| Component | Lifetime | Reason |
|-----------|----------|--------|
| IEventBus | Scoped | Per-request isolation, supports both singleton and scoped usage via IServiceScopeFactory |
| IEventProcessor | Scoped | Consistent with IEventBus lifetime |
| IEventHandler | Scoped | Per-event processing isolation |
| IRequestHandler | Scoped | Per-request processing isolation |
| EventBusSettings | Singleton | Configuration |
| ScheduledEventStore | Singleton | Shared state for scheduled events |
| ChannelEventsPublishingHostedService | Singleton | Background service |
| ChannelEventsSchedulingHostedService | Singleton | Background service |
| ScheduledEventProcessingHostedService | Singleton | Background service |
| EventChannelProvider | Singleton | Shared channel instances |

## Performance Characteristics

### Memory Management

**Bounded Channel**:
- Fixed memory footprint: `capacity × sizeof(IEvent reference) ≈ capacity × 8 bytes`
- Example: 10,000 capacity ≈ 80 KB for queue structure
- Event objects themselves counted separately

**Unbounded Channel**:
- Dynamic allocation
- Grows with event volume
- Risk: Memory exhaustion if producers outpace consumers

**Event Lifetime**:
```
Event Created → Published → Enqueued → Dequeued → Processed → GC
                                                             ▲
                                                             │
                                              No references remaining
```

### Throughput Optimization

**Channel Mode Optimizations**:
1. `AllowSynchronousContinuations = true`: Reduces thread pool usage
2. `SingleReader = false`: Multiple concurrent readers
3. Semaphore limits: Prevents overwhelming handlers
4. Parallel handler execution: `Task.WhenAll()`

**Bottleneck Analysis**:
```
Publisher Rate = 10,000 events/sec
Handler Time = 10ms per event
EventProcessorCapacity = 10

Max Throughput = (1000ms / 10ms) × 10 = 1,000 events/sec

→ Channel will accumulate events
→ Solution: Increase EventProcessorCapacity or optimize handlers
```

## Thread Safety

### Thread-Safe Components

1. **Channel**: Thread-safe by design
2. **SemaphoreSlim**: Thread-safe synchronization
3. **DI Container**: Thread-safe service resolution
4. **ImmutableArray<Handler>**: No shared mutable state

### Considerations

**Handler State**: Handlers should be stateless or use scoped state only

❌ **Bad**:
```csharp
public class MyHandler : IEventHandler<MyEvent>
{
    private int _counter; // Shared mutable state!
    
    public async Task HandleAsync(MyEvent @event, CancellationToken ct)
    {
        _counter++; // Race condition!
    }
}
```

✅ **Good**:
```csharp
public class MyHandler : IEventHandler<MyEvent>
{
    private readonly IRepository _repo; // Scoped per event
    
    public MyHandler(IRepository repo)
    {
        _repo = repo;
    }
    
    public async Task HandleAsync(MyEvent @event, CancellationToken ct)
    {
        await _repo.SaveCounterAsync(); // Thread-safe via scoping
    }
}
```

## Error Handling Strategy

### Handler Isolation

**Principle**: One handler's failure should not affect others.

**Implementation**:
```csharp
// Each handler wrapped in try-catch
var tasks = handlers.Select(h => SafeHandleAsync(h, @event, ct));
await Task.WhenAll(tasks);
```

### Error Propagation

**Channel Mode**: Errors logged, event processing continues
**General Mode**: Errors logged, exception can be caught by publisher

### Retry Strategies

Implemented in custom `IEventProcessor`:

```csharp
public class RetryEventProcessor : IEventProcessor
{
    public async Task ProcessEventAsync(IEvent @event, CancellationToken ct)
    {
        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                await ProcessEventHandlersAsync(@event, ct);
                return;
            }
            catch (TransientException) when (i < maxRetries)
            {
                await Task.Delay(GetBackoff(i), ct);
            }
        }
    }
}
```

## Configuration Design

### EventBusSettings

**Design**: Plain POCO bound from configuration

**Rationale**:
- Easy to test (no dependencies)
- Type-safe access
- Validation can be added via FluentValidation or Data Annotations

### Build-Time Constants

Generated during build via MSBuild:

```xml
<PropertyGroup>
    <EventBusTypeConfigPath>EventBusSettings:EventBusType</EventBusTypeConfigPath>
    <BuildConstants>
        namespace $(RootNamespace);
        internal static class BuildConstants
        {
            public const string EventBusTypeConfigPath = "$(EventBusTypeConfigPath)";
            public const string Channel = "Channel";
            public const string General = "General";
        }
    </BuildConstants>
</PropertyGroup>
```

**Benefits**:
- No magic strings
- Compile-time checking
- Consistent configuration keys

## Extension Points

### Custom Event Processor

Implement `IEventProcessor` for custom behavior:
- Circuit breakers
- Retry policies
- Metrics collection
- Distributed tracing
- Event filtering
- Custom scheduling logic

**Example**:
```csharp
public class RetryableEventProcessor : IEventProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EventBusSettings _settings;
    private readonly ILogger _logger;

    public async Task ProcessEventAsync(IEvent @event, CancellationToken ct)
    {
        var retryIntervals = _settings.RetryIntervals;

        for (int attempt = 0; attempt <= _settings.RetryCount; attempt++)
        {
            try
            {
                await ProcessEventHandlersAsync(@event, ct);
                return; // Success
            }
            catch (Exception ex) when (attempt < _settings.RetryCount)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} failed. Retrying...", attempt + 1);
                await Task.Delay(TimeSpan.FromSeconds(retryIntervals[attempt]), ct);
            }
        }
    }

    public async Task ProcessScheduledEventAsync(
        IEvent @event, 
        DateTimeOffset scheduledTime, 
        CancellationToken ct)
    {
        // Custom scheduled event processing
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
        var tasks = applicableHandlers.Select(h => SafeHandleAsync(h, @event, ct));
        await Task.WhenAll(tasks);
    }

    public async Task<TResult> InvokeAsync<TResult>(object @event, CancellationToken ct)
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

    private async Task SafeHandleAsync(IEventHandler h, IEvent e, CancellationToken ct)
    {
        try
        {
            await h.HandleAsync(e, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler {Handler} failed", h.GetType().Name);
        }
    }
}
```

### Custom Event Bus

Implement `IEventBus` for different backends:
- Message broker integration (RabbitMQ, Azure Service Bus)
- Event persistence (database, file system)
- Event replay capability
- Event versioning

### Handler Middleware

Wrap handlers with decorators:

```csharp
public class LoggingHandlerDecorator<TEvent> : IEventHandler<TEvent>
    where TEvent : IEvent
{
    private readonly IEventHandler<TEvent> _inner;
    private readonly ILogger _logger;

    public LoggingHandlerDecorator(IEventHandler<TEvent> inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task HandleAsync(TEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("Before handling {EventType}", typeof(TEvent).Name);
        await _inner.HandleAsync(@event, ct);
        _logger.LogInformation("After handling {EventType}", typeof(TEvent).Name);
    }
}
```

### Custom Scheduled Event Storage

If you need persistent scheduled events, implement a custom storage:

```csharp
public class PersistentScheduledEventStore
{
    private readonly IDbContext _dbContext;

    public async Task AddScheduledEventAsync(IEvent @event, DateTimeOffset time)
    {
        var serialized = JsonSerializer.Serialize(@event);
        await _dbContext.ScheduledEvents.AddAsync(new ScheduledEventEntity
        {
            EventType = @event.GetType().AssemblyQualifiedName,
            EventData = serialized,
            ScheduledTime = time
        });
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<ScheduledEventEntry>> GetDueEventsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var entities = await _dbContext.ScheduledEvents
            .Where(e => e.ScheduledTime <= now)
            .ToListAsync();

        return entities.Select(e => new ScheduledEventEntry
        {
            Id = e.Id,
            Event = DeserializeEvent(e.EventType, e.EventData),
            ScheduledTime = e.ScheduledTime,
            AddedAt = e.CreatedAt
        }).ToList();
    }
}
```

## Design Trade-offs

### In-Memory vs Distributed

**In-Memory** (This Library):
- ✅ Simple, fast, no external dependencies
- ✅ Perfect for single-process applications
- ✅ Built-in scheduled event support
- ❌ Events lost on crash (unless using persistent storage extension)
- ❌ No cross-process communication

**Distributed** (RabbitMQ, Azure Service Bus, etc.):
- ✅ Durable, survives crashes
- ✅ Cross-process/service communication
- ✅ Native distributed scheduling
- ❌ Complex setup and configuration
- ❌ Network latency
- ❌ Additional infrastructure costs

### Channel vs General Mode

**Channel Mode**:
- ✅ Non-blocking publishers
- ✅ High throughput
- ✅ Background processing
- ✅ Better for production workloads
- ❌ Eventual consistency
- ❌ More complex debugging

**General Mode**:
- ✅ Immediate processing
- ✅ Simpler debugging
- ✅ Synchronous flow
- ✅ Better for testing
- ❌ Blocks publisher
- ❌ Lower throughput

### InMemoryEventProcessor vs Custom Processor

**InMemoryEventProcessor**:
- ✅ Built-in scheduled event support
- ✅ Fire-and-forget execution (non-blocking)
- ✅ In-memory storage via ScheduledEventStore
- ✅ Zero configuration required
- ✅ Suitable for most use cases
- ❌ No retry logic (fails once and logs)
- ❌ In-memory only (scheduled events lost on crash)

**Custom Event Processor**:
- ✅ Full control over processing logic
- ✅ Custom retry policies
- ✅ Circuit breakers, metrics, tracing
- ✅ Can integrate with external schedulers
- ❌ More code to write and maintain
- ❌ Must manually add ScheduledEventProcessingHostedService if needed
- ❌ Higher complexity

### In-Memory Scheduling vs External Scheduler

**In-Memory Scheduling** (InMemoryEventProcessor):
- ✅ No external dependencies
- ✅ Simple setup
- ✅ Fast for short delays
- ✅ Perfect for single-instance apps
- ❌ Scheduled events lost on app restart
- ❌ Limited to single process
- ❌ Not suitable for long delays (days/weeks)

**External Scheduler** (Hangfire, Quartz.NET):
- ✅ Persistent scheduling (survives restarts)
- ✅ Suitable for long delays
- ✅ Distributed scheduling
- ✅ Advanced features (cron, recurrence)
- ❌ External dependency
- ❌ More complex setup
- ❌ Additional infrastructure

## Future Enhancements

Potential improvements:

1. **Event Persistence**: Optional durable event store for scheduled events
2. **Event Replay**: Reprocess historical events
3. **Dead Letter Queue**: Handle permanently failed events
4. **Metrics/Health Checks**: Built-in observability with custom metrics
5. **Event Versioning**: Support event schema evolution
6. **Saga Support**: Long-running transaction coordination
7. **Priority Queue**: Process high-priority events first
8. **Batching**: Batch similar events for efficiency
9. **Retry Policies**: Built-in configurable retry strategies in InMemoryEventProcessor
10. **Distributed Scheduling**: Integration with external schedulers (Hangfire, Quartz.NET)
11. **Event Sourcing**: Integration with event sourcing patterns
12. **Streaming Support**: Integration with streaming platforms (Kafka, Event Hubs)

## Implementation Notes for Contributors

### Adding a New Feature

1. **Define the Interface**: Start with abstractions in `Abstractions` folder
2. **Implement Core Logic**: Add implementation in `Infrastructure` folder
3. **Update DI Registration**: Modify `DependencyInjection.cs`
4. **Add Tests**: Create comprehensive unit tests
5. **Update Documentation**: Update both README.md and ARCHITECTURE.md
6. **Add Samples**: Add usage examples if applicable

### Code Organization

```
src/Softoverse.EventBus.InMemory/
├── Abstractions/              # Interfaces and contracts
│   ├── IEvent.cs
│   ├── IEventBus.cs
│   ├── IEventHandler.cs
│   ├── IEventProcessor.cs
│   └── IRequestHandler.cs
├── Infrastructure/            # Implementations
│   ├── Channels/             # Channel-based implementations
│   │   ├── ChannelEventBus.cs
│   │   ├── ChannelEventsPublishingHostedService.cs
│   │   ├── ChannelEventsSchedulingHostedService.cs
│   │   └── EventChannelProvider.cs
│   ├── General/              # Direct processing implementations
│   │   └── GeneralEventBus.cs
│   └── Processors/           # Event processor implementations
│       ├── InMemoryEventProcessor.cs
│       ├── ScheduledEventStore.cs
│       └── ScheduledEventProcessingHostedService.cs
├── Models/                    # Data models and settings
│   ├── EventDispatch/
│   └── Settings/
│       └── EventBusSettings.cs
└── DependencyInjection.cs    # DI registration

tests/
├── EventBus.InMemory.Tests/  # Unit and integration tests
└── WorkerService/            # Sample application
```

### Testing Guidelines

1. **Unit Tests**: Test individual components in isolation
2. **Integration Tests**: Test end-to-end scenarios
3. **Performance Tests**: Benchmark critical paths
4. **Concurrency Tests**: Verify thread safety

### Performance Considerations

1. **Async All the Way**: Use async/await throughout
2. **Avoid Blocking**: Never use `.Result` or `.Wait()`
3. **Pool Resources**: Use object pooling for frequently allocated objects
4. **Minimize Allocations**: Reuse buffers and collections
5. **Batch Operations**: Process multiple items when possible

### Debugging Tips

1. **Use General Mode**: Easier to debug synchronous flow
2. **Enable Verbose Logging**: Set log level to Debug or Trace
3. **Breakpoints in Handlers**: Debug handler execution
4. **Monitor Channels**: Check channel reader/writer state
5. **Track Semaphore**: Monitor semaphore count for capacity issues

---

This architecture provides a solid foundation for event-driven applications while maintaining simplicity, performance, and extensibility. Contributors should focus on maintaining these principles when adding new features.
