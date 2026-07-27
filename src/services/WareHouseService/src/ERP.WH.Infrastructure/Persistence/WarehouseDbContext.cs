using Microsoft.EntityFrameworkCore;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Aggregates.Location;
using ERP.WH.Domain.Aggregates.Transfers;
using ERP.WH.Domain.Aggregates.Stocktakes;
using ERP.WH.Domain.Settings;
using ERP.Shared.Messaging;

namespace ERP.WH.Infrastructure.Persistence;

public class WarehouseDbContext : DbContext
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

    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ProductSku).HasMaxLength(100);
            entity.Property(e => e.TotalQuantity).IsRequired();
            entity.Property(e => e.ReservedQuantity).IsRequired();
            entity.Property(e => e.LockedQuantity).IsRequired();
            entity.Property(e => e.ReorderPoint).IsRequired();
            entity.Property(e => e.ReorderQuantity).IsRequired();
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

            entity.HasIndex(e => new { e.ProductId, e.RemainingQuantity });
            entity.HasIndex(e => e.ExpiryDate);
        });

        modelBuilder.Entity<InventorySerial>(entity =>
        {
            entity.HasKey(e => e.SerialId);
            entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired();

            entity.HasIndex(e => e.SerialNumber).IsUnique();
            entity.HasIndex(e => new { e.ProductId, e.Status });
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.MovementId);
            entity.Property(e => e.ProductId).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.MovementType).IsRequired();
            entity.Property(e => e.FromLocation).HasMaxLength(100);
            entity.Property(e => e.ToLocation).HasMaxLength(100);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RecordedAt).IsRequired();

            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.RecordedAt);
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
                items.WithOwner().HasForeignKey("ReservationId");
                items.Property<int>("Id");
                items.HasKey("Id");
                items.Property(i => i.WarehouseLocation).HasMaxLength(100);

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

        modelBuilder.Entity<InventoryLocationStock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.ReservedQuantity).IsRequired();
            entity.Property(e => e.LockedQuantity).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => new { e.ProductId, e.LocationId }).IsUnique();
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

            entity.HasIndex(e => e.Code).IsUnique();
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

            entity.HasIndex(e => new { e.PeriodCode, e.WarehouseId }).IsUnique();
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

            entity.HasIndex(e => e.Barcode).IsUnique();
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

            entity.HasIndex(e => e.TransferNumber).IsUnique();
        });

        modelBuilder.Entity<TransferOrderLine>(entity =>
        {
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

            entity.HasIndex(e => e.StocktakeNumber).IsUnique();
        });

        modelBuilder.Entity<StocktakeLine>(entity =>
        {
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
    }
}
