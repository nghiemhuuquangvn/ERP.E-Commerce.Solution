# ERP.E-Commerce.Solution

## Architecture

Microservice Architecture

Services:

- IdentityService
- SaleOrderService
- PurchaseOrderService

## Patterns

- Clean Architecture
- DDD
- CQRS
- Repository Pattern

## Messaging

- RabbitMQ
- Kafka

## API Gateway

- Ocelot

## Authentication

- IdentityServer
- Auth0

## Database

Each service owns its own database.

## Rules

Never put business logic in Controller.

Use MediatR for commands.

Use Repository only through Application Layer.