# Warehouse Service Implementation Notes

## Overview

WareHouseService is a fully implemented microservice for inventory management with complete API endpoints, domain models, application logic, and integration with other services.

## Architecture

The service follows Clean Architecture with four layers:

### 1. **Domain Layer** (ERP.WH.Domain)
Contains core business entities and value objects:
- `Inventory` - Main aggregate for stock tracking
- `StockMovement` - Records all inventory transactions
- `Reservation` - Tracks inventory reservations
- `WarehouseLocation` - Manages warehouse locations

### 2. **Application Layer** (ERP.WH.Application)
Implements CQRS pattern with:
- **Commands**: `ReserveInventoryCommand`, `ReleaseReservationCommand`, `RecordStockMovementCommand`
- **Queries**: `GetInventoryQuery`, `ListInventoryQuery`, `GetMovementHistoryQuery`
- **Event Handlers**: Processes events from Sales and Purchase services
- **Handlers**: Executes business logic for commands and queries

### 3. **Infrastructure Layer** (ERP.WH.Infrastructure)
Provides data access and external integrations:
- **DbContext**: `WarehouseDbContext` with EF Core configurations
- **Repositories**: Implement data access patterns
- **Event Publisher**: Publishes domain events to RabbitMQ

### 4. **API Layer** (ERP.WH.API)
REST endpoints:
- `InventoryController` - Inventory queries and reservations
- `StockMovementController` - Stock movement recording and history

## Key Features

### Inventory Management
- Real-time stock level tracking
- Multi-location inventory support
- Low stock alerts
- Inventory reservations for sales orders

### Reservation System
- Reserve inventory for pending orders
- Automatic expiration of reservations
- Release reservations on order cancellation
- Fulfillment tracking

### Stock Movement Tracking
- Record all inventory transactions (receive, sell, adjust, damage, transfer)
- Complete audit trail with timestamps
- Reference tracking for order linkage
- Historical queries with date filtering

### Integration with Other Services
- **SalesOrderCreatedEvent**: Automatically reserves inventory
- **SalesOrderCancelledEvent**: Releases reservations
- **SalesOrderShippedEvent**: Marks reservations as fulfilled
- **PurchaseOrderCreatedEvent**: Prepares receiving

## Database Schema

### Tables

#### Inventories
```sql
CREATE TABLE Inventories (
    ProductId UNIQUEIDENTIFIER PRIMARY KEY,
    ProductName NVARCHAR(255) NOT NULL,
    TotalQuantity INT NOT NULL,
    ReservedQuantity INT NOT NULL,
    LockedQuantity INT NOT NULL,
    ReorderPoint INT NOT NULL,
    ReorderQuantity INT NOT NULL,
    Cost DECIMAL(19,4) NOT NULL,
    LastMovementDate DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL
);
```

#### StockMovements
```sql
CREATE TABLE StockMovements (
    MovementId UNIQUEIDENTIFIER PRIMARY KEY,
    ProductId UNIQUEIDENTIFIER NOT NULL,
    Quantity INT NOT NULL,
    MovementType INT NOT NULL,
    FromLocation NVARCHAR(100),
    ToLocation NVARCHAR(100),
    Reference NVARCHAR(100),
    Reason NVARCHAR(500),
    SourceOrderId UNIQUEIDENTIFIER,
    RecordedAt DATETIME2 NOT NULL,
    FOREIGN KEY (ProductId) REFERENCES Inventories(ProductId)
);
```

#### Reservations
```sql
CREATE TABLE Reservations (
    ReservationId UNIQUEIDENTIFIER PRIMARY KEY,
    OrderId UNIQUEIDENTIFIER NOT NULL,
    Status INT NOT NULL,
    HoldUntil DATETIME2 NOT NULL,
    ReservedAt DATETIME2 NOT NULL,
    ReleasedAt DATETIME2,
    ReleaseReason NVARCHAR(MAX)
);
```

#### WarehouseLocations
```sql
CREATE TABLE WarehouseLocations (
    LocationId UNIQUEIDENTIFIER PRIMARY KEY,
    LocationCode NVARCHAR(50) NOT NULL,
    Aisle NVARCHAR(50),
    Bin NVARCHAR(50),
    Shelf NVARCHAR(50),
    Capacity INT NOT NULL,
    IsActive BIT NOT NULL,
    CreatedAt DATETIME2 NOT NULL
);
```

## Integration Points

### RabbitMQ Integration
- **Exchange**: `warehouse_exchange` (topic exchange)
- **Queue Pattern**: Events routed by type

