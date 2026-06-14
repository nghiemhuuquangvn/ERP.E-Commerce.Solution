# ERP Shared Core

Core domain interfaces and base classes used by all microservices.

This library provides the foundation for DDD (Domain-Driven Design) implementation across the entire platform.

## Contents

### Domain
- **BaseEntity.cs** — Base class for all domain entities with:
  - GUID primary key
  - Soft-delete (IsDeleted flag)
  - Audit fields (CreatedBy/At, UpdatedBy/At)
  - Domain event collection and publishing
  
- **AggregateRoot.cs** — Base class for aggregate roots (inherits from BaseEntity)

- **IDomainEvent.cs** — Marker interface for domain events

- **ValueObject.cs** — Base class for immutable value objects with:
  - Value equality semantics (compared by value, not identity)
  - Automatic GetHashCode and Equals implementation

## Architecture Pattern

Domain entities in each service inherit from `BaseEntity`:

```csharp
public class Order : AggregateRoot
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; private set; }

    public static Order Create(Guid customerId, List<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Draft
        };
        
        order.AddDomainEvent(new OrderCreatedDomainEvent(order.OrderId, customerId));
        return order;
    }

    public void Approve()
    {
        Status = OrderStatus.Approved;
        AddDomainEvent(new OrderApprovedDomainEvent(OrderId));
    }
}
```

## Domain vs Integration Events

- **Domain Events** (IDomainEvent) — Raised within a service to trigger business logic changes
  - Naming: `{AggregateRoot}{Action}DomainEvent`
  - Example: `OrderApprovedDomainEvent`
  - Published to Outbox → Kafka by OutboxProcessor

- **Integration Events** (IntegrationEventBase) — Sent to other services via Kafka
  - Naming: `{AggregateRoot}{Action}IntegrationEvent`
  - Example: `OrderApprovedIntegrationEvent`
  - Deserialized from Kafka by event handlers

## Soft Delete Pattern

Entities are never physically deleted. Instead, `IsDeleted = true`:

```csharp
// In repository
var query = _dbContext.Orders
    .Where(o => !o.IsDeleted)  // Implicit filter in all queries
    .AsQueryable();
```

## Audit Trail

All BaseEntity objects track:
- `CreatedBy` — User ID who created the entity
- `CreatedAt` — Timestamp of creation
- `UpdatedBy` — User ID who last modified the entity
- `UpdatedAt` — Timestamp of last modification

These are auto-populated by the service's DbContext in `SaveChangesAsync`.
