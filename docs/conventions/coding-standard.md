# Coding Standards

Comprehensive coding standards and best practices for the ERP E-Commerce Solution codebase.

## C# Naming Conventions

### General Naming Rules

**Use English** for all names, comments, and documentation.

**Use meaningful names** that clearly indicate purpose and intent.

```csharp
// GOOD: Clear intent
var orderCreatedDate = DateTime.UtcNow;
var isValidCustomer = ValidateCustomer(customer);

// BAD: Unclear abbreviations
var ocd = DateTime.UtcNow;
var ivc = ValidateCustomer(customer);
```

### Class & Interface Names

**Use PascalCase** for class and interface names.

```csharp
public class SalesOrder { }
public interface IOrderRepository { }
public record OrderCreatedEvent { }
```

### Method Names

**Use PascalCase** for method names.  
**Use verb-noun pattern** to indicate action.

```csharp
public class OrderService
{
    public async Task<Order> CreateOrderAsync(CreateOrderCommand command) { }
    public async Task ApproveOrderAsync(Guid orderId) { }
    public void ValidateOrderItems(List<OrderItem> items) { }
    public bool IsValidCustomer(Guid customerId) { }
}
```

### Property Names

**Use PascalCase** for property names.

```csharp
public class Order
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Variable Names

**Use camelCase** for local variables and parameters.

```csharp
public void ProcessOrder(Order order)
{
    var customerId = order.CustomerId;
    var totalAmount = order.TotalAmount;
    var isApproved = order.Status == OrderStatus.Approved;
}
```

### Constants

**Use UPPER_SNAKE_CASE** for constants.

```csharp
public class OrderConstants
{
    public const decimal DEFAULT_TAX_RATE = 0.08m;
    public const int MAX_ORDER_ITEMS = 100;
    public const int RESERVATION_HOLD_HOURS = 72;
}
```

### Boolean Names

**Start with Is, Has, Can, Should** to indicate boolean values.

```csharp
public class Order
{
    public bool IsApproved { get; set; }
    public bool HasDiscount { get; set; }
    public bool CanBeCancelled { get; set; }
    public bool ShouldNotify { get; set; }
}
```

## Code Organization

### Clean Architecture Layers

```
API Layer (Controllers)
    ↓
Application Layer (Commands, Queries, Handlers, Services)
    ↓
Domain Layer (Entities, Aggregates, Value Objects, Domain Events)
    ↓
Infrastructure Layer (Repositories, External Services, Data Access)
```

### Project Structure

```
Service/
├── API/
│   └── Controllers/
│       ├── OrdersController.cs
│       ├── CustomersController.cs
│       └── ReportsController.cs
├── Application/
│   ├── Commands/
│   │   ├── CreateOrderCommand.cs
│   │   └── ApproveOrderCommandHandler.cs
│   ├── Queries/
│   │   ├── GetOrderQuery.cs
│   │   └── GetOrderQueryHandler.cs
│   ├── Services/
│   │   ├── IOrderService.cs
│   │   └── OrderService.cs
│   └── DTOs/
│       ├── CreateOrderRequest.cs
│       └── OrderResponse.cs
├── Domain/
│   ├── Aggregates/
│   │   └── Order/
│   │       ├── Order.cs
│   │       ├── OrderItem.cs
│   │       └── OrderStatus.cs
│   ├── ValueObjects/
│   │   ├── Money.cs
│   │   └── Address.cs
│   ├── Events/
│   │   ├── OrderCreatedEvent.cs
│   │   └── OrderApprovedEvent.cs
│   └── Exceptions/
│       ├── OrderNotFoundException.cs
│       └── InvalidOrderStateException.cs
├── Infrastructure/
│   ├── Repositories/
│   │   ├── IOrderRepository.cs
│   │   └── OrderRepository.cs
│   ├── Services/
│   │   ├── IInventoryService.cs
│   │   └── InventoryService.cs
│   └── Persistence/
│       ├── OrderContext.cs
│       └── Migrations/
└── Tests/
    ├── Unit/
    │   └── Domain/
    └── Integration/
        └── Repositories/
```

## Coding Patterns

### Domain-Driven Design (DDD)

#### Aggregate Root
```csharp
public class Order : AggregateRoot
{
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    private List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    
    public static Order Create(Guid customerId, List<OrderItem> items)
    {
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerId = customerId,
            _items = items
        };
        
