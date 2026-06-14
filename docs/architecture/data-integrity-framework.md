# Data Integrity Framework for ERP E-Commerce Solution

## Overview

This document defines the data integrity patterns for all 14 microservices to ensure:
- ✅ **Validation Rules** - Business logic enforcement
- ✅ **Audit Trail** - Complete change tracking
- ✅ **Optimistic Locking** - Race condition prevention
- ✅ **Tenant Isolation** - Multi-tenant data separation

---

## 1. Optimistic Locking Pattern

### Problem
Without version control, concurrent updates can cause data loss:
```
User A reads PO v1 (TotalAmount=1000)
User B reads PO v1 (TotalAmount=1000)
User B updates PO v2 (TotalAmount=1200) ← DB writes successfully
User A updates PO v2 (TotalAmount=1000) ← Overwrites B's change silently!
```

### Solution: Add Version Field to BaseEntity

**File:** `src/Shared/Core/BaseEntity.cs`

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }  // ← NEW: Tenant isolation
    public string? Description { get; set; }
    public bool IsDeleted { get; protected set; } = false;
    
    // Audit Trail
    public string? CreatedBy { get; protected set; }
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; protected set; }
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;
    
    // Optimistic Locking
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();  // ← NEW: Version control
    
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### EF Core Configuration

**File:** `src/Shared/Infrastructure/EntityTypeConfiguration.cs`

```csharp
public class BaseEntityConfiguration : IEntityTypeConfiguration<BaseEntity>
{
    public void Configure(EntityTypeBuilder<BaseEntity> builder)
    {
        // TenantId as part of composite primary key for isolation
        builder.HasKey(e => new { e.Id, e.TenantId });
        
        // RowVersion for Optimistic Locking (SQL Server specific)
        builder.Property(e => e.RowVersion)
            .IsRowVersion();
        
        // Audit fields
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAdd();
        
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAddOrUpdate();
        
        // Soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
```

### Usage in EF Core SaveAsync

```csharp
try
{
    await _dbContext.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateConcurrencyException ex)
{
    // Handle: Another user updated this record. Please refresh and try again.
    throw new ConcurrencyException(
        "Data has been modified by another user. Please refresh and try again.",
        ex);
}
```

---

## 2. Tenant Isolation Pattern

### Problem
Without tenant filtering, data from Tenant A is visible to Tenant B:
```sql
-- User from TenantA queries
SELECT * FROM PurchaseOrders  -- Could return TenantB's data!
```

### Solution: Automatic Tenant Filtering

**File:** `src/Shared/Infrastructure/TenantFilterConfiguration.cs`

```csharp
public class TenantFilterConfiguration
{
    public static void ConfigureTenantFilter(ModelBuilder builder, string tenantId)
    {
        var entityTypes = builder.Model.GetEntityTypes();
        
        foreach (var entityType in entityTypes)
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var propertyAccess = Expression.Property(parameter, nameof(BaseEntity.TenantId));
                var tenantFilter = Expression.Equal(
                    propertyAccess,
                    Expression.Constant(new Guid(tenantId)));
                var lambda = Expression.Lambda(tenantFilter, parameter);
                
                entityType.SetQueryFilter(lambda);
            }
        }
    }
}
```

**Usage in DbContext:**

```csharp
public class PurchasingDbContext : DbContext
{
    private readonly string _tenantId;
    
    public PurchasingDbContext(DbContextOptions options, IUserContextService userContext)
        : base(options)
    {
        _tenantId = userContext.GetTenantId() 
            ?? throw new InvalidOperationException("TenantId is required");
    }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        TenantFilterConfiguration.ConfigureTenantFilter(builder, _tenantId);
    }
}
```

---

## 3. Validation Rules Pattern

### Domain-Level Validation (Immutable Rules)

Business logic lives in domain entities:

**File:** `src/services/PurchaseOrderService/src/ERP.PO.Domain/Entities/PurchaseOrder.cs`

