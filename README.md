# Softoverse.EventBus.InMemory

[![NuGet Version](https://img.shields.io/nuget/v/Softoverse.EventBus.InMemory.svg)](https://www.nuget.org/packages/Softoverse.EventBus.InMemory/)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

A lightweight, high-performance in-memory event bus implementation for .NET 9+ applications that enables decoupled communication through the publish-subscribe pattern. Perfect for implementing domain events, CQRS architectures, and microservice communication patterns within a single application instance.

## ? Features

- **?? High Performance**: Built on .NET 9's `System.Threading.Channels` for optimal throughput
- **? Asynchronous Processing**: Non-blocking event publishing and handling
- **?? Type-Safe**: Strongly-typed event contracts with compile-time safety
- **?? Flexible Configuration**: Choose between Channel-based or General event processing
- **?? Concurrency Control**: Built-in semaphore-based concurrency management
- **??? Resilient**: Configurable retry policies and error handling
- **?? Bulk Operations**: Support for bulk event publishing
- **?? Extensive Logging**: Comprehensive logging for monitoring and debugging
- **??? Dependency Injection**: First-class DI container support

## ?? Installation

Install the package via NuGet Package Manager:

```bash
dotnet add package Softoverse.EventBus.InMemory
```

Or via Package Manager Console:

```powershell
Install-Package Softoverse.EventBus.InMemory
```

## ?? Quick Start

### 1. Define Your Events

Create events by implementing the `IEvent` interface or inheriting from `EventBase`:

```csharp
using Softoverse.EventBus.InMemory.Abstractions;

// Using EventBase (recommended)
public class OrderCreatedEvent : EventBase
{
    public string OrderNumber { get; set; }
    public decimal Amount { get; set; }
    public string CustomerId { get; set; }
    
    public OrderCreatedEvent() : base() { }
    public OrderCreatedEvent(Guid id) : base(id) { }
}

// Or implement IEvent directly
public class PaymentProcessedEvent : IEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string PaymentId { get; set; }
    public decimal Amount { get; set; }
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
        _logger.LogInformation("Processing event {EventType} with ID {EventId}", 
            @event.GetType().Name, @event.Id);
            
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

## ?? Configuration

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

## ?? Usage

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

## ?? Processing Strategies

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

## ??? Advanced Usage

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

## ?? Monitoring and Diagnostics

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

## ?? Testing

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

## ?? Performance Characteristics

- **Throughput**: Handles thousands of events per second in Channel mode
- **Memory Usage**: Efficient memory management with bounded channels
- **Latency**: Sub-millisecond event publishing, configurable processing delays
- **Scalability**: Horizontal scaling through concurrency controls

## ?? Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## ?? License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## ?? Links

- [Repository](https://github.com/mahmudabir/EventBus)
- [NuGet Package](https://www.nuget.org/packages/Softoverse.EventBus.InMemory/)
- [Issues](https://github.com/mahmudabir/EventBus/issues)

## ?? Changelog

### Version 1.0.0
- Initial release
- Channel-based and General event processing strategies
- Comprehensive DI integration
- Configurable retry policies
- Extensive logging support