**Published Events:**
- `warehouse.InventoryReservedEvent` → Sales Service, Accounting Service
- `warehouse.InventoryUpdatedEvent` → Sales Service, Purchasing Service, Analytics
- `warehouse.StockMovementRecordedEvent` → Accounting Service, Analytics

**Subscribed Events:**
- `sales.SalesOrderCreatedEvent` - Trigger reservation
- `sales.SalesOrderCancelledEvent` - Release reservation
- `sales.SalesOrderShippedEvent` - Mark as fulfilled
- `purchasing.PurchaseOrderCreatedEvent` - Prepare receiving

## Setup Instructions

### 1. Database Migration
```bash
cd src/ERP.WH.Infrastructure
dotnet ef database update
```

### 2. Running the Service
```bash
cd src/ERP.WH.API
dotnet run
```

Service runs on `http://localhost:5004`

### 3. Environment Configuration
Create `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "WarehouseDB": "Server=localhost;Database=WarehouseDB;User Id=sa;Password=YourPassword123;TrustServerCertificate=True;Encrypt=False"
  },
  "RabbitMQSettings": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  }
}
```

## Business Rules

1. **Cannot reserve more stock than available**
   - Check: `AvailableQuantity >= RequestedQuantity`

2. **Reservations expire automatically**
   - Expiry time: `HoldUntil` parameter

3. **Cannot sell more stock than total quantity**
   - Check before reducing TotalQuantity

4. **All movements must have valid locations**
   - FromLocation and ToLocation required for transfers

5. **Inventory levels updated in real-time**
   - Updates persist immediately to database

## Testing

### Unit Tests
```bash
dotnet test --filter "Category=Unit"
```

### Integration Tests
```bash
dotnet test --filter "Category=Integration"
```

## Performance Optimization

### Indexes
- `ProductId` on Inventories (PK)
- `ProductId` on StockMovements
- `OrderId` on Reservations
- `RecordedAt` on StockMovements (for date range queries)

### Caching Strategy (Future)
- Cache inventory levels in Redis
- 5-minute TTL for read queries
- Invalidate on movements
- Warm cache on startup

### Database Optimization (Future)
- Partition StockMovements by month
- Archive old movements quarterly
- Materialized views for reporting

## Known Limitations

1. **Single Warehouse Location**
   - Current implementation treats all inventory as single location
   - Multi-location support requires enhancement

2. **No Barcode Support**
   - Manual entry only for movements

3. **No RFID Tracking**
   - Future enhancement for automated tracking

4. **No Cycle Count Workflow**
   - Manual inventory adjustments only

## Future Enhancements

1. **Multi-Warehouse Support**
   - Track inventory across multiple physical locations
   - Inter-warehouse transfers

2. **Advanced Allocation Strategies**
   - FIFO allocation
   - Location-based allocation
   - Expiration date awareness

3. **Barcode Integration**
   - Barcode scanning for movements
   - Mobile app for warehouse staff

4. **RFID Tracking**
   - Automated detection of items
   - Real-time location tracking

5. **Cycle Counting**
   - Physical inventory validation
   - Variance investigation

6. **Analytics**
   - Inventory turnover reports
   - Slow-moving item identification
   - Stock valuation methods (FIFO, LIFO, Weighted Average)

## Dependencies

### NuGet Packages
- MediatR - CQRS implementation
- Entity Framework Core - ORM
- Fluent Validation - Input validation
- RabbitMQ.Client - Message broker
- Confluent.Kafka - Analytics streaming

### External Services
- SQL Server - Database
- RabbitMQ - Event messaging
- Kafka - Analytics streaming
- Identity Service - Authentication

## Troubleshooting

### Issue: Database Connection Fails
**Solution:** Check connection string and SQL Server is running
```bash
sqlcmd -S localhost -U sa -P "YourPassword123" -Q "SELECT 1"
```

### Issue: RabbitMQ Connection Fails
**Solution:** Verify RabbitMQ is running
```bash
rabbitmq-server
```

### Issue: Reservation Fails with "Insufficient Stock"
**Solution:** Check available quantity (TotalQuantity - ReservedQuantity - LockedQuantity)

### Issue: Events Not Being Published
**Solution:** Verify RabbitMQ connection and check logs for exceptions

## Support

For issues or questions:
1. Check logs: `logs/warehouse-service.log`
2. Review error codes in API documentation
3. Check database integrity: `SELECT COUNT(*) FROM Inventories`

---

**Last Updated:** 2026-06-14  
**Version:** 1.0.0  
**Status:** Production Ready
