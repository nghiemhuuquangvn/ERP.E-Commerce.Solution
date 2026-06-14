# PO Service - Caching Implementation Guide

## Overview

The PO Service includes **comprehensive caching support** with both **Memory Cache** and **Redis Cache** implementations.

**Status**: ✅ **Build Successful** - All packages configured and integrated

---

## 🔄 Caching Architecture

### Dual Caching Strategy

```
┌─────────────────────────────────────────┐
│         API Layer (Controllers)         │
└──────────────────┬──────────────────────┘
                   │ Inject ICacheService
                   ↓
┌─────────────────────────────────────────┐
│     ICacheService Interface             │
│  (Abstraction for cache providers)      │
└───────┬─────────────────────────────────┘
        │
        ├─→ MemoryCacheService (Development)
        │   ├─ Thread-safe dictionary
        │   ├─ Configurable TTL
        │   └─ Pattern-based invalidation
        │
        └─→ RedisCacheService (Production)
            ├─ Distributed cache
            ├─ JSON serialization
            ├─ Namespaced keys
            └─ Pattern-based invalidation
```

---

## 📦 Caching Services

### 1. **ICacheService** (Interface)
Base abstraction for all cache operations:

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
    Task RemoveByPatternAsync(string pattern);
}
```

### 2. **MemoryCacheService**
In-process caching for development and single-instance deployments:

**Features:**
- Thread-safe with lock-based synchronization
- Automatic 1-hour expiration (configurable)
- Pattern-based cache invalidation
- Zero external dependencies

**Use Case:** Development, testing, single-node deployments

**Example:**
```csharp
// Registered automatically in Development
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
```

### 3. **RedisCacheService**
Distributed caching for production and multi-node deployments:

**Features:**
- Distributed cache across multiple nodes
- JSON serialization for data persistence
- Namespaced keys (prefix: `po_service:`)
- Pattern-based wildcard invalidation
- TTL support

**Use Case:** Production, distributed systems, multi-instance deployments

**Example:**
```csharp
// Registered automatically in Production
var redis = ConnectionMultiplexer.Connect("redis-service:6379");
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<ICacheService>(sp =>
    new RedisCacheService(sp.GetRequiredService<IConnectionMultiplexer>()));
```

---

## ⚙️ Configuration

### appsettings.json (Base)
```json
{
  "CacheSettings": {
    "Type": "Memory",
    "RedisConnection": "localhost:6379"
  }
}
```

### appsettings.Development.json
```json
{
  "CacheSettings": {
    "Type": "Memory"
  }
}
```

### appsettings.Production.json
```json
{
  "CacheSettings": {
    "Type": "Redis",
    "RedisConnection": "redis-service:6379"
  }
}
```

### Environment-Based Selection
```csharp
var cacheType = cacheSettings["Type"] ?? "Memory";

if (cacheType.Equals("Redis", StringComparison.OrdinalIgnoreCase))
{
    // Use Redis
}
else
{
    // Use Memory Cache (default)
}
```

---

## 🔑 Cache Keys

Standard cache key patterns defined in `CacheKeys` utility class:

### Purchase Order Keys
```csharp
// Single PO cache
PurchaseOrderPrefix = "po"
GetPurchaseOrderKey(Guid id)                    // po:550e8400...
GetPurchaseOrdersBySupplierKey(Guid supplierId) // po:supplier:550e8400...
GetPurchaseOrdersByStatusKey(string status)     // po:status:Approved
```

### Supplier Keys
```csharp
// Single supplier cache
SupplierPrefix = "supplier"
GetSupplierKey(Guid id)     // supplier:550e8400...
GetSupplierListKey()         // supplier:list
GetActiveSupplierKey()       // supplier:active
```

### Invalidation Patterns
```csharp
InvalidatePurchaseOrderPattern()  // po:*     (all POs)
InvalidateSupplierPattern()        // supplier:* (all suppliers)
```

---

## 📝 Usage Examples

### Using in Query Handlers

```csharp
public class GetPurchaseOrderByIdQueryHandler : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto>
{
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;

    public GetPurchaseOrderByIdQueryHandler(ICacheService cacheService, IMapper mapper)
    {
        _cacheService = cacheService;
        _mapper = mapper;
    }

    public async Task<PurchaseOrderDto> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.GetPurchaseOrderKey(request.PurchaseOrderId);

        // Try to get from cache
        var cached = await _cacheService.GetAsync<PurchaseOrderDto>(cacheKey);
        if (cached != null)
        {
            _logger.LogInformation("Cache hit for PO {PoId}", request.PurchaseOrderId);
            return cached;
        }

        // Not in cache, fetch from database
        var po = await _repository.GetByIdAsync(request.PurchaseOrderId);
        var dto = _mapper.Map<PurchaseOrderDto>(po);

        // Cache for 30 minutes
        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(30));

        return dto;
    }
}
```

### Cache Invalidation on Write

```csharp
public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, PurchaseOrderDto>
{
    private readonly ICacheService _cacheService;

    public async Task<PurchaseOrderDto> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        // Create PO
        var purchaseOrder = PurchaseOrder.Create(...);

        // Invalidate supplier's PO list cache
        await _cacheService.RemoveByPatternAsync($"po:supplier:{request.SupplierId}*");

        return _mapper.Map<PurchaseOrderDto>(purchaseOrder);
    }
}
```

### Cache Invalidation Patterns

```csharp
// Invalidate specific PO
await _cacheService.RemoveAsync(CacheKeys.GetPurchaseOrderKey(poId));

// Invalidate all POs for a supplier
await _cacheService.RemoveByPatternAsync($"po:supplier:{supplierId}*");

// Invalidate by status
await _cacheService.RemoveByPatternAsync("po:status:*");

// Invalidate all POs
await _cacheService.RemoveByPatternAsync(CacheKeys.InvalidatePurchaseOrderPattern());

// Invalidate all suppliers
await _cacheService.RemoveByPatternAsync(CacheKeys.InvalidateSupplierPattern());
```

---

## 🚀 Performance Benefits

### Memory Cache
- ✅ **Ultra-fast** - In-process, no I/O
- ✅ **Zero latency** - Direct memory access
- ✅ **Dev-friendly** - No external service required
- ⚠️ Limited to single instance

### Redis Cache
- ✅ **Distributed** - Shared across instances
- ✅ **Persistent** - Survives application restarts
- ✅ **Scalable** - Handles high load
- ✅ **Multi-node** - Works in Kubernetes
- ⚠️ Slight network overhead

### Benchmarks (Typical)
| Operation | Memory Cache | Redis Cache |
|-----------|-------------|------------|
| Get | < 1ms | 1-5ms |
| Set | < 1ms | 1-5ms |
| Delete | < 1ms | 1-5ms |
| Pattern match | ~5-10ms | ~10-20ms |

---

## 🔐 Production Deployment

### Docker Compose
```yaml
services:
  po-service:
    image: po-service:latest
    environment:
      CacheSettings__Type: Redis
      CacheSettings__RedisConnection: redis:6379
    depends_on:
      - redis

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
```

### Kubernetes
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: po-service-config
data:
  CacheSettings__Type: Redis
  CacheSettings__RedisConnection: redis-service:6379

---
apiVersion: v1
kind: Service
metadata:
  name: redis-service
spec:
  selector:
    app: redis
  ports:
    - port: 6379
      targetPort: 6379
```

---

## 📊 Cache Strategies by Endpoint

### Read-Heavy Endpoints
```csharp
// Cache TTL: 30 minutes
// Get PO details
GET /api/purchase-orders/{id}
  TTL: 30 min
  Invalidation: On PO update

// List suppliers
GET /api/suppliers
  TTL: 1 hour
  Invalidation: On supplier create/update
```

### Write Operations
```csharp
// No caching (write-through)
POST /api/purchase-orders
  Cache invalidation: po:supplier:{supplierId}*

// Invalidate on approve
POST /api/purchase-orders/{id}/approve
  Cache invalidation: po:{id}, po:status:*

// Invalidate on cancel
POST /api/purchase-orders/{id}/cancel
  Cache invalidation: po:{id}, po:status:*
```

---

## 🧪 Testing Cache

### Unit Tests
```csharp
[Fact]
public async Task GetAsync_CachedValue_ReturnsCachedValue()
{
    var cacheService = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
    var key = "test-key";
    var value = "test-value";

    await cacheService.SetAsync(key, value);
    var result = await cacheService.GetAsync<string>(key);

    Assert.Equal(value, result);
}

[Fact]
public async Task RemoveAsync_ExistingKey_RemovesFromCache()
{
    var cacheService = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
    
    await cacheService.SetAsync("key", "value");
    await cacheService.RemoveAsync("key");
    var result = await cacheService.GetAsync<string>("key");

    Assert.Null(result);
}

[Fact]
public async Task RemoveByPatternAsync_MatchingKeys_RemovesAll()
{
    var cacheService = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
    
    await cacheService.SetAsync("po:123", "value1");
    await cacheService.SetAsync("po:456", "value2");
    await cacheService.SetAsync("supplier:123", "value3");

    await cacheService.RemoveByPatternAsync("po:*");

    Assert.Null(await cacheService.GetAsync<string>("po:123"));
    Assert.Null(await cacheService.GetAsync<string>("po:456"));
    Assert.NotNull(await cacheService.GetAsync<string>("supplier:123"));
}
```

---

## 📈 Monitoring Cache

### Key Metrics to Monitor
- **Cache hit rate** - Target: > 80%
- **Cache eviction rate** - Watch for thrashing
- **Redis memory usage** - Alert if > 90%
- **Serialization time** - For Redis operations

### Logging Example
```csharp
_logger.LogInformation("Cache hit for key {Key}", cacheKey);
_logger.LogInformation("Cache miss for key {Key}, fetching from DB", cacheKey);
_logger.LogWarning("Cache invalidation for pattern {Pattern}", pattern);
```

---

## 🔧 Troubleshooting

### Memory Cache Issues
**Problem:** Cache keeps growing, high memory usage
```
Solution: 
1. Lower TTL values
2. Implement more aggressive invalidation
3. Switch to Redis for distributed scenarios
```

### Redis Connection Issues
**Problem:** "Cannot connect to redis"
```
Solution:
1. Verify redis-service DNS name
2. Check redis port (default 6379)
3. Verify firewall rules
4. Check Redis service status
```

### Cache Deserialization Errors
**Problem:** JSON deserialization fails
```
Solution:
1. Ensure consistent DTO definitions
2. Clear Redis cache: FLUSHDB
3. Restart application
4. Check Redis value: redis-cli GET key
```

---

## 🎯 Best Practices

✅ **DO**:
- Cache read-heavy operations
- Use appropriate TTLs (30 min - 1 hour typical)
- Invalidate related caches on writes
- Monitor cache hit rates
- Use namespaced keys (po_service:)
- Test cache behavior

❌ **DON'T**:
- Cache user-sensitive data without encryption
- Use indefinite TTLs (unless intentional)
- Cache if hit rate would be < 50%
- Store large objects (> 1MB) in cache
- Ignore cache invalidation strategies
- Skip monitoring in production

---

## 📚 Files Created

```
src/ERP.PO.Application/Caching/
├── ICacheService.cs              # Cache abstraction
├── MemoryCacheService.cs         # Memory cache implementation
├── RedisCacheService.cs          # Redis cache implementation
└── CacheKeys.cs                  # Standard cache key patterns
```

---

## Summary

✅ **Memory Cache** - Development, fast, no external dependency
✅ **Redis Cache** - Production, distributed, scalable
✅ **Abstraction** - Easy to switch between implementations
✅ **Configuration** - Environment-based automatic selection
✅ **Performance** - Significant reduction in database hits
✅ **Scalability** - Ready for multi-node deployments

The caching system is **production-ready** and follows best practices for both small (development) and large (production) deployments.
