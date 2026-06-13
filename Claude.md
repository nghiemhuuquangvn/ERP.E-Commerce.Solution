# ERP E-Commerce Solution

## Project Overview

ERP E-Commerce Solution is a .NET microservices ERP platform.

Architecture:
- Microservices
- DDD
- CQRS
- Clean Architecture
- Event Driven Architecture
- Saga Partern
- Outbox Partern

Technology:
- .NET 10
- SQL Server
- RabbitMQ
- Kafka
- Ocelot API Gateway
- Docker
- Kubernetes

## Repository Structure

src/
    services/
        IdentityService/
        PurchaseOrderService/
        SaleOrderService/
        WareHouseService/

    Shared/
        Contracts/
        Core/
        Infrastructure/
        Messaging/

    Database/
        Identity/
        Purchasing/
        Sales/

docs/
    architecture/
    business/
    Events/

deployment/
    docker/
    k8s/

tests/
    unit/
    integration/
    performance/

## Service Boundaries

### Identity Service

Responsibilities:
- Authentication
- Authorization
- User Management
- Role Management
- Permission Management

Owner Database:
- AuthDB Database

Published Events:
- UserCreated
- UserUpdated

Consumed Events:
- None

### Sale Order Service

Responsibilities:
- Sales Orders
- Customer Orders
- Sales Workflow

Database:
- Sales Database

Published Events:
- SalesOrderCreated
- SalesOrderApproved

Consumed Events:
- UserCreated

### Purchase Order Service

Responsibilities:
- Purchase Orders
- Supplier Orders
- Purchasing Workflow

Database:
- Purchasing Database

Published Events:
- PurchaseOrderCreated

Consumed Events:
- UserCreated

### Warehouse Service

Responsibilities:
- Inventory
- Stock Movement
- Reservation

Database:
- Warehouse Database

Published Events:
- InventoryReserved

Consumed Events:
- SalesOrderCreated
- PurchaseOrderCreated

## Database Ownership

Identity Service
    -> Identity Database

Sales Service
    -> Sales Database

Purchase Service
    -> Purchasing Database

Warehouse Service
    -> Warehouse Database

## Integration Rules

Services must not access another service database directly.

Allowed:
- REST API
- RabbitMQ
- Kafka
- Shared Contracts

Not Allowed:
- Cross Database Query
- Direct Table Access

## Coding Standards

Architecture:
- Clean Architecture
- TDD
- DDD
- CQRS

Application Layer:
- Use MediatR
- Use FluentValidation

Domain Layer:
- Contains business rules only

Infrastructure Layer:
- External systems
- Database access
- Message broker

API Layer:
- Controllers only

## Messaging

RabbitMQ:
- Commands
- Integration Events

Kafka:
- Event Streaming
- Analytics

Shared Contracts:
src/Shared/Contracts

## Documentation

Architecture:
docs/architecture

Business:
docs/business

Events:
docs/Events

Conventions:
docs/conventions

## Claude Working Rules

When reviewing code:

1. Stay within the current service boundary.

2. Do not move business logic into Infrastructure.

3. Do not introduce cross-service database access.

4. Prefer existing Shared Contracts before creating new DTOs.

5. Reuse existing events before creating new integration events.

6. Follow repository structure.

7. Review business documentation before changing business logic.

8. Review EventCatalog before modifying messaging flows.

## Priority Reading Order

1. CLAUDE.md
2. docs/architecture/service-map.md
3. docs/Events/EventCatalog.md
4. Service README.md
5. docs/business/*
6. Source Code

