# Testing Strategy

Comprehensive testing strategy and guidelines for the ERP E-Commerce Solution.

## Testing Pyramid

```
        ┌─────────────────┐
        │   E2E Tests     │  Slow, Expensive
        │   (Few)         │
        ├────────────────┤
        │ Integration     │  Medium
        │   Tests         │
        │  (Some)         │
        ├────────────────┤
        │  Unit Tests     │  Fast, Cheap
        │   (Many)        │
        └─────────────────┘
```

## Unit Tests

### Purpose
Test individual components in isolation with mocked dependencies.

### Tools
- **Framework**: xUnit
- **Mocking**: Moq
- **Assertions**: FluentAssertions

### Example: Domain Entity Test

```csharp
public class OrderTests
{
    [Fact]
    public void Order_Create_InitializesWithCorrectValues()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var items = new List<OrderItem> 
        { 
            new OrderItem(Guid.NewGuid(), 5, 100m) 
        };

        // Act
        var order = Order.Create(customerId, items);

        // Assert
        order.CustomerId.Should().Be(customerId);
        order.Items.Should().HaveCount(1);
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void Order_Approve_SucceedsWhenStatusIsSubmitted()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Submitted);

        // Act
        order.Approve();

        // Assert
        order.Status.Should().Be(OrderStatus.Approved);
    }

    [Fact]
    public void Order_Approve_ThrowsWhenStatusIsNotSubmitted()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Draft);

        // Act & Assert
        var exception = Assert.Throws<InvalidOrderStateException>(() => order.Approve());
        exception.Message.Should().Contain("cannot be approved");
    }

    private Order CreateTestOrder(OrderStatus status = OrderStatus.Draft)
    {
        var order = Order.Create(Guid.NewGuid(), new List<OrderItem>());
        
        if (status != OrderStatus.Draft)
        {
            // Use reflection to set status for testing
            var statusProperty = order.GetType().GetProperty("Status", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            statusProperty?.SetValue(order, status);
        }
        
        return order;
    }
}
```

### Example: Handler Test with Mocks

```csharp
public class CreateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<IInventoryService> _mockInventoryService;
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockInventoryService = new Mock<IInventoryService>();
        
        _handler = new CreateOrderCommandHandler(
            _mockOrderRepository.Object,
            _mockInventoryService.Object
        );
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesOrderAndPublishesEvent()
    {
        // Arrange
        var command = new CreateOrderCommand
        {
            CustomerId = Guid.NewGuid(),
            Items = new List<CreateOrderItemRequest>()
        };

        _mockOrderRepository
            .Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mockOrderRepository.Verify(
            x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

### Coverage Requirements
- **Minimum**: 80% code coverage
- **Target**: 85% code coverage
- Focus on critical business logic

### Running Unit Tests

```bash
# Run all unit tests
dotnet test --filter "Category=Unit"

# Run tests for specific class
dotnet test --filter "ClassName=OrderTests"

# Run with verbose output
dotnet test --verbosity detailed

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

## Integration Tests

### Purpose
Test interactions between components and external systems (database, message bus).

### Tools
- **Database**: TestContainers or LocalDB
- **Message Bus**: TestContainers (RabbitMQ)
- **Framework**: xUnit with IAsyncLifetime

### Example: Repository Test

```csharp
public class OrderRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly ApplicationDbContext _dbContext;
    private readonly OrderRepository _repository;

    public OrderRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;
        
        _dbContext = new ApplicationDbContext(options);
        _repository = new OrderRepository(_dbContext);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AddAsync_ValidOrder_PersistsToDatabase()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), new List<OrderItem>());

        // Act
        await _repository.AddAsync(order, CancellationToken.None);
        var retrievedOrder = await _repository.GetByIdAsync(order.OrderId, CancellationToken.None);

        // Assert
        retrievedOrder.Should().NotBeNull();
        retrievedOrder.OrderId.Should().Be(order.OrderId);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
```

### Example: Event Bus Test

```csharp
public class EventBusIntegrationTests : IAsyncLifetime
{
    private readonly ITestMessageBus _messageBus;
    private readonly EventBus _eventBus;

    public EventBusIntegrationTests()
    {
        // Initialize test RabbitMQ
        _messageBus = new TestMessageBus();
        _eventBus = new EventBus(_messageBus);
    }

    public async Task InitializeAsync()
    {
        await _messageBus.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _messageBus.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PublishAsync_ValidEvent_PublishesToBus()
    {
        // Arrange
        var @event = new OrderCreatedEvent
        {
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        var receivedEvents = new List<OrderCreatedEvent>();
        _messageBus.Subscribe<OrderCreatedEvent>(e => receivedEvents.Add(e));

        // Act
        await _eventBus.PublishAsync(@event, CancellationToken.None);
        await Task.Delay(100); // Wait for async processing

        // Assert
        receivedEvents.Should().ContainSingle();
        receivedEvents[0].OrderId.Should().Be(@event.OrderId);
    }
}
```

### Running Integration Tests

```bash
# Run all integration tests
dotnet test --filter "Category=Integration"

# Run specific test class
dotnet test --filter "ClassName=OrderRepositoryIntegrationTests"

# Run with SQL Server test instance
dotnet test --filter "Category=Integration" -e SqlServer=localdb
```

## E2E Tests

### Purpose
Test complete business workflows across multiple services.

### Example: Create Order Workflow

```csharp
public class OrderWorkflowE2ETests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrderWorkflowE2ETests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _factory.Dispose();
    }

    [Fact]
    public async Task OrderWorkflow_CreateApproveShip_Succeeds()
    {
        // Step 1: Register User
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "test@example.com", password = "Password123!" }
        );
        registerResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var userDto = await registerResponse.Content.ReadAsAsync<UserResponse>();

        // Step 2: Login
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "test@example.com", password = "Password123!" }
        );
        var loginDto = await loginResponse.Content.ReadAsAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginDto.AccessToken);

        // Step 3: Create Order
        var createOrderResponse = await _client.PostAsJsonAsync(
            "/api/v1/orders",
            new { customerId = userDto.UserId, items = new[] { /* ... */ } }
        );
        createOrderResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var orderDto = await createOrderResponse.Content.ReadAsAsync<OrderResponse>();

        // Step 4: Approve Order
        var approveResponse = await _client.PostAsync(
            $"/api/v1/orders/{orderDto.OrderId}/approve",
            null
        );
        approveResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Step 5: Ship Order
        var shipResponse = await _client.PostAsJsonAsync(
            $"/api/v1/orders/{orderDto.OrderId}/ship",
            new { trackingNumber = "TRACK123" }
        );
        shipResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Verify final state
        var getOrderResponse = await _client.GetAsync($"/api/v1/orders/{orderDto.OrderId}");
        var finalOrder = await getOrderResponse.Content.ReadAsAsync<OrderResponse>();
        finalOrder.Status.Should().Be("Shipped");
    }
}
```

## Performance Tests

### Purpose
Verify system performance meets requirements.

### Load Testing with k6

```javascript
// load-test.js
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  stages: [
    { duration: '5m', target: 100 },
    { duration: '10m', target: 100 },
    { duration: '5m', target: 0 }
  ],
  thresholds: {
    http_req_duration: ['p(99)<500', 'p(95)<300', 'p(90)<200'],
    http_req_failed: ['rate<0.1']
  }
};

export default function() {
  let response = http.get('http://localhost:5000/api/v1/orders');
  
  check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 200ms': (r) => r.timings.duration < 200
  });
}
```

Run: `k6 run load-test.js`

## Continuous Integration

### GitHub Actions Workflow

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      sqlserver:
        image: mcr.microsoft.com/mssql/server:2019-latest
        env:
          SA_PASSWORD: YourPassword123
          ACCEPT_EULA: Y
        options: >-
          --health-cmd="/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourPassword123 -Q 'SELECT 1'"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 3

    steps:
    - uses: actions/checkout@v2
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: 10.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Unit Tests
      run: dotnet test --filter "Category=Unit" --no-build
    
    - name: Integration Tests
      run: dotnet test --filter "Category=Integration" --no-build
      env:
        ConnectionStrings__TestDatabase: "Server=localhost;User Id=sa;Password=YourPassword123;"
    
    - name: Upload Coverage
      run: dotnet test /p:CollectCoverage=true
    
    - name: Upload Coverage Reports
      uses: codecov/codecov-action@v2
```

## Test Data Management

### Fixtures

```csharp
public class OrderFixtures
{
    public static Order CreateValidOrder()
    {
        return Order.Create(
            Guid.NewGuid(),
            new List<OrderItem>
            {
                new OrderItem(Guid.NewGuid(), 5, 100m),
                new OrderItem(Guid.NewGuid(), 3, 50m)
            }
        );
    }

    public static Order CreateOrderWithStatus(OrderStatus status)
    {
        var order = CreateValidOrder();
        // Set status via reflection or property
        return order;
    }
}
```

### Database Seeding

```csharp
public class DatabaseSeeder
{
    public static async Task SeedTestDataAsync(ApplicationDbContext context)
    {
        var customers = new[]
        {
            new Customer { CustomerId = Guid.NewGuid(), Name = "Customer 1" },
            new Customer { CustomerId = Guid.NewGuid(), Name = "Customer 2" }
        };

        context.Customers.AddRange(customers);
        await context.SaveChangesAsync();
    }
}
```

## Testing Checklist

Before each commit, verify:

- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Code coverage >= 80%
- [ ] No failed or skipped tests
- [ ] Performance metrics within acceptable range
- [ ] New features have test coverage
- [ ] Bug fixes have regression tests

## Test Naming Convention

**Format**: `{Method}_{Scenario}_{ExpectedResult}`

```csharp
// Good
[Fact]
public void Order_Approve_SucceedsWhenStatusIsSubmitted() { }

[Fact]
public void Order_Approve_ThrowsWhenStatusIsNotSubmitted() { }

// Bad
[Fact]
public void TestOrder() { }

[Fact]
public void OrderTest2() { }
```

## Running All Tests

```bash
# Run all tests
dotnet test

# Run with specific category
dotnet test --filter "Category=Unit|Category=Integration"

# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverageFormat=cobertura

# Run in parallel
dotnet test --parallel
```

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions](https://fluentassertions.com/)
- [TestContainers](https://www.testcontainers.org/)
- [k6 Performance Testing](https://k6.io/)

---

**Last Updated**: 2026-06-13  
**Version**: 1.0.0
