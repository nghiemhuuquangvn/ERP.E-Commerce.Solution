# ERP Shared Contracts

Shared DTOs, value objects, and integration event contracts used across all microservices.

This package is referenced by all 14 services to ensure consistency in cross-service communication.

## Contents

- **IntegrationEventBase.cs** — Base class for all integration events (EventId, OccurredAt)
- Integration event definitions — Service-specific events (e.g., UserCreatedIntegrationEvent, SalesOrderCreatedIntegrationEvent)
- **Shared DTOs** — Request/response models used across service boundaries

## Naming Conventions

Integration events: `{AggregateRoot}{Action}IntegrationEvent`

Example:
```csharp
public class UserCreatedIntegrationEvent : IntegrationEventBase
{
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## Usage

All services import from this package:
```csharp
using ERP.Shared.Contracts;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
```

## How to Add New Shared Contracts

1. Create integration event class in this project inheriting from `IntegrationEventBase`
2. Add to your service's `Infrastructure/MessageBus/IntegrationEvents/` folder
3. Register consumer in your service's `Program.cs`:
   ```csharp
   services.AddScoped<IEventHandler<YourIntegrationEvent>, YourEventHandler>();
   ```