```csharp
public class PurchaseOrder : BaseEntity
{
    // ... properties ...
    
    public static PurchaseOrder Create(
        string poNumber,
        Guid supplierId,
        Guid tenantId,  // ← NEW: Tenant context
        DateTime? expectedDeliveryDate = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
            throw new ArgumentException("PO Number is required", nameof(poNumber));
        
        if (supplierId == Guid.Empty)
            throw new ArgumentException("Supplier ID is required", nameof(supplierId));
        
        if (expectedDeliveryDate.HasValue && expectedDeliveryDate < DateTime.UtcNow)
            throw new ArgumentException(
                "Expected delivery date must be in the future",
                nameof(expectedDeliveryDate));
        
        return new PurchaseOrder
        {
            PONumber = poNumber,
            SupplierId = supplierId,
            TenantId = tenantId,  // ← Set tenant
            ExpectedDeliveryDate = expectedDeliveryDate,
            Notes = notes,
            Status = PurchaseOrderStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    // ← NEW: Validation at submit time
    public void Submit()
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException(
                $"Only Draft orders can be submitted. Current status: {Status}");
        
        if (!Items.Any())
            throw new InvalidOperationException(
                "Cannot submit PO with no items");
        
        // Recalculate total for safety
        RecalculateTotal();
        
        Status = PurchaseOrderStatus.Submitted;
    }
}
```

### Application-Level Validation (Business Rules with External Dependencies)

**File:** `src/services/PurchaseOrderService/src/ERP.PO.Application/Features/PurchaseOrders/Validators/CreatePurchaseOrderCommandValidator.cs`

```csharp
public class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUserContextService _userContextService;
    
    public CreatePurchaseOrderCommandValidator(
        ISupplierRepository supplierRepository,
        IUserContextService userContextService)
    {
        _supplierRepository = supplierRepository;
        _userContextService = userContextService;
        
        // ← NEW: Async validation for supplier existence
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier ID is required.")
            .MustAsync(SupplierMustExist, "Supplier does not exist.")
            .MustAsync(SupplierMustBeActive, "Supplier is inactive.");
        
        RuleFor(x => x.PONumber)
            .NotEmpty().WithMessage("PO Number is required.")
            .MaximumLength(50).WithMessage("PO Number must not exceed 50 characters.")
            .MustAsync(PoNumberMustBeUnique, "PO Number already exists.");
        
        RuleFor(x => x.ExpectedDeliveryDate)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Expected delivery date must be in the future.")
            .When(x => x.ExpectedDeliveryDate.HasValue);
    }
    
    private async Task<bool> SupplierMustExist(Guid supplierId, CancellationToken cancellationToken)
    {
        var tenantId = _userContextService.GetTenantId();
        var supplier = await _supplierRepository.GetByIdAsync(supplierId, tenantId);
        return supplier != null;
    }
    
    private async Task<bool> SupplierMustBeActive(Guid supplierId, CancellationToken cancellationToken)
    {
        var tenantId = _userContextService.GetTenantId();
        var supplier = await _supplierRepository.GetByIdAsync(supplierId, tenantId);
        return supplier?.IsActive ?? false;
    }
    
    private async Task<bool> PoNumberMustBeUnique(string poNumber, CancellationToken cancellationToken)
    {
        var tenantId = _userContextService.GetTenantId();
        var existing = await _supplierRepository.GetByPoNumberAsync(poNumber, tenantId);
        return existing == null;
    }
}
```

---

## 4. Audit Trail Pattern

### Audit at Command Handler Level

**File:** `src/services/PurchaseOrderService/src/ERP.PO.Application/Features/PurchaseOrders/Handlers/CreatePurchaseOrderCommandHandler.cs`

```csharp
public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, PurchaseOrderDto>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;
    private readonly ILogger<CreatePurchaseOrderCommandHandler> _logger;
    
    public CreatePurchaseOrderCommandHandler(
        IRepository<PurchaseOrder> repository,
        IUserContextService userContextService,
        IMapper mapper,
        ILogger<CreatePurchaseOrderCommandHandler> logger)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
        _logger = logger;
    }
    
    public async Task<PurchaseOrderDto> Handle(
        CreatePurchaseOrderCommand request,
        CancellationToken cancellationToken)
    {
        // ← NEW: Get user context from gateway headers
        var user = _userContextService.GetCurrentUser();
        
        // Create aggregate root
        var purchaseOrder = PurchaseOrder.Create(
            request.PONumber,
            request.SupplierId,
            new Guid(user.TenantId),  // ← Set tenant
            request.ExpectedDeliveryDate,
            request.Notes);
        
        // ← NEW: Set audit trail
        purchaseOrder.CreatedBy = user.UserId;
        purchaseOrder.CreatedAt = DateTime.UtcNow;
        
        // Save to repository (EF Core)
        _repository.Add(purchaseOrder);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);
        
        // ← NEW: Audit log
        _logger.LogInformation(
            "Purchase Order created: {PONumber} by {UserId} for tenant {TenantId}",
            purchaseOrder.PONumber,
            user.UserId,
            user.TenantId);
        
        return _mapper.Map<PurchaseOrderDto>(purchaseOrder);
    }
}
```