        order.AddDomainEvent(new OrderCreatedEvent(order.OrderId, customerId));
        return order;
    }
    
    public void Approve()
    {
        if (Status != OrderStatus.Submitted)
            throw new InvalidOrderStateException("Order cannot be approved in this state");
        
        Status = OrderStatus.Approved;
        AddDomainEvent(new OrderApprovedEvent(OrderId));
    }
}
```

#### Value Object
```csharp
public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }
    
    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount must be non-negative");
        
        Amount = amount;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
    }
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
    
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot add different currencies");
        return new Money(left.Amount + right.Amount, left.Currency);
    }
}
```

### CQRS Pattern (via MediatR)

#### Command
```csharp
public class CreateOrderCommand : IRequest<OrderResponse>
{
    public Guid CustomerId { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; }
    public string ShippingAddress { get; set; }
}
```

#### Command Handler
```csharp
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IEventBus _eventBus;
    
    public async Task<OrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Validate
        var customer = await ValidateCustomerAsync(request.CustomerId, cancellationToken);
        
        // Create aggregate
        var order = Order.Create(request.CustomerId, MapToOrderItems(request.Items));
        
        // Save to repository
        await _orderRepository.AddAsync(order, cancellationToken);
        
        // Publish domain events
        foreach (var domainEvent in order.GetDomainEvents())
        {
            await _eventBus.PublishAsync(domainEvent, cancellationToken);
        }
        
        return MapToResponse(order);
    }
}
```

#### Query
```csharp
public class GetOrderQuery : IRequest<OrderResponse>
{
    public Guid OrderId { get; set; }
}
```

#### Query Handler
```csharp
public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderResponse>
{
    private readonly IOrderRepository _orderRepository;
    
    public async Task<OrderResponse> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        
        if (order == null)
            throw new OrderNotFoundException(request.OrderId);
        
        return MapToResponse(order);
    }
}
```

### Dependency Injection

Register services in startup:
```csharp
public static IServiceCollection AddOrderServices(this IServiceCollection services)
{
    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IOrderService, OrderService>();
    services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
    
    services.AddMediatR(typeof(CreateOrderCommandHandler));
    
    return services;
}
```

### Validation

Use FluentValidation for input validation:
```csharp
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required");
        
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Order must contain at least one item")
            .Must(items => items.Count <= 100).WithMessage("Order cannot contain more than 100 items");
        
        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Quantity must be greater than 0");
                
                item.RuleFor(x => x.UnitPrice)
                    .GreaterThan(0).WithMessage("Price must be greater than 0");
            });
    }
}
```

### Error Handling

Create custom exceptions in Domain layer:
```csharp
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public class OrderNotFoundException : DomainException
{
    public Guid OrderId { get; }
    
    public OrderNotFoundException(Guid orderId) 
        : base($"Order with ID {orderId} was not found")
    {
        OrderId = orderId;
    }
}

public class InvalidOrderStateException : DomainException
{
    public InvalidOrderStateException(string message) : base(message) { }
}
```

Use in controllers:
```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder(CreateOrderCommand command)
{
    try
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetOrder), new { id = result.OrderId }, result);
    }
    catch (DomainException ex)
    {
        return BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating order");
        return StatusCode(500, new { error = "An unexpected error occurred" });
    }
}
```

## Async/Await Patterns

Always use async patterns for I/O operations:

```csharp
// GOOD: Async
public async Task<Order> GetOrderAsync(Guid orderId)
{
    return await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
}

// BAD: Blocking
public Order GetOrder(Guid orderId)
{
    return _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
}
```

Use `ConfigureAwait(false)` in libraries:
```csharp
public async Task<Order> GetOrderAsync(Guid orderId)
{
    return await _context.Orders
        .FirstOrDefaultAsync(o => o.OrderId == orderId)
        .ConfigureAwait(false);
}
```

## Logging

Use structured logging with context:

```csharp
public class OrderService
{
    private readonly ILogger<OrderService> _logger;
    
