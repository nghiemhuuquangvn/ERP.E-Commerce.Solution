using Microsoft.EntityFrameworkCore;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Aggregates.Location;
using ERP.WH.Domain.Aggregates.Transfers;
using ERP.WH.Domain.Aggregates.Stocktakes;
using ERP.WH.Domain.Settings;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;

namespace ERP.WH.Infrastructure.Persistence;

public class WarehouseDbContext : DbContext, ITenantScopedDbContext
{
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<WarehouseLocation> WarehouseLocations { get; set; }
    public DbSet<InventoryLot> InventoryLots { get; set; }
    public DbSet<InventorySerial> InventorySerials { get; set; }
    public DbSet<TransferOrder> TransferOrders { get; set; }
    public DbSet<Stocktake> Stocktakes { get; set; }
    public DbSet<SystemInventorySettings> SystemInventorySettings { get; set; }
    public DbSet<TenantInventorySettings> TenantInventorySettings { get; set; }
    public DbSet<InventoryLocationStock> InventoryLocationStocks { get; set; }
    public DbSet<StockBalance> StockBalances { get; set; }
    public DbSet<StockAdjustment> StockAdjustments { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<BarcodeMapping> BarcodeMappings { get; set; }
    public DbSet<CostConsumptionLog> CostConsumptionLogs { get; set; }
    public DbSet<InventoryValuationSnapshot> InventoryValuationSnapshots { get; set; }
    public DbSet<InventoryValuationSnapshotLine> InventoryValuationSnapshotLines { get; set; }

    /// <summary>
    /// Isolated Outbox table used only for the new Settings-changed Kafka publish path
    /// (SettingsOutboxProcessor / KafkaSettingsEventPublisher). Unrelated to
    /// IEventPublisher/EventPublisher's RabbitMQ warehouse_exchange flow.
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    private readonly ITenantProvider _tenantProvider;

    /// <summary>
    /// Tenant of the current request (JWT / Gateway host tenant) or consumer message
    /// (TenantScope). Read by the tenant query filter on every query and used to
    /// stamp/validate every save — the plain-POCO aggregates implement ITenantOwned.
    /// </summary>
    public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ProductSku).HasMaxLength(100);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.Property(e => e.TotalQuantity).IsRequired();
            entity.Property(e => e.ReservedQuantity).IsRequired();
            entity.Property(e => e.LockedQuantity).IsRequired();
            entity.Property(e => e.ReorderPoint).IsRequired();
            entity.Property(e => e.ReorderQuantity).IsRequired();
            entity.Property(e => e.MinStock).IsRequired();
            entity.Property(e => e.MaxStock).IsRequired();
            entity.Property(e => e.SafetyStock).IsRequired();
            entity.Property(e => e.Cost).IsRequired();
            entity.Property(e => e.ValuationMethod).IsRequired();
            entity.Property(e => e.LastMovementDate).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.ProductName);
            entity.HasIndex(e => e.ProductSku);
        });

        modelBuilder.Entity<InventoryLot>(entity =>
        {
            entity.HasKey(e => e.LotId);
            entity.Property(e => e.LotNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ReservedQuantity).IsRequired();
            entity.Property(e => e.UnitCost).HasPrecision(18, 2);
            entity.Property(e => e.ReceivedDate).IsRequired();
            entity.Property(e => e.Status).HasConversion<byte>().HasDefaultValue(LotStatus.Available);
            entity.Property(e => e.Source).HasConversion<byte>().HasDefaultValue(LotSource.Receipt);
            entity.Property(e => e.ManufactureDate).HasColumnType("date");
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.ProductId, e.RemainingQuantity });
            entity.HasIndex(e => e.ExpiryDate);
        });

        modelBuilder.Entity<InventorySerial>(entity =>
        {
            entity.HasKey(e => e.SerialId);
            entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired();

            entity.HasIndex(e => new { e.TenantId, e.SerialNumber }).IsUnique();
            entity.HasIndex(e => new { e.ProductId, e.Status });
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.MovementId);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ProductId).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.MovementType).IsRequired();
            entity.Property(e => e.FromLocation).HasMaxLength(100);
            entity.Property(e => e.ToLocation).HasMaxLength(100);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RecordedAt).IsRequired();
            // Live column is "ReferenceId" (001_CreateWarehouseDB.sql) but the C#
            // property is "SourceOrderId" — unmapped, this 500'd with "Invalid
            // column name 'SourceOrderId'" the moment any movement was actually
            // recorded through the real API (only ever seeded directly before).
            entity.Property(e => e.SourceOrderId).HasColumnName("ReferenceId");

            entity.Property(e => e.UnitCost).HasPrecision(18, 6);
            entity.Property(e => e.ValueDelta).HasPrecision(18, 2);
            entity.Property(e => e.StockDelta).HasPrecision(18, 6);

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.RecordedAt);
            // One production movement per document line (SQL 012); older types are not protected (repeated rows exist).
            entity.HasIndex(e => new { e.TenantId, e.ProductId, e.Reference, e.MovementType })
                .IsUnique().HasDatabaseName("UX_StockMovements_Idempotency")
                .HasFilter("[Reference] IS NOT NULL AND [MovementType] >= 7");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(e => e.ReservationId);
            entity.Property(e => e.OrderId).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.HoldUntil).IsRequired();
            entity.Property(e => e.ReservedAt).IsRequired();

            // Previously unconfigured — EF Core cannot map a bare List<ReservedItem>
            // navigation with no owner/key convention it recognizes, which throws at
            // model-build time (first DB access) the moment any reservation is read
            // or written. Owned collection keyed by the shadow FK EF generates.
            entity.OwnsMany(e => e.ReservedItems, items =>
            {
                // Without this, EF defaults to the CLR class name "ReservedItem" (singular) —
                // the actual table (002_AddLotsSerialsTransfersStocktakes.sql) is
                // "Reservation_ReservedItems", so every reservation read/write 500'd with
                // "Invalid object name 'ReservedItem'" (including the background
                // ReservationExpiryProcessor, silently failing every tick).
                items.ToTable("Reservation_ReservedItems");
                items.WithOwner().HasForeignKey("ReservationId");
                items.Property<int>("Id");
                items.HasKey("Id");
                items.Property(i => i.WarehouseLocation).HasMaxLength(100);
                items.Property(i => i.Quantity).HasPrecision(18, 6);

                // Persisted as JSON rather than a second-level owned collection — EF Core
                // supports nested OwnsMany but it requires its own composite-key plumbing
                // for what's really just a small value list (LotId, Quantity) per line.
                items.Property(i => i.LotAllocations)
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<List<LotAllocation>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<LotAllocation>())
                    .HasColumnName("LotAllocationsJson")
                    .HasColumnType("nvarchar(max)");
            });

            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.HoldUntil);
        });

        modelBuilder.Entity<StockBalance>(entity =>
        {
            entity.ToTable("StockBalances");
            entity.HasKey(e => e.BalanceId);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.TenantId, e.ProductId, e.WarehouseId, e.LotId }).IsUnique().HasDatabaseName("UX_StockBalances_Key");
            entity.HasIndex(e => new { e.TenantId, e.WarehouseId, e.ProductId }).HasDatabaseName("IX_StockBalances_Warehouse");
        });

        modelBuilder.Entity<InventoryLocationStock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.ReservedQuantity).IsRequired();
            entity.Property(e => e.LockedQuantity).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => new { e.TenantId, e.ProductId, e.LocationId }).IsUnique();
        });

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuantityDelta).IsRequired();
            entity.Property(e => e.Reason).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.RequestedBy).IsRequired().HasMaxLength(256);
            entity.Property(e => e.RequestedAt).IsRequired();
            entity.Property(e => e.ApprovedBy).HasMaxLength(256);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired();

            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        });

        modelBuilder.Entity<CostConsumptionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitCost).HasPrecision(18, 2);
            entity.Property(e => e.TotalCost).HasPrecision(18, 2);
            entity.Property(e => e.ConsumedAt).IsRequired();

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.SourceOrderId);
        });

        modelBuilder.Entity<InventoryValuationSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PeriodCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SnapshotDate).IsRequired();
            entity.Property(e => e.IsClosed).IsRequired();

            entity.HasMany(e => e.Lines)
                .WithOne()
                .HasForeignKey(l => l.SnapshotId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(e => e.Lines).AutoInclude();

            entity.HasIndex(e => new { e.TenantId, e.PeriodCode, e.WarehouseId }).IsUnique();
        });

        modelBuilder.Entity<InventoryValuationSnapshotLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitCost).HasPrecision(18, 2);
            entity.Property(e => e.TotalValue).HasPrecision(18, 2);
        });

        modelBuilder.Entity<BarcodeMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Barcode).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SerialNumber).HasMaxLength(100);

            entity.HasIndex(e => new { e.TenantId, e.Barcode }).IsUnique();
            entity.HasIndex(e => e.ProductId);
        });

        modelBuilder.Entity<WarehouseLocation>(entity =>
        {
            entity.HasKey(e => e.LocationId);
            entity.Property(e => e.LocationCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Zone).HasMaxLength(50);
            entity.Property(e => e.Aisle).HasMaxLength(50);
            entity.Property(e => e.Bin).HasMaxLength(50);
            entity.Property(e => e.Shelf).HasMaxLength(50);
            entity.Property(e => e.Capacity).IsRequired();
            entity.Property(e => e.CurrentOccupancy).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.LocationCode);
            entity.HasIndex(e => e.WarehouseId);
        });

        modelBuilder.Entity<TransferOrder>(entity =>
        {
            entity.HasKey(e => e.TransferOrderId);
            entity.Property(e => e.TransferNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasMany(e => e.Lines)
                .WithOne()
                .HasForeignKey(l => l.TransferOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(e => e.Lines).AutoInclude();

            entity.HasIndex(e => new { e.TenantId, e.TransferNumber }).IsUnique();
        });

        modelBuilder.Entity<TransferOrderLine>(entity =>
        {
            // TransferOrderLine has no DbSet of its own (only reachable via
            // TransferOrder.Lines), so EF defaults to the CLR class name "TransferOrderLine"
            // (singular) unless told otherwise — the actual table is "TransferOrderLines".
            entity.ToTable("TransferOrderLines");
            entity.HasKey(e => e.LineId);
            entity.Property(e => e.Quantity).IsRequired();
        });

        modelBuilder.Entity<Stocktake>(entity =>
        {
            entity.HasKey(e => e.StocktakeId);
            entity.Property(e => e.StocktakeNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.StartedBy).HasMaxLength(256);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.StartedAt).IsRequired();

            entity.HasMany(e => e.Lines)
                .WithOne()
                .HasForeignKey(l => l.StocktakeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(e => e.Lines).AutoInclude();

            entity.HasIndex(e => new { e.TenantId, e.StocktakeNumber }).IsUnique();
        });

        modelBuilder.Entity<StocktakeLine>(entity =>
        {
            // StocktakeLine has no DbSet of its own (only reachable via Stocktake.Lines), so
            // EF defaults to the CLR class name "StocktakeLine" (singular) unless told
            // otherwise — the actual table is "StocktakeLines".
            entity.ToTable("StocktakeLines");
            entity.HasKey(e => e.LineId);
            entity.Property(e => e.SystemQuantity).IsRequired();
        });

        modelBuilder.Entity<SystemInventorySettings>(entity =>
        {
            entity.ToTable("SystemInventorySettings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<TenantInventorySettings>(entity =>
        {
            entity.ToTable("TenantInventorySettings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.OccurredOnUtc).IsRequired();

            // Index for pending messages (used by SettingsOutboxProcessor)
            entity.HasIndex(e => e.ProcessedOnUtc)
                .HasDatabaseName("IX_OutboxMessages_ProcessedOnUtc_Pending")
                .HasFilter("[ProcessedOnUtc] IS NULL");
        });

        // Every stock quantity is decimal(18,6) since the Warehouse upgrade W1 (SQL Warehouse/010). A bare decimal would map to (18,2).
        ConfigureQuantityPrecision(modelBuilder);

        // Last, so it composes with any per-entity filter configured above.
        modelBuilder.ApplyTenantQueryFilters(this);
    }

    private static void ConfigureQuantityPrecision(ModelBuilder modelBuilder)
    {
        void Set<T>(params System.Linq.Expressions.Expression<Func<T, decimal>>[] quantities) where T : class
        {
            foreach (var quantity in quantities) modelBuilder.Entity<T>().Property(quantity).HasPrecision(18, 6);
        }

        Set<Inventory>(e => e.TotalQuantity, e => e.ReservedQuantity, e => e.LockedQuantity, e => e.ReorderPoint, e => e.ReorderQuantity, e => e.MinStock, e => e.MaxStock, e => e.SafetyStock);
        Set<InventoryLot>(e => e.ReceivedQuantity, e => e.RemainingQuantity, e => e.ReservedQuantity);
        Set<InventoryLocationStock>(e => e.Quantity, e => e.ReservedQuantity, e => e.LockedQuantity);
        Set<StockBalance>(e => e.Quantity, e => e.ReservedQuantity, e => e.LockedQuantity);
        Set<StockMovement>(e => e.Quantity);
        Set<StockAdjustment>(e => e.QuantityDelta);
        Set<CostConsumptionLog>(e => e.Quantity);
        Set<InventoryValuationSnapshotLine>(e => e.Quantity);
        Set<WarehouseLocation>(e => e.Capacity, e => e.CurrentOccupancy);
        Set<TransferOrderLine>(e => e.Quantity);
        Set<StocktakeLine>(e => e.SystemQuantity);
        modelBuilder.Entity<StocktakeLine>().Property(e => e.CountedQuantity).HasPrecision(18, 6);
        Set<TenantInventorySettings>(e => e.LowStockThreshold);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw Conflict(ex);
        }
        catch (DbUpdateException ex) when (IsIdempotencyViolation(ex))
        {
            throw Conflict(ex);
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw Conflict(ex);
        }
        catch (DbUpdateException ex) when (IsIdempotencyViolation(ex))
        {
            throw Conflict(ex);
        }
    }

    /// <summary>The same production movement arrived twice at once and the unique index let only one through: the caller retries and gets the first result.</summary>
    private static bool IsIdempotencyViolation(DbUpdateException ex)
        => ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } sql
           && sql.Message.Contains("UX_StockMovements_Idempotency", StringComparison.OrdinalIgnoreCase);

    /// <summary>Stock rows carry a RowVersion: someone else changed the same row first, so the caller must read again and retry (HTTP 409).</summary>
    private static ERP.Shared.Infrastructure.Exceptions.ConcurrencyException Conflict(DbUpdateException ex)
        => new("The stock was changed by another request at the same time. Read it again and retry.", ex);
}