### Audit Log Table (Optional for regulatory requirements)

```sql
CREATE TABLE AuditLogs (
    Id BIGINT PRIMARY KEY IDENTITY(1,1),
    EntityName NVARCHAR(255),
    EntityId UNIQUEIDENTIFIER,
    TenantId UNIQUEIDENTIFIER,
    Action NVARCHAR(50),  -- Create, Update, Delete
    UserId NVARCHAR(255),
    Changes NVARCHAR(MAX),  -- JSON: {CreatedBy: "john@example.com", Status: Draft->Submitted}
    CreatedAt DATETIME2,
    INDEX IX_EntityId (EntityId),
    INDEX IX_TenantId (TenantId),
    INDEX IX_CreatedAt (CreatedAt)
);
```

---

## 5. Repository Pattern with Tenant & Version Awareness

**File:** `src/Shared/Infrastructure/Repository/IRepository.cs`

```csharp
public interface IRepository<T> where T : BaseEntity
{
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
    
    Task<T?> GetByIdAsync(Guid id, Guid tenantId);
    Task<IEnumerable<T>> GetAllAsync(Guid tenantId);
    
    IUnitOfWork UnitOfWork { get; }
}

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    ITransaction BeginTransaction();
}
```

**Implementation:**

```csharp
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly DbContext _context;
    
    public Repository(DbContext context) => _context = context;
    
    public void Add(T entity) => _context.Set<T>().Add(entity);
    
    public void Update(T entity) => _context.Set<T>().Update(entity);
    
    public void Delete(T entity) => _context.Set<T>().Remove(entity);
    
    // ← TenantId is automatically filtered by DbContext.OnModelCreating
    public async Task<T?> GetByIdAsync(Guid id, Guid tenantId) =>
        await _context.Set<T>()
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);
    
    public async Task<IEnumerable<T>> GetAllAsync(Guid tenantId) =>
        await _context.Set<T>()
            .Where(e => e.TenantId == tenantId)
            .ToListAsync();
}
```

---

## 6. Exception Handling for Data Integrity

**File:** `src/Shared/Exceptions/DataIntegrityException.cs`

```csharp
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}

public class TenantIsolationException : Exception
{
    public TenantIsolationException(Guid entityId, Guid tenantId)
        : base($"Entity {entityId} is not accessible by tenant {tenantId}") { }
}

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }
    
    public ValidationException(Dictionary<string, string[]> errors)
    {
        Errors = errors;
    }
}
```

**Middleware to catch and handle:**

```csharp
app.UseExceptionMiddleware();  // Catches and returns proper HTTP status codes
```

---

## 7. Implementation Checklist per Service

For each of the 14 microservices, apply this checklist:

### Domain Layer
- [ ] Add `TenantId` to `BaseEntity`
- [ ] Add `[Timestamp] byte[] RowVersion` to `BaseEntity`
- [ ] Add validation to `Create()` factory methods
- [ ] Update aggregate root methods (Submit, Approve, etc.) with status checks
- [ ] Ensure `CreatedBy` is set at creation time

### Application Layer
- [ ] Create async validators for foreign key references
- [ ] Add async validators for uniqueness checks
- [ ] Inject `IUserContextService` into handlers
- [ ] Extract `TenantId` and `UserId` from user context
- [ ] Set `CreatedBy`, `UpdatedBy` from user context, NOT from request
- [ ] Add proper error handling for ConcurrencyException

### Infrastructure Layer
- [ ] Create `DbContext` with proper entity configurations
- [ ] Configure `TenantId` filtering in `OnModelCreating`
- [ ] Add `IRepository<T>` implementation
- [ ] Add `IUnitOfWork` implementation
- [ ] Configure `RowVersion` for optimistic locking

### API Layer
- [ ] No custom authorization logic (use shared `[AuthorizePermission]`)
- [ ] Use `IUserContextService.GetCurrentUser()` to extract tenant/user
- [ ] Pass tenant/user context to handlers, NOT in request DTOs
- [ ] Return proper error responses (400, 401, 403, 409 for concurrency)

---

## 8. Testing Data Integrity

### Unit Tests for Domain Logic

