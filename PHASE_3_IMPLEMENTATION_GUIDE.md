# Phase 3: Business Logic Implementation Guide

**Objective:** Implement controllers, MediatR handlers, repositories, validators, and business logic across all 14 microservices.

**Timeline:** 3-4 weeks for comprehensive implementation | Starting with ProductService (no upstream dependencies)

---

## Implementation Scope

### What's Already Done (Phase 1 & 2)
✅ Project structure & .csproj files (4 fully scaffolded + 10 templates)  
✅ DbContext with Outbox pattern (all 14 services)  
✅ Basic domain entities with Create() factory methods (all scaffolded)  
✅ Kafka message bus & background job (all services)  
✅ Dependency injection setup (all services)  
✅ appsettings.json (all environments)  
✅ Architecture & business documentation (comprehensive)  

### What's Pending (Phase 3)
⏳ **API Controllers** — Create 200+ endpoints across 14 services  
⏳ **MediatR Handlers** — Command & query handlers (CQRS pattern)  
⏳ **FluentValidation** — Input validation for all commands  
⏳ **Repositories** — Data access layer for aggregates  
⏳ **Business Logic** — Domain logic in aggregate roots  
⏳ **Domain Events** — Event handler implementations  
⏳ **Integration Tests** — Test each workflow end-to-end  
⏳ **API Documentation** — Swagger endpoints updated with examples  

---

## Implementation Priority & Dependencies

### Tier 1: Upstream Services (No Dependencies) - Start Here

#### 1. ProductService (Port 5008, Code: PRD)
**Reason:** Foundational — all other services depend on product data  
**Key Endpoints:** 20+ (catalog, variants, pricing, images)  
**Key Entities:** Product, ProductVariant, Category, ProductImage, PriceList  
**Key Workflows:**
- Create product with variants
- Update product pricing
- Publish/discontinue products
- Bulk operations for catalog sync

**Implementation Order:**
1. **Phase 3.1:** Create Product Aggregate Root with business logic
2. **Phase 3.2:** Create ProductVariant, Category, PriceList entities
3. **Phase 3.3:** Implement 5 key commands: CreateProduct, UpdatePrice, PublishProduct, DiscontinueProduct, BulkImportProducts
4. **Phase 3.4:** Implement 5 key queries: GetProduct, ListProducts, SearchProducts, GetPriceList, GetVariants
5. **Phase 3.5:** Create controllers & Swagger documentation
6. **Phase 3.6:** Integration tests (product catalog workflows)

**Estimated Lines of Code:** 2,000-3,000 lines  
**Estimated Time:** 8-12 hours

---

#### 2. IdentityService (Port 5001)
**Reason:** Authentication foundation — used by all other services  
**Status:** Already mostly implemented (JWT, RBAC)  
**Remaining Work:** Complete user management endpoints, role assignment workflows

**Implementation Order:**
1. Create User Commands: RegisterUser, UpdateUserProfile, ChangePassword, ResetPassword
2. Create Role Commands: CreateRole, AssignRoleToUser, RevokeRole
3. Create queries: GetUser, GetRoles, ValidateToken
4. Implement authentication middleware (JWT validation)
5. Integration tests (auth workflows)

**Estimated Time:** 6-8 hours

---

### Tier 2: Core Business Services (Depend on Tier 1)

#### 3. SaleOrderService (Port 5002)
**Reason:** Revenue stream — depends on Product, depends on Warehouse/Payment/CRM  
**Key Commands:**
- CreateSalesOrder (with line items)
- ApproveSalesOrder
- CancelSalesOrder
- UpdateOrderStatus

**Key Queries:**
- GetSalesOrder
- ListOrdersByCustomer
- SearchOrders
- GetOrderStatus

**Workflows:**
1. CreateOrder → Validate products exist (call ProductService) → Create order in SalesDB
2. ApproveOrder → Publish SalesOrderCreated → Warehouse listens & reserves inventory
3. Payment received → Update order status → Warehouse ships → Notification sent

**Estimated Time:** 12-16 hours

---

