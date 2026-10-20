# ERP Shared Messaging

Shared messaging interfaces and patterns for event-driven communication across microservices.

This library provides the abstraction for Kafka-based event publishing and consumption.

## Contents

- **IMessageBus.cs** — Abstraction for publishing integration events to Kafka
- **IEventHandler<TEvent>.cs** — Interface for consuming integration events
- **OutboxMessage.cs** — Entity class for the Outbox pattern
- **OutboxEventDeserializer.cs** — Shared rules every OutboxProcessor uses to turn an
  `OutboxMessage` back into its event (resolve by embedded `$type`, batch size, dead-letter)

## Outbox Pattern

Guarantees reliable, exactly-once event publishing by storing events in the database before publishing to Kafka.

### How It Works

1. **Service writes to database:**
   ```csharp
   var order = Order.Create(...);
   order.AddDomainEvent(new OrderCreatedDomainEvent(...));
   await _dbContext.SaveChangesAsync();
   ```

2. **DbContext intercepts SaveChangesAsync:**
   - Collects all domain events from entities
   - Serializes events to `OutboxMessages` table
   - Commits both entity changes + outbox entries in same transaction

3. **Background job (OutboxProcessor) every 5 seconds:**
   - Polls up to `OutboxEventDeserializer.DefaultBatchSize` unpublished, non-dead-lettered
     entries (`ProcessedOnUtc IS NULL`, `Error` not prefixed with `DeadLetterPrefix`)
   - Deserializes via `OutboxEventDeserializer.TryDeserialize` and publishes via `IMessageBus`
   - Sets `ProcessedOnUtc` only after a successful publish
   - On publish failure: records `Error`, retried on the next poll

### Guaranteed Delivery

- If service crashes before publish: events are still in OutboxMessages table → retry on restart
- If Kafka is down: events queue in OutboxMessages → publish when Kafka recovers
- Idempotent consumers: use EventId for deduplication if event published twice
  (EventId is an `init` property on `IntegrationEventBase`, so it survives serialize/deserialize)

## Message Bus Implementation

Each service implements `IMessageBus` with Kafka:

```csharp
public class KafkaMessageBus : IMessageBus
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaMessageBus> _logger;

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        var topic = $"{_serviceName}-topic";
        var serialized = JsonSerializer.Serialize(message);
        
        var deliveryResult = await _producer.ProduceAsync(
            topic,
            new Message<string, string> 
            { 
                Key = Guid.NewGuid().ToString(),
                Value = serialized 
            },
            cancellationToken
        );
        
        _logger.LogInformation("Published event to {Topic}: {MessageType}", topic, typeof(T).Name);
    }
}
```

## Event Handler Pattern

Each service registers handlers for integration events it consumes:

```csharp
// In Program.cs
services.AddScoped<IEventHandler<UserCreatedIntegrationEvent>, UserCreatedEventHandler>();

// Implementation
public class UserCreatedEventHandler : IEventHandler<UserCreatedIntegrationEvent>
{
    public async Task HandleAsync(UserCreatedIntegrationEvent @event, CancellationToken cancellationToken)
    {
        // React to user creation (e.g., create customer profile in CRM)
        var customer = new Customer { UserId = @event.UserId, Email = @event.Email };
        await _repository.AddAsync(customer, cancellationToken);
    }
}
```

## Retry Policy

What is actually implemented (no schema change, no retry counter column):
- **Publish failure** (broker down, timeout): `Error = ex.Message`, `ProcessedOnUtc` stays
  null → retried every poll (5s) until it succeeds.
- **Unrecoverable message** (empty/malformed JSON, `$type` that no longer resolves):
  `OutboxEventDeserializer.MarkDeadLettered` writes `Error = "[DeadLetter] <reason>"`,
  `ProcessedOnUtc` stays null, and the row is excluded from polling. Query
  `WHERE Error LIKE '[[]DeadLetter]%'` to inspect; set `Error = NULL` to re-queue.
- Never resolve the event type from the `Type` column alone — it is inconsistent across
  services (Name / FullName / AssemblyQualifiedName). The serialized `$type` is authoritative.

## Kafka Topics

Each service publishes to its own topic:
- `identity-topic` — Identity Service
- `sales-topic` (or `sale-order-topic`) — Sales Service
- `purchasing-topic` (or `purchase-order-topic`) — Purchasing Service
- `warehouse-topic` — Warehouse Service
- `accounting-topic` — Accounting Service
- `crm-topic` — CRM Service
- `hrm-topic` — HRM Service
- `product-topic` — Product Service
- `payment-topic` — Payment Service
- `shipping-topic` — Shipping Service
- `pos-topic` — POS Service
- `promotion-topic` — Promotion Service
- `notification-topic` — Notification Service
- `reporting-topic` — Reporting Service

All topics use Kafka's default partition strategy (by key for ordering within partition).
