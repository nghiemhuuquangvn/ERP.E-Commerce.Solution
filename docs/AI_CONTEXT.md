# AI Coding Rules

## Coding Standards

- Use Clean Architecture
- Use Reponsitory Pattern and Unit Of Work
- Use CQRS with MediatR
- No business logic inside controllers
- Use FluentValidation
- Use async/await everywhere
- Use Result pattern
- Use RESTfull API Standar

## Folder Structure

Application/
Domain/
Infrastructure/
Persistence/
API/

## Naming

Commands:
CreateOrderCommand

Queries:
GetOrderByIdQuery

Events:
OrderCreatedEvent

## Database Rules

- Use EF Core
- All tables use soft delete
- Never use SELECT *
- Avoid N + 1 queries

## Messaging Rules

Kafka:
- Integration events only
- Analytics stream only