#### 4. WareHouseService (Port 5004)
**Reason:** Fulfillment backbone — depends on Product, depended on by Sales/Purchasing  
**Key Commands:**
- ReserveInventory
- ReleaseReservation
- RecordStockMovement
- ReceiveGoods
- AdjustInventory

**Key Queries:**
- GetInventoryBalance
- CheckAvailability
- GetStockMovementHistory
- GetLowStockItems

**Workflows:**
1. SalesOrderCreated → Validate stock available → Create reservation
2. PurchaseOrderCreated → Prepare receiving location
3. ReceiveGoods → Update inventory → Release receiving location

**Estimated Time:** 12-16 hours

---

#### 5. AccountingService (Port 5005)
**Reason:** Financial backbone — depends on Sales, Purchasing, Payment  
**Key Commands:**
- CreateInvoice (from SalesOrder)
- CreateBill (from PurchaseOrder)
- PostJournalEntry
- RecordPayment
- CloseFiscalPeriod

**Key Queries:**
- GetGLBalance
- GetARAgingReport
- GetAPAgingReport
- GetJournalEntryDetails
- ValidateGLBalance

**Workflows:**
1. SalesOrderCreated → Create invoice → Post to GL
2. PaymentReceived → Record payment → Update invoice status
3. PurchaseOrderCreated → Create bill → Post to AP ledger

**Estimated Time:** 14-18 hours

---

### Tier 3: Support Services (Multiple Dependencies)

#### 6. CRMService (Port 5006)
**Commands:** CreateLead, QualifyLead, CreateOpportunity, CloseOpportunity, CreateCase, ResolvCase  
**Estimated Time:** 10-12 hours

#### 7. PaymentService (Port 5009)
**Commands:** InitiatePayment, AuthorizePayment, CapturePayment, RefundPayment, ReconcileSettlement  
**Estimated Time:** 12-14 hours

#### 8. ShippingService (Port 5010)
**Commands:** CreateShipment, AssignCarrier, UpdateTracking, ConfirmDelivery  
**Estimated Time:** 8-10 hours

#### 9. PromotionService (Port 5012)
**Commands:** CreatePromotion, RedeemCoupon, EarnLoyaltyPoints, RedeemLoyalty  
**Estimated Time:** 8-10 hours

#### 10. HRMService (Port 5007)
**Commands:** HireEmployee, ApproveLeaveRequest, GeneratePaySlip, RecordAttendance  
**Estimated Time:** 8-10 hours

#### 11. PurchaseOrderService (Port 5003)
**Commands:** CreatePO, ApprovePO, ReceiveGoods, CancelPO  
**Estimated Time:** 8-10 hours

---

### Tier 4: Specialized Services (Consumer-Only or Analytics)

#### 12. NotificationService (Port 5013)
**Purpose:** Consumer of all 13 services' events → multi-channel delivery  
**Implementation:** Event handler registration for all 13 topics → Email/SMS/Push dispatch  
**Estimated Time:** 8-10 hours

#### 13. ReportingService (Port 5014)
**Purpose:** Read-only data warehouse consumer  
**Implementation:** Kafka consumer → denormalize to fact/dimension tables → expose for BI queries  
**Estimated Time:** 6-8 hours

#### 14. POSService (Port 5011)
**Commands:** CreateTransaction, OpenCashShift, CloseCashShift, BalanceRegister  
**Queries:** GetTerminalStatus, GetTransactionHistory, GetCashShiftReport  
**Estimated Time:** 10-12 hours

---

## Implementation Pattern (Per Service)

### Step 1: Domain Entity Enhancement

**File:** `src/ERP.{CODE}.Domain/Entities/{AggregateRoot}.cs`

```csharp
public class Product : AggregateRoot
{
    // Properties already defined in Phase 1
    public string ProductCode { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public ProductStatus Status { get; set; }

    // Business Logic Methods (add in Phase 3)
    public void Publish()
    {
        if (Status != ProductStatus.Draft)
            throw new InvalidOperationException("Only draft products can be published");
        
        Status = ProductStatus.Active;
        AddDomainEvent(new ProductPublishedDomainEvent(this.Id, DateTime.UtcNow));
    }

    public void Discontinue()
    {
        if (Status == ProductStatus.Discontinued)
            throw new InvalidOperationException("Product already discontinued");
        
        Status = ProductStatus.Discontinued;
        AddDomainEvent(new ProductDiscontinuedDomainEvent(this.Id, DateTime.UtcNow));
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new ArgumentException("Price must be positive");
        
        var oldPrice = Price;
        Price = newPrice;
        AddDomainEvent(new ProductPriceChangedDomainEvent(this.Id, oldPrice, newPrice));
    }
}
```

---

### Step 2: Commands & Handlers (CQRS via MediatR)

**File:** `src/ERP.{CODE}.Application/Features/Products/CreateProductCommand.cs`

```csharp
public class CreateProductCommand : IRequest<CreateProductResponse>
{
    public string ProductCode { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
}

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.ProductCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}

public class CreateProductHandler : IRequestHandler<CreateProductCommand, CreateProductResponse>
{
    private readonly IRepository<Product> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<CreateProductResponse> Handle(CreateProductCommand request, CancellationToken ct)
    {
        // Validate category exists
        var category = await _repository.GetAsync(x => x.Id == request.CategoryId, ct);
        if (category == null)
            throw new BusinessException("Category not found");

        // Create product
        var product = Product.Create(
            request.ProductCode,
            request.Name,
            request.Description,
            request.Price,
            request.CategoryId
        );

        // Save
        await _repository.AddAsync(product, ct);
        await _unitOfWork.CommitAsync(ct);

        return new CreateProductResponse { ProductId = product.Id };
    }
}
```

**File:** `src/ERP.{CODE}.Application/Features/Products/GetProductQuery.cs`

```csharp
public class GetProductQuery : IRequest<ProductDto>
{
    public Guid ProductId { get; set; }
}

public class GetProductHandler : IRequestHandler<GetProductQuery, ProductDto>
{
    private readonly IRepository<Product> _repository;

    public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken ct)
    {
        var product = await _repository.GetAsync(x => x.Id == request.ProductId, ct);
        if (product == null)
            throw new NotFoundException("Product not found");

        return new ProductDto
        {
            Id = product.Id,
            ProductCode = product.ProductCode,
            Name = product.Name,
            Price = product.Price,
            Status = product.Status
        };
    }
}
```

---

### Step 3: Validators (FluentValidation)

**File:** `src/ERP.{CODE}.Application/Features/Products/UpdateProductPriceValidator.cs`

```csharp
public class UpdateProductPriceValidator : AbstractValidator<UpdateProductPriceCommand>
{
    public UpdateProductPriceValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required");

        RuleFor(x => x.NewPrice)
            .GreaterThan(0).WithMessage("Price must be greater than 0")
            .LessThan(99999.99M).WithMessage("Price exceeds maximum");
    }
}
```

---

### Step 4: Controllers (API Layer)

**File:** `src/ERP.{CODE}.API/Controllers/ProductsController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetProduct), new { id = result.ProductId }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var query = new GetProductQuery { ProductId = id };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPut("{id}/price")]
    public async Task<IActionResult> UpdatePrice(Guid id, [FromBody] decimal newPrice)
    {
        var command = new UpdateProductPriceCommand { ProductId = id, NewPrice = newPrice };
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPost("{id}/publish")]
    public async Task<IActionResult> PublishProduct(Guid id)
    {
        var command = new PublishProductCommand { ProductId = id };
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> ListProducts([FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        var query = new ListProductsQuery { Skip = skip, Take = take };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
```

---

### Step 5: Repositories

**File:** `src/ERP.{CODE}.Infrastructure/Repositories/ProductRepository.cs`

