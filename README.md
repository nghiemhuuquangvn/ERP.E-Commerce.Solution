# ERP E-Commerce Solution

A comprehensive .NET 10 microservices-based ERP platform designed for enterprise e-commerce operations. Built with Domain-Driven Design (DDD), CQRS, and Event-Driven Architecture patterns.

## Quick Links

- **[CLAUDE.md](CLAUDE.md)** - Project guidelines and working rules
- **[Service Map](docs/architecture/service-map.md)** - Service architecture and interactions
- **[Event Catalog](docs/architecture/event-catalog.md)** - Integration events reference
- **[API Contracts](src/Shared/Contracts/README.md)** - Shared DTOs/integration event contracts
- **[Architecture](docs/architecture/)** - System design and decisions
- **[Coding Standards](docs/conventions/coding-standard.md)** - Development guidelines

## Project Overview

### Core Domains

- **Identity Service** - Authentication, authorization, user & role management
- **Sales Order Service** - Sales orders, customer orders, sales workflow
- **Purchase Order Service** - Purchase orders, supplier orders, purchasing workflow
- **Warehouse Service** - Inventory management, stock movement, reservations

### Technology Stack

- **.NET 10** - Runtime platform
- **SQL Server** - Primary data store with per-service databases
- **RabbitMQ** - Asynchronous messaging (commands, integration events)
- **Kafka** - Event streaming and analytics
- **Ocelot API Gateway** - API routing and composition
- **Docker & Kubernetes** - Containerization and orchestration

### Architecture Patterns

- **Microservices** - Autonomous, independently deployable services
- **Domain-Driven Design (DDD)** - Business logic organization around domains
- **CQRS** - Separation of read and write models
- **Event-Driven Architecture** - Services communicate via events
- **Saga Pattern** - Distributed transaction management
- **Outbox Pattern** - Guaranteed message delivery

## Getting Started

### Prerequisites

- .NET 10 SDK
- SQL Server 2019 or later
- Docker & Docker Compose
- Git

### Quick Start

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd ERP.E-Commerce.Solution
   ```

2. **Set up databases**
   - Identity Database
   - Sales Database
   - Purchasing Database
   - Warehouse Database

3. **Configure message brokers**
   - RabbitMQ (port 5672)
   - Kafka (port 9092)

4. **Run services**
   ```bash
   # One-time: copy .env.example to .env and set SA_PASSWORD / RABBITMQ_PASSWORD /
   # JWT_SIGNING_KEY (docker-compose.yml reads secrets from .env, which is gitignored)
   cp .env.example .env

   docker-compose up -d
   ```

5. **Access API Gateway**
   - Gateway URL: http://localhost:5000

For detailed setup instructions, see [Setup Guide](docs/architecture/setup-guide.md).

## Project Structure

```
src/
├── services/
│   ├── IdentityService/          # Authentication & authorization
│   ├── SaleOrderService/         # Sales management
│   ├── PurchaseOrderService/     # Purchasing management
│   └── WareHouseService/         # Inventory management
├── Shared/
│   ├── Contracts/               # Shared DTOs & API contracts
│   ├── Core/                    # Shared domain models
│   ├── Infrastructure/          # Shared infrastructure utilities
│   └── Messaging/               # Message bus abstractions
└── Database/
    ├── Identity/               # Identity service database
    ├── Purchasing/             # Purchasing service database
    └── Sales/                  # Sales service database

docs/
├── architecture/               # Architecture decisions & patterns
├── business/                   # Business logic documentation
├── contracts/                  # API specifications
├── conventions/                # Coding & naming standards
└── Events/                     # Event documentation

deployment/
├── docker/                     # Docker configurations
└── k8s/                        # Kubernetes manifests

tests/
├── unit/                       # Unit tests
├── integration/                # Integration tests
└── performance/                # Performance tests
```

## Architecture Highlights

### Service Isolation

Each microservice owns its database and communicates only through:
- REST APIs
- RabbitMQ (for commands and events)
- Kafka (for streaming and analytics)
- Shared contracts (DTOs)

### Event Flow

```
Client → API Gateway → Service A → RabbitMQ/Kafka → Service B → Database
                ↓
            Service C (listener)
