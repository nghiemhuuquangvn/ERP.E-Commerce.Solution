# System Architecture
# Backend API
## Overview
This system is an enterprise microservice architecture built with .NET 10.

Services:
- AuthService
- ProductService
- OrderService
- PaymentService
- NotificationService
- WareHouseService
- WorkManagementService
- HRMService
- CRMService
- ManufactureService

Gateway:
- Ocelot API Gateway

Authentication:
- IdentityServer4
- Auth0 integration

Messaging:
- Kafka

Databases:
- SQL Server
- PostgreSQL

Logging:
- Serilog + Seq

Cache:
- Redis

## Service Communication

- Sync:
  REST API through Gateway

- Async:
  Kafka event bus

## Domain Responsibilities

### UserService
Handles:
- authentication
- user profile
- permissions

### OrderService
Handles:
- order lifecycle
- order events
- inventory reservation

## Important Rules

- No direct DB access across services
- All cross-service communication must use API or events
- Use repository pattern
- Use MediatR for CQRS
- All APIs must return Result<T>

# Frontend
- Use React Hook

## Landing Page

- Build a system to manage all subsystems, including Admin and UI.