```csharp
public class ProductRepository : IRepository<Product>
{
    private readonly ProductDbContext _context;

    public async Task AddAsync(Product entity, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(entity, cancellationToken);
    }

    public async Task<Product> GetAsync(
        Expression<Func<Product, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IEnumerable<Product>> GetAllAsync(
        Expression<Func<Product, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Product> query = _context.Products;
        if (predicate != null)
            query = query.Where(predicate);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        _context.Products.Update(entity);
    }

    public async Task DeleteAsync(Product entity, CancellationToken cancellationToken = default)
    {
        entity.IsDeleted = true;
        _context.Products.Update(entity);
    }
}
```

---

### Step 6: Integration Tests

**File:** `tests/integration/ProductService.IntegrationTests/CreateProductTests.cs`

```csharp
public class CreateProductTests : IClassFixture<ProductServiceWebFactory>
{
    private readonly HttpClient _client;

    public CreateProductTests(ProductServiceWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProduct_WithValidData_ReturnsCreated()
    {
        // Arrange
        var command = new
        {
            productCode = "PROD-001",
            name = "Test Product",
            price = 99.99m,
            categoryId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", command);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadAsAsync<CreateProductResponse>();
        Assert.NotEqual(Guid.Empty, result.ProductId);
    }

    [Fact]
    public async Task CreateProduct_WithInvalidPrice_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            productCode = "PROD-002",
            name = "Test Product",
            price = 0, // Invalid
            categoryId = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", command);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

---

## File Count Estimate (Per Service)

For a fully-implemented service like ProductService:

| Component | Files | Examples |
|-----------|-------|----------|
| Domain | 8-12 | Product.cs, ProductVariant.cs, Category.cs, enums, value objects, events |
| Application | 15-20 | Commands, handlers, validators, queries, DTOs |
| API | 3-5 | ProductsController.cs, Startup.cs, ErrorHandler.cs |
| Infrastructure | 5-8 | ProductRepository.cs, ProductDbContext.cs, EventHandler.cs |
| Tests | 8-12 | Unit tests, integration tests |
| **Total Per Service** | **40-60 files** | Production-ready implementation |

---

## Phase 3 Timeline

| Week | Services | Focus | Deliverables |
|------|----------|-------|--------------|
| **Week 1** | ProductService, IdentityService | Core implementation | 100+ endpoints, domain logic |
| **Week 2** | SalesService, WarehouseService, AccountingService | Tier 2 implementation | Complex workflows, integrations |
| **Week 3** | CRM, Payment, Shipping, Promotion | Tier 3 implementation | Multi-service dependencies |
| **Week 4** | HRM, Purchasing, Notification, Reporting, POS | Remaining services | Consumer patterns, data warehouse |

**Total:** ~3-4 weeks for comprehensive implementation (600+ files, 50,000+ lines of code)

---

## Critical Success Factors

1. ✅ **Consistent Patterns** — All services follow the same 5-step implementation pattern
2. ✅ **Strong Typing** — DTOs, commands, queries with full validation
3. ✅ **Domain-Driven Design** — Business logic in aggregates, not anemic models
4. ✅ **Event Publishing** — All state changes trigger domain events (for Outbox pattern)
5. ✅ **Comprehensive Tests** — Unit + integration tests for all workflows
6. ✅ **API Documentation** — Swagger specs with real examples
7. ✅ **Error Handling** — Consistent exception mapping, meaningful error responses
8. ✅ **Logging & Tracing** — Correlation IDs for end-to-end debugging

---

## Next Steps

1. ✅ **ProductService Implementation** — Start with catalog CRUD operations
2. ✅ **Test ProductService** — Verify integration with other services
3. ✅ **Replicate Pattern** — Use ProductService as template for other services
4. ✅ **Parallel Implementation** — Develop multiple services in parallel
5. ✅ **Integration Testing** — Test Order-to-Cash workflow end-to-end
6. ✅ **Performance Testing** — Load test under realistic transaction volumes
7. ✅ **Documentation** — API documentation, runbooks, troubleshooting guides
8. ✅ **Deployment** — Docker, Kubernetes, CI/CD pipeline setup

---

**Created:** 2026-06-13  
**Phase:** 3 of 3  
**Status:** Ready for Implementation  
**Estimated Effort:** 600+ hours | 50,000+ lines of code | 14 services fully functional
