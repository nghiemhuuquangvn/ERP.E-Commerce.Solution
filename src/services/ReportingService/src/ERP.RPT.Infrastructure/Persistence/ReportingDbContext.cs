using ERP.RPT.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace ERP.RPT.Infrastructure.Persistence;

public class ReportingDbContext : DbContext
{
    public DbSet<FactSalesOrder> FactSalesOrders => Set<FactSalesOrder>();
    public DbSet<FactPayment> FactPayments => Set<FactPayment>();
    public DbSet<FactInventoryMovement> FactInventoryMovements => Set<FactInventoryMovement>();
    public DbSet<DimDate> DimDates => Set<DimDate>();

    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FactSalesOrder>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.CustomerId);
        });

        modelBuilder.Entity<FactPayment>(e =>
        {
            e.HasKey(x => x.PaymentId);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => x.DateKey);
        });

        modelBuilder.Entity<FactInventoryMovement>(e =>
        {
            e.HasKey(x => x.MovementId);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.ProductId);
        });

        modelBuilder.Entity<DimDate>(e =>
        {
            e.HasKey(x => x.DateKey);
            e.HasIndex(x => x.Date).IsUnique();
        });
    }
}
