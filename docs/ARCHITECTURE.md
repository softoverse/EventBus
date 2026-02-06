﻿# Architecture Documentation

## Overview

Softoverse.EventBus.InMemory is designed as a lightweight, high-performance in-memory event bus for .NET applications. This document describes the internal architecture, design decisions, and implementation details.

## Core Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Application Layer                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │   Service    │  │   Service    │  │   Service    │          │
│  │      A       │  │      B       │  │      C       │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
│         │                  │                  │                   │
│         └──────────────────┼──────────────────┘                   │
│                            │                                      │
└────────────────────────────┼──────────────────────────────────────┘
                             ▼
              ┌──────────────────────────────┐
              │         IEventBus            │
              │    (Abstraction Layer)       │
              └──────────────┬───────────────┘
                             │
                    ┌────────┴────────┐
                    │                 │
         ┌──────────▼─────────┐  ┌───▼──────────────┐
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

**Typical Implementation Pattern**:
```csharp
public async Task ProcessEventHandlersAsync(IEvent @event, CancellationToken ct)
{
    // Create new DI scope for scoped dependencies
    using var scope = _serviceProvider.CreateScope();
    
    // Resolve all handlers
    var handlers = scope.ServiceProvider.GetServices<IEventHandler>();
    
    // Filter to applicable handlers
    var applicableHandlers = handlers.Where(h => h.CanHandle(@event));
    
    // Execute in parallel with error isolation
    var tasks = applicableHandlers.Select(h => SafeHandleAsync(h, @event, ct));
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
        _logger.LogError(ex, "Handler failed");
        // Don't rethrow - isolate failures
    }
}
```

### 5. Background Services for Channel Processing

The EventBus uses two dedicated background services for processing events from channels, providing separation of concerns and independent lifecycle management.

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

**Benefits of Separation**:
- **Separation of Concerns**: Each service has a single, clear responsibility
- **Independent Scaling**: Different `EventProcessorCapacity` could be configured per service in future versions
- **Better Observability**: Separate logging and monitoring for immediate vs scheduled events
- **Fault Isolation**: Failure in one service doesn't affect the other
- **Clearer Code**: Simpler, more maintainable implementation

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

// With two services:
Total concurrent handlers (both) = 60    // Publishing (30) + Scheduling (30)
```

**Benefits of Separate Services**:
- **Independent Concurrency**: Each service (Publishing/Scheduling) can process up to `EventProcessorCapacity` events concurrently
- **Better Resource Distribution**: Immediate and scheduled events don't compete for the same processing slots
- **Fault Isolation**: A failure in one service doesn't affect the other's processing capacity

### General Mode

**Concurrency**:
- Limited by caller's concurrency
- If multiple threads call `PublishAsync`, multiple events process concurrently
- No built-in concurrency limits

## Dependency Injection

### Registration Strategy

**Handler Lifetime**: Scoped (created per event processing scope)

**Rationale**:
- Allows handlers to use scoped services (DbContext, etc.)
- Proper disposal of resources
- Isolation between event processing

**Registration Code**:
```csharp
// Scan assemblies for handlers
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
        }
    }
}
```

### Service Lifetimes

| Component | Lifetime | Reason |
|-----------|----------|--------|
| IEventBus | Singleton | Shared across application |
| IEventProcessor | Singleton | Stateless coordinator |
| IEventHandler | Scoped | Per-event processing isolation |
| EventBusSettings | Singleton | Configuration |
| ChannelEventsPublishingHostedService | Singleton | Background service |
| ChannelEventsSchedulingHostedService | Singleton | Background service |

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

### Custom Event Bus

Implement `IEventBus` for different backends:
- Message broker integration
- Event persistence
- Event replay capability

### Handler Middleware

Wrap handlers with decorators:

```csharp
public class LoggingHandlerDecorator<TEvent> : IEventHandler<TEvent>
    where TEvent : IEvent
{
    private readonly IEventHandler<TEvent> _inner;
    private readonly ILogger _logger;
    
    public async Task HandleAsync(TEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("Before handling");
        await _inner.HandleAsync(@event, ct);
        _logger.LogInformation("After handling");
    }
}
```

## Design Trade-offs

### In-Memory vs Distributed

**In-Memory** (This Library):
- ✅ Simple, fast, no external dependencies
- ✅ Perfect for single-process applications
- ❌ Events lost on crash
- ❌ No cross-process communication

**Distributed** (RabbitMQ, etc.):
- ✅ Durable, survives crashes
- ✅ Cross-process/service communication
- ❌ Complex setup and configuration
- ❌ Network latency

### Channel vs General Mode

**Channel Mode**:
- ✅ Non-blocking publishers
- ✅ High throughput
- ✅ Background processing
- ❌ Eventual consistency
- ❌ More complex debugging

**General Mode**:
- ✅ Immediate processing
- ✅ Simpler debugging
- ✅ Synchronous flow
- ❌ Blocks publisher
- ❌ Lower throughput

## Future Enhancements

Potential improvements:

1. **Event Persistence**: Optional durable event store
2. **Event Replay**: Reprocess historical events
3. **Dead Letter Queue**: Handle failed events
4. **Metrics/Health Checks**: Built-in observability
5. **Event Versioning**: Support event schema evolution
6. **Saga Support**: Long-running transaction coordination
7. **Priority Queue**: Process high-priority events first
8. **Batching**: Batch similar events for efficiency

---

This architecture provides a solid foundation for event-driven applications while maintaining simplicity and performance.