```csharp
[TestMethod]
public void CreatePurchaseOrder_WithEmptyPONumber_ThrowsException()
{
    // Arrange & Act & Assert
    var ex = Assert.ThrowsException<ArgumentException>(
        () => PurchaseOrder.Create("", Guid.NewGuid(), Guid.NewGuid()));
    
    Assert.IsTrue(ex.Message.Contains("PO Number is required"));
}

[TestMethod]
public void SubmitPurchaseOrder_WithNoItems_ThrowsException()
{
    // Arrange
    var po = PurchaseOrder.Create("PO-001", Guid.NewGuid(), Guid.NewGuid());
    
    // Act & Assert
    var ex = Assert.ThrowsException<InvalidOperationException>(
        () => po.Submit());
    
    Assert.IsTrue(ex.Message.Contains("no items"));
}
```

### Integration Tests for Concurrency

```csharp
[TestMethod]
public async Task UpdatePurchaseOrder_WithConcurrentChange_ThrowsConcurrencyException()
{
    // Arrange
    var tenantId = Guid.NewGuid();
    var poId = Guid.NewGuid();
    
    var context1 = CreateDbContext(tenantId);
    var context2 = CreateDbContext(tenantId);
    
    var po1 = await context1.PurchaseOrders.FirstAsync(p => p.Id == poId);
    var po2 = await context2.PurchaseOrders.FirstAsync(p => p.Id == poId);
    
    // Act
    po1.Status = PurchaseOrderStatus.Approved;
    await context1.SaveChangesAsync();  // ← Success
    
    po2.Status = PurchaseOrderStatus.Cancelled;
    
    // Assert
    await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(
        async () => await context2.SaveChangesAsync());
}
```

### Integration Tests for Tenant Isolation

```csharp
[TestMethod]
public async Task GetPurchaseOrders_WithDifferentTenant_ReturnsEmpty()
{
    // Arrange
    var tenant1Id = Guid.NewGuid();
    var tenant2Id = Guid.NewGuid();
    
    var context1 = CreateDbContext(tenant1Id);
    var context2 = CreateDbContext(tenant2Id);
    
    var po = PurchaseOrder.Create("PO-001", Guid.NewGuid(), tenant1Id);
    context1.PurchaseOrders.Add(po);
    await context1.SaveChangesAsync();
    
    // Act
    var posInTenant2 = await context2.PurchaseOrders.ToListAsync();
    
    // Assert
    Assert.AreEqual(0, posInTenant2.Count);  // Tenant 2 cannot see Tenant 1's data
}
```

---

## 9. Migration Strategy

### Phase 1: Update Shared Libraries (Week 1)
1. ✅ Update `ERP.Shared/Core/BaseEntity.cs` with `TenantId` and `RowVersion`
2. ✅ Create `ERP.Shared/Infrastructure/TenantFilterConfiguration.cs`
3. ✅ Create `ERP.Shared/Exceptions/DataIntegrityExceptions.cs`
4. ✅ Create `ERP.Shared/Infrastructure/Repository/IRepository.cs`

### Phase 2: Update PurchaseOrderService (Week 2)
1. Update `PurchaseOrder.Domain` entities
2. Create `PurchaseOrder.Infrastructure/Data/` with DbContext
3. Update command validators with async rules
4. Update command handlers with tenant/user context
5. Add tests

### Phase 3: Roll out to other 13 services (Weeks 3-5)
1. Use PurchaseOrderService as template
2. Parallel updates: 2-3 services per week
3. Integration tests before each service goes live

---

## 10. Monitoring & Auditing

### Log Important Events

```csharp
_logger.LogInformation(
    "PurchaseOrder {POId} created by {UserId} in tenant {TenantId}",
    purchaseOrder.Id,
    user.UserId,
    user.TenantId);

_logger.LogWarning(
    "Concurrency conflict on PurchaseOrder {POId}: version mismatch",
    purchaseOrder.Id);

_logger.LogError(
    "Tenant isolation violation: User {UserId} from {TenantId} attempted to access data from {UnauthorizedTenantId}",
    user.UserId,
    user.TenantId,
    attemptedTenantId);
```

### Metrics to Track
- `po.create.success` - Successful PO creation
- `po.create.validation_error` - Validation failures
- `po.concurrency_conflict` - Concurrent update conflicts
- `po.tenant_isolation_violation` - Unauthorized tenant access attempts

---

## Summary

| Aspect | Pattern | Implementation |
|--------|---------|-----------------|
| **Validation Rules** | Domain + FluentValidation | Create() methods + async validators |
| **Audit Trail** | CreatedBy/By, CreatedAt/At | Set in handlers from IUserContextService |
| **Optimistic Locking** | RowVersion + [Timestamp] | EF Core concurrency conflict handling |
| **Tenant Isolation** | TenantId + Query Filter | DbContext.OnModelCreating + auto-filter |

