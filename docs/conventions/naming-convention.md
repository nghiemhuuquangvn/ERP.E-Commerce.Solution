# Naming Conventions

Comprehensive naming conventions for all artifacts in the ERP E-Commerce Solution.

## File & Folder Naming

### Project Files

**Format**: PascalCase with descriptive names

```
IdentityService/
  IdentityService.csproj
  IdentityService.sln
```

### Class Files

**Rule**: One public class per file, filename matches class name

```csharp
// File: Order.cs
public class Order { }

// File: OrderStatus.cs
public enum OrderStatus { }

// File: CreateOrderCommand.cs
public class CreateOrderCommand { }

// File: IOrderRepository.cs
public interface IOrderRepository { }
```

### Folder Structure

**Use PascalCase** for folder names that represent domain concepts:

```
Domain/
  Aggregates/
    Order/
      Order.cs
      OrderItem.cs
      OrderStatus.cs
  ValueObjects/
    Money.cs
    Address.cs
  Events/
    OrderCreatedEvent.cs

Application/
  Commands/
    CreateOrderCommand.cs
    ApproveOrderCommand.cs
  Queries/
    GetOrderQuery.cs
  Handlers/
    CreateOrderCommandHandler.cs
```

### Test Files

**Append Test or Tests** to the class name being tested:

```
OrderTests.cs
OrderRepositoryTests.cs
CreateOrderCommandHandlerTests.cs
```

**Or use descriptive folder structure**:

```
Tests/
  Unit/
    Domain/
      OrderTests.cs
  Integration/
    Repositories/
      OrderRepositoryTests.cs
```

## Database Naming

### Table Names

**Use PascalCase with business entity name** (singular or plural):

```sql
-- Good
CREATE TABLE Users ( ... )
CREATE TABLE Roles ( ... )
CREATE TABLE SalesOrders ( ... )
CREATE TABLE OrderItems ( ... )

-- Bad
CREATE TABLE user ( ... )
CREATE TABLE USERS ( ... )
CREATE TABLE tbl_Users ( ... )
```

### Column Names

**Use PascalCase matching property names**:

```sql
CREATE TABLE Orders (
    OrderId UNIQUEIDENTIFIER PRIMARY KEY,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    OrderDate DATETIME2 NOT NULL,
    TotalAmount DECIMAL(18, 2),
    Status NVARCHAR(50),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2
)
```

### Foreign Key Names

**Format**: FK_{TableName}_{ReferencedTable}

```sql
ALTER TABLE Orders
ADD CONSTRAINT FK_Orders_Customers
FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)

ALTER TABLE OrderItems
ADD CONSTRAINT FK_OrderItems_Orders
FOREIGN KEY (OrderId) REFERENCES Orders(OrderId)
```

### Index Names

**Format**: IX_{TableName}_{ColumnNames}

```sql
CREATE INDEX IX_Orders_CustomerId ON Orders(CustomerId)
CREATE INDEX IX_OrderItems_OrderId ON OrderItems(OrderId)
CREATE INDEX IX_Orders_Status_CreatedAt ON Orders(Status, CreatedAt)
```

### Unique Constraints

**Format**: UQ_{TableName}_{ColumnNames}

```sql
ALTER TABLE Users
ADD CONSTRAINT UQ_Users_Email UNIQUE (Email)

ALTER TABLE Roles
ADD CONSTRAINT UQ_Roles_Name UNIQUE (Name)
```

### Check Constraints

**Format**: CK_{TableName}_{ConditionDescription}

```sql
ALTER TABLE Orders
ADD CONSTRAINT CK_Orders_PositiveAmount 
CHECK (TotalAmount > 0)

ALTER TABLE OrderItems
ADD CONSTRAINT CK_OrderItems_PositiveQuantity
CHECK (Quantity > 0)
```

### Stored Procedures

**Format**: sp_{ServiceName}_{Action}

```sql
CREATE PROCEDURE sp_Orders_CreateOrder
    @CustomerId UNIQUEIDENTIFIER,
    @TotalAmount DECIMAL(18, 2)
AS
BEGIN
    -- Implementation
END

CREATE PROCEDURE sp_Inventory_GetLowStockItems
    @ReorderPoint INT
AS
BEGIN
    -- Implementation
END
```

### Views

**Prefix with v_**:

```sql
CREATE VIEW v_OrderSummary AS
SELECT O.OrderId, O.CustomerId, COUNT(OI.OrderItemId) as ItemCount
FROM Orders O
LEFT JOIN OrderItems OI ON O.OrderId = OI.OrderId
GROUP BY O.OrderId, O.CustomerId

CREATE VIEW v_CustomerOrderHistory AS
SELECT C.CustomerId, C.Name, COUNT(O.OrderId) as OrderCount
FROM Customers C
LEFT JOIN Orders O ON C.CustomerId = O.CustomerId
GROUP BY C.CustomerId, C.Name
```

## Code Naming Conventions

### Namespaces