```

### Data Consistency

- **Distributed Transactions**: Managed via Saga pattern
- **Message Delivery**: Guaranteed with Outbox pattern
- **Event Sourcing**: Optional per service
- **CQRS**: Read models separate from write models

## Development Workflow

### 1. Code Organization

Follow clean architecture layers:
- **API Layer**: Controllers only
- **Application Layer**: Use MediatR for commands/queries, FluentValidation for input
- **Domain Layer**: Business rules and aggregates
- **Infrastructure Layer**: Data access, message brokers, external services

### 2. Creating a New Feature

1. Define domain models in Domain layer
2. Create commands/queries in Application layer
3. Implement handlers with MediatR
4. Define API endpoints in API layer
5. Write unit & integration tests
6. Update event catalog if publishing events

### 3. Integration Between Services

1. Define shared contract in `src/Shared/Contracts`
2. Publish integration event from source service
3. Configure listener in target service
4. Handle event in target service's event handler

For detailed guidelines, see [CLAUDE.md](CLAUDE.md) and [Coding Standards](docs/conventions/coding-standard.md).

## Integration Rules

### What's Allowed
- REST API calls between services
- RabbitMQ messaging (commands, integration events)
- Kafka event streaming
- Shared contracts from `src/Shared/Contracts`

### What's NOT Allowed
- Direct database access across service boundaries
- Cross-service table queries
- Shared entity models (use DTOs instead)
- Synchronous cascading calls

## Testing Strategy

- **Unit Tests**: Domain logic, business rules
- **Integration Tests**: Service interactions, database operations
- **Performance Tests**: Load testing, response times
- **E2E Tests**: Complete business workflows

See [Testing Strategy](docs/architecture/testing-strategy.md) for details.

## Deployment

### Development
```bash
docker-compose up -d
# Services available at http://localhost:5000
```

### Production
- Kubernetes deployment manifests in `deployment/k8s/`
- Docker images built and pushed to container registry
- Helm charts for Kubernetes deployments

See [Deployment Guide](docs/deployment/deployment-guide.md) for details.

## Documentation

### Priority Reading Order

1. [CLAUDE.md](CLAUDE.md) - Project guidelines & working rules
2. [Service Map](docs/architecture/service-map.md) - Architecture overview
3. [Event Catalog](docs/architecture/event-catalog.md) - Integration events
4. Service READMEs - Individual service documentation
5. [Business Documentation](docs/business/) - Domain-specific business logic
6. Source code - Implementation details

### Key Documents

- **[Coding Standards](docs/conventions/coding-standard.md)** - Code style & patterns
- **[Naming Conventions](docs/conventions/naming-convention.md)** - Naming rules
- **[SQL Standards](docs/conventions/sql-standard.md)** - Database conventions
- **[Git Flow](docs/conventions/git-flow.md)** - Version control workflow

## Key Services

### Identity Service
Manages user authentication, authorization, roles, and permissions across all services.

**Key Features:**
- User registration & login
- Role-based access control (RBAC)
- Permission management
- OAuth2/OpenID Connect integration

**Published Events:** UserCreated, UserUpdated, RoleAssigned

### Sales Order Service
Manages customer orders and sales workflows.

**Key Features:**
- Create & manage sales orders
- Sales order approval workflow
- Customer order tracking
- Integration with Warehouse Service

**Published Events:** SalesOrderCreated, SalesOrderApproved

### Purchase Order Service
Manages supplier orders and purchasing workflows.

**Key Features:**
- Create & manage purchase orders
- Supplier management
- Purchasing approval workflow
- Inventory synchronization

**Published Events:** PurchaseOrderCreated, PurchaseOrderApproved

### Warehouse Service
Manages inventory, stock movements, and reservations.

**Key Features:**
- Inventory tracking
- Stock movement recording
- Reservation management
- Real-time stock levels

**Published Events:** InventoryReserved, InventoryUpdated

## Database Architecture

Each service has an isolated database:

| Service | Database | Schema |
|---------|----------|--------|
| Identity | IdentityDB | dbo.Users, dbo.Roles, dbo.Permissions |
| Sales | SalesDB | dbo.SalesOrders, dbo.OrderItems, dbo.Customers |
| Purchasing | PurchasingDB | dbo.PurchaseOrders, dbo.PurchaseItems, dbo.Suppliers |
| Warehouse | WarehouseDB | dbo.Inventory, dbo.StockMovements, dbo.Reservations |

## Contributing

1. Create a feature branch from `master`
2. Follow [Coding Standards](docs/conventions/coding-standard.md)
3. Write unit & integration tests
4. Submit pull request with clear description
5. Ensure all tests pass and code reviews are approved

## Support & Resources

- **Issues**: GitHub Issues for bug reports and feature requests
- **Documentation**: See `docs/` directory
- **API Documentation**: See `src/Shared/Contracts/README.md`

## License

[Add your license here]

## Contact

For questions or support, contact the ERP team.

---

**Last Updated**: 2026-06-13
**Version**: 1.0.0