    public async Task<Order> CreateOrderAsync(CreateOrderCommand command)
    {
        _logger.LogInformation("Creating order for customer {CustomerId}", command.CustomerId);
        
        try
        {
            var order = Order.Create(command.CustomerId, items);
            await _orderRepository.AddAsync(order);
            
            _logger.LogInformation("Order {OrderId} created successfully for customer {CustomerId}", 
                order.OrderId, command.CustomerId);
            
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for customer {CustomerId}", command.CustomerId);
            throw;
        }
    }
}
```

## Code Style

### Formatting

- **Indentation**: 4 spaces
- **Line length**: Maximum 120 characters (where practical)
- **Braces**: Always use braces for blocks, even single-line

```csharp
// GOOD
if (isValid)
{
    ProcessOrder(order);
}

// BAD
if (isValid) ProcessOrder(order);
```

### Using Statements

Group using statements:
```csharp
// System namespaces first
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Then third-party
using MediatR;
using FluentValidation;

// Then application namespaces
using ERP.Sales.Domain.Aggregates;
using ERP.Sales.Application.Commands;
```

### Null Handling

Use null-coalescing and null-conditional operators:
```csharp
// Use null-coalescing operator
var city = address?.City ?? "Unknown";

// Use null-conditional operator
var itemCount = order?.Items?.Count ?? 0;

// Check explicitly when needed
if (order == null)
    throw new ArgumentNullException(nameof(order));
```

### String Interpolation

Use string interpolation instead of concatenation:
```csharp
// GOOD
var message = $"Order {orderId} created at {DateTime.UtcNow}";

// BAD
var message = "Order " + orderId + " created at " + DateTime.UtcNow;
```

## Testing Standards

### Unit Tests

Test domain logic and business rules:
```csharp
[Fact]
public void Order_Approve_ChangesStatusToApproved()
{
    // Arrange
    var order = CreateTestOrder();
    
    // Act
    order.Approve();
    
    // Assert
    Assert.Equal(OrderStatus.Approved, order.Status);
}

[Fact]
public void Order_Approve_ThrowsWhenNotInSubmittedState()
{
    // Arrange
    var order = CreateTestOrder(OrderStatus.Draft);
    
    // Act & Assert
    Assert.Throws<InvalidOrderStateException>(() => order.Approve());
}
```

### Integration Tests

Test repository operations and database access:
```csharp
[Fact]
public async Task OrderRepository_Add_PersistsOrder()
{
    // Arrange
    var order = CreateTestOrder();
    
    // Act
    await _repository.AddAsync(order);
    
    // Assert
    var retrievedOrder = await _repository.GetByIdAsync(order.OrderId);
    Assert.NotNull(retrievedOrder);
    Assert.Equal(order.OrderId, retrievedOrder.OrderId);
}
```

### E2E Tests

Test complete workflows:
```csharp
[Fact]
public async Task CreateOrder_ApproveOrder_UpdatesStatus()
{
    // Arrange
    var createCommand = new CreateOrderCommand { /* ... */ };
    
    // Act
    var createResult = await _mediator.Send(createCommand);
    var approveCommand = new ApproveOrderCommand { OrderId = createResult.OrderId };
    var approveResult = await _mediator.Send(approveCommand);
    
    // Assert
    Assert.Equal(OrderStatus.Approved, approveResult.Status);
}
```

## Code Review Checklist

Before submitting PR, verify:

- [ ] Code follows naming conventions
- [ ] Code is organized according to Clean Architecture
- [ ] No direct database access outside repositories
- [ ] Async/await used for all I/O operations
- [ ] Proper error handling with meaningful messages
- [ ] Input validation using FluentValidation
- [ ] Logging includes relevant context
- [ ] No hardcoded strings or magic numbers
- [ ] Comments explain "why" not "what"
- [ ] Unit/integration tests added for new features
- [ ] No commented-out code
- [ ] No unused imports or variables

## Tools & EditorConfig

Use `.editorconfig` for consistency:
```ini
root = true

[*.cs]
indent_size = 4
indent_style = space
max_line_length = 120

[*.{json,yml,yaml}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false
```

## References

- [Microsoft C# Naming Guidelines](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Clean Code by Robert C. Martin](https://www.amazon.com/Clean-Code-Handbook-Software-Craftsmanship/dp/0132350882)
- [Domain-Driven Design by Eric Evans](https://www.domainlanguage.com/ddd/)

---

**Last Updated**: 2026-06-13  
**Version**: 1.0.0