**Format**: {Company}.{Product}.{Service}.{Layer}

```csharp
namespace ERP.Sales.Application.Commands { }
namespace ERP.Sales.Domain.Aggregates { }
namespace ERP.Sales.Infrastructure.Repositories { }
namespace ERP.Shared.Contracts { }
```

### Classes

**Use PascalCase** with descriptive nouns:

```csharp
public class Order { }
public class OrderItem { }
public class Customer { }
public class InventoryReservation { }
public class SalesOrderApprovedEvent { }
```

### Interfaces

**Prefix with I** using PascalCase:

```csharp
public interface IOrderRepository { }
public interface IOrderService { }
public interface IEventBus { }
public interface IInventoryService { }
```

### Enums

**Use PascalCase**, values in UPPER_SNAKE_CASE:

```csharp
public enum OrderStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum MovementType
{
    Received = 0,
    Sold = 1,
    Adjusted = 2,
    Damaged = 3,
    Transferred = 4
}
```

### Methods

**Use PascalCase with verb-noun pattern**:

```csharp
public void ValidateOrder(Order order) { }
public async Task<Order> GetOrderAsync(Guid orderId) { }
public bool IsValidCustomer(Guid customerId) { }
public void UpdateOrderStatus(Order order, OrderStatus status) { }
public async Task PublishEventAsync(IntegrationEvent @event) { }
```

### Properties

**Use PascalCase**:

```csharp
public class Order
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
```

### Local Variables

**Use camelCase**:

```csharp
public void ProcessOrder(Order order)
{
    var customerId = order.CustomerId;
    var orderItems = order.Items;
    var totalAmount = order.TotalAmount;
    var isApproved = order.Status == OrderStatus.Approved;
    var orderDate = order.OrderDate;
}
```

### Parameters

**Use camelCase**:

```csharp
public async Task<OrderResponse> CreateOrderAsync(
    Guid customerId, 
    List<CreateOrderItemRequest> lineItems,
    string shippingAddress)
{
    // Implementation
}
```

### Private/Protected Members

**Use camelCase with underscore prefix** for private fields:

```csharp
public class Order
{
    private readonly List<OrderItem> _items = new();
    private decimal _totalAmount;
    protected DateTime _lastModified;
    
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
}
```

### Constants

**Use UPPER_SNAKE_CASE**:

```csharp
public class OrderConstants
{
    public const decimal DEFAULT_TAX_RATE = 0.08m;
    public const int MAX_ORDER_ITEMS = 100;
    public const int RESERVATION_HOLD_HOURS = 72;
    public const string FULFILLMENT_QUEUE = "orders.fulfillment";
}
```

### Static Fields

**Use PascalCase or UPPER_SNAKE_CASE** based on mutability:

```csharp
public class OrderService
{
    private static readonly ILogger Logger = LoggerFactory.CreateLogger<OrderService>();
    private static readonly string ORDER_TOPIC = "erp.sales.orders";
}
```

## Domain-Driven Design Naming

### Aggregates

**Entity names that represent business concepts**:

```csharp
public class Order : AggregateRoot { }
public class Customer : AggregateRoot { }
public class Supplier : AggregateRoot { }
public class Inventory : AggregateRoot { }
public class PurchaseOrder : AggregateRoot { }
```

### Value Objects

**Immutable objects representing concepts**:

```csharp
public class Money : ValueObject { }
public class Address : ValueObject { }
public class OrderStatus : ValueObject { }
public class Quantity : ValueObject { }
```

### Domain Events

**Format**: {AggregateRoot}{Action}Event

```csharp
public class OrderCreatedEvent : DomainEvent { }
public class OrderApprovedEvent : DomainEvent { }
public class OrderShippedEvent : DomainEvent { }
public class OrderCancelledEvent : DomainEvent { }
```

### Integration Events

**Format**: {AggregateRoot}{Action}Event**

```csharp
public class SalesOrderCreatedEvent : IntegrationEvent { }
public class InventoryReservedEvent : IntegrationEvent { }
public class PaymentReceivedEvent : IntegrationEvent { }
```

### Repositories

**Format**: I{AggregateRoot}Repository

```csharp
public interface IOrderRepository : IRepository<Order> { }
public interface ICustomerRepository : IRepository<Customer> { }
public interface IInventoryRepository : IRepository<Inventory> { }
```

### Services

**Format**: {Domain}Service or I{Domain}Service (interface)**

```csharp
public interface IOrderService { }
public class OrderService : IOrderService { }

public interface IInventoryService { }
public class InventoryService : IInventoryService { }
```

## CQRS Naming

### Commands

**Format**: {Action}{AggregateRoot}Command

```csharp
public class CreateOrderCommand : IRequest<OrderResponse> { }
public class ApproveOrderCommand : IRequest { }
public class CancelOrderCommand : IRequest { }
public class ReserveInventoryCommand : IRequest<ReservationResponse> { }
```

### Command Handlers

**Format**: {Action}{AggregateRoot}CommandHandler

