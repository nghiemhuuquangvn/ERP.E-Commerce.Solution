using Microsoft.EntityFrameworkCore;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Infrastructure.Persistence;

public class WarehouseDbContext : DbContext
{
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<WarehouseLocation> WarehouseLocations { get; set; }

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
            entity.Property(e => e.TotalQuantity).IsRequired();
            entity.Property(e => e.ReservedQuantity).IsRequired();
            entity.Property(e => e.LockedQuantity).IsRequired();
            entity.Property(e => e.ReorderPoint).IsRequired();
            entity.Property(e => e.ReorderQuantity).IsRequired();
            entity.Property(e => e.Cost).IsRequired();
            entity.Property(e => e.LastMovementDate).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasIndex(e => e.ProductName);
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

            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.HoldUntil);
        });

        modelBuilder.Entity<WarehouseLocation>(entity =>
        {
            entity.HasKey(e => e.LocationId);
            entity.Property(e => e.LocationCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Aisle).HasMaxLength(50);
            entity.Property(e => e.Bin).HasMaxLength(50);
            entity.Property(e => e.Shelf).HasMaxLength(50);
            entity.Property(e => e.Capacity).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.LocationCode);
        });
    }
}
