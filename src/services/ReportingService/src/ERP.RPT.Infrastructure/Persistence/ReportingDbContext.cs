using ERP.RPT.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace ERP.RPT.Infrastructure.Persistence;

public class ReportingDbContext : DbContext
{
    public DbSet<FactSalesOrder> FactSalesOrders => Set<FactSalesOrder>();
    public DbSet<FactPayment> FactPayments => Set<FactPayment>();
    public DbSet<FactInventoryMovement> FactInventoryMovements => Set<FactInventoryMovement>();
    public DbSet<FactPOSTransaction> FactPOSTransactions => Set<FactPOSTransaction>();
    public DbSet<FactHRPayslip> FactHRPayslips => Set<FactHRPayslip>();
    public DbSet<DimDate> DimDates => Set<DimDate>();
    public DbSet<DimProduct> DimProducts => Set<DimProduct>();

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

        modelBuilder.Entity<FactPOSTransaction>(e =>
        {
            e.HasKey(x => x.TransactionId);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.TaxAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.DateKey);
            e.HasIndex(x => x.StoreId);
        });

        modelBuilder.Entity<FactHRPayslip>(e =>
        {
            e.HasKey(x => x.PayslipId);
            e.Property(x => x.BasicSalary).HasPrecision(18, 2);
            e.Property(x => x.OvertimePay).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Period);
        });

        modelBuilder.Entity<DimDate>(e =>
        {
            e.HasKey(x => x.DateKey);
            e.HasIndex(x => x.Date).IsUnique();
        });

        modelBuilder.Entity<DimProduct>(e =>
        {
            e.HasKey(x => x.ProductId);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasIndex(x => x.Category);
        });
    }
}