```csharp
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderResponse> { }
public class ApproveOrderCommandHandler : IRequestHandler<ApproveOrderCommand> { }
```

### Queries

**Format**: Get{AggregateRoot}Query or {Action}{AggregateRoot}Query

```csharp
public class GetOrderQuery : IRequest<OrderResponse> { }
public class ListOrdersQuery : IRequest<PagedResult<OrderResponse>> { }
public class GetCustomerOrderHistoryQuery : IRequest<List<OrderResponse>> { }
```

### Query Handlers

**Format**: {Action}{AggregateRoot}QueryHandler

```csharp
public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderResponse> { }
public class ListOrdersQueryHandler : IRequestHandler<ListOrdersQuery, PagedResult<OrderResponse>> { }
```

## API Naming

### REST Endpoints

**Use lowercase with hyphens** (kebab-case):

```
GET    /api/v1/orders
POST   /api/v1/orders
GET    /api/v1/orders/{orderId}
PUT    /api/v1/orders/{orderId}
DELETE /api/v1/orders/{orderId}
POST   /api/v1/orders/{orderId}/approve
POST   /api/v1/orders/{orderId}/cancel
GET    /api/v1/customers/{customerId}/orders
GET    /api/v1/inventory/low-stock
```

### Request/Response DTOs

**Format**: {Action}{EntityName}Request / {EntityName}Response

```csharp
public class CreateOrderRequest { }
public class UpdateOrderRequest { }
public class OrderResponse { }
public class PagedOrderResponse { }
public class ApproveOrderRequest { }
```

## Event Message Naming

### RabbitMQ Queues

**Format**: {service}.{entity}.{action}

```
sales.order.created
sales.order.approved
sales.order.shipped
warehouse.inventory.reserved
purchasing.purchase-order.received
```

### Kafka Topics

**Format**: {service}-{entity}-{type}

```
sales-orders-events
warehouse-inventory-events
purchasing-purchase-orders-events
```

## Configuration & Settings

### Configuration Keys

**Use dot notation with hierarchical structure**:

```json
{
  "ConnectionStrings": {
    "SalesDatabase": "...",
    "IdentityDatabase": "..."
  },
  "Jwt": {
    "SecretKey": "...",
    "Issuer": "...",
    "ExpirationMinutes": 60
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest"
  },
  "Services": {
    "IdentityService": "http://localhost:5001",
    "WarehouseService": "http://localhost:5004"
  }
}
```

### Environment Variables

**Use UPPER_SNAKE_CASE**:

```bash
ASPNETCORE_ENVIRONMENT=Production
CONNECTION_STRING_SALES_DB=...
JWT_SECRET_KEY=...
RABBITMQ_HOSTNAME=...
LOG_LEVEL=Information
```

## Migration Files

**Format**: {Timestamp}_{DescriptiveActionName}.cs

```
20260613100000_CreateOrdersTable.cs
20260613110000_AddCustomersTable.cs
20260613120000_CreateIndexOnOrderCustomerId.cs
20260614090000_AddApprovalColumnsToOrders.cs
```

## Test Data & Fixtures

**Use descriptive names with Clear/Arrange/Act/Assert sections**:

```csharp
[Fact]
public void Order_Approve_SuccessfullyApprovedValidOrder()
{
    // Arrange
    var order = CreateTestOrder(OrderStatus.Submitted);
    
    // Act
    order.Approve();
    
    // Assert
    Assert.Equal(OrderStatus.Approved, order.Status);
}
```

## Summary Table

| Artifact | Format | Example |
|----------|--------|---------|
| Namespace | PascalCase with dots | `ERP.Sales.Application.Commands` |
| Class | PascalCase | `SalesOrder` |
| Interface | I + PascalCase | `IOrderRepository` |
| Method | PascalCase | `CreateOrderAsync` |
| Property | PascalCase | `TotalAmount` |
| Local variable | camelCase | `customerId` |
| Constant | UPPER_SNAKE_CASE | `MAX_ORDER_ITEMS` |
| Table | PascalCase | `SalesOrders` |
| Column | PascalCase | `OrderId` |
| Foreign Key | FK_{Table}_{Ref} | `FK_Orders_Customers` |
| Index | IX_{Table}_{Cols} | `IX_Orders_CustomerId` |
| Stored Proc | sp_{Service}_{Action} | `sp_Orders_CreateOrder` |
| Command | {Action}{Entity}Command | `CreateOrderCommand` |
| Query | Get{Entity}Query | `GetOrderQuery` |
| Event | {Entity}{Action}Event | `OrderCreatedEvent` |
| DTO Request | {Action}{Entity}Request | `CreateOrderRequest` |
| DTO Response | {Entity}Response | `OrderResponse` |
| API Endpoint | /api/v1/{resource} | `/api/v1/orders` |
| Queue | {service}.{entity}.{action} | `sales.order.created` |

---

**Last Updated**: 2026-06-13  
**Version**: 1.0.0
