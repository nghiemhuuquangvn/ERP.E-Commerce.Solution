using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.PO.Infrastructure.Data;

/// <summary>
/// DbContext for Purchase Order Service.
/// Manages PurchaseOrder, PurchaseOrderItem, and Supplier aggregates.
/// Enforces tenant isolation and optimistic locking patterns.
/// </summary>
public class PurchasingDbContext : DbContext, IUnitOfWork
{
    private readonly IUserContextService _userContextService;

    public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; } = null!;
    public DbSet<Supplier> Suppliers { get; set; } = null!;

    public PurchasingDbContext(DbContextOptions<PurchasingDbContext> options, IUserContextService userContextService)
        : base(options)
    {
        _userContextService = userContextService;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply tenant filter for multi-tenant isolation
        var userContext = _userContextService.GetCurrentUser();
        if (!string.IsNullOrEmpty(userContext.TenantId))
        {
            TenantFilterConfiguration.ConfigureTenantFilter(builder, new Guid(userContext.TenantId));
        }

        // Configure PurchaseOrder aggregate
        var purchaseOrderBuilder = builder.Entity<PurchaseOrder>();
        purchaseOrderBuilder.HasKey(po => new { po.Id, po.TenantId });
        purchaseOrderBuilder.Property(po => po.PONumber).HasMaxLength(50).IsRequired();
        purchaseOrderBuilder.Property(po => po.Status).IsRequired();
        purchaseOrderBuilder.Property(po => po.CreatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAdd();
        purchaseOrderBuilder.Property(po => po.UpdatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAddOrUpdate();

        // Foreign key to Supplier
        purchaseOrderBuilder.HasOne(po => po.Supplier)
            .WithMany()
            .HasForeignKey(po => new { po.SupplierId, po.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-Many: PurchaseOrder to PurchaseOrderItem
        purchaseOrderBuilder.HasMany(po => po.Items)
            .WithOne()
            .HasForeignKey(poi => new { poi.PurchaseOrderId, poi.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        purchaseOrderBuilder.Navigation(po => po.Items).AutoInclude();

        // Configure PurchaseOrderItem
        var itemBuilder = builder.Entity<PurchaseOrderItem>();
        itemBuilder.HasKey(poi => new { poi.Id, poi.TenantId });
        itemBuilder.Property(poi => poi.ProductCode).HasMaxLength(50).IsRequired();
        itemBuilder.Property(poi => poi.ProductName).HasMaxLength(255).IsRequired();
        itemBuilder.Property(poi => poi.Quantity).HasPrecision(18, 2);
        itemBuilder.Property(poi => poi.Unit).HasMaxLength(20).IsRequired();
        itemBuilder.Property(poi => poi.Status).IsRequired();
        itemBuilder.Property(poi => poi.CreatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAdd();
        itemBuilder.Property(poi => poi.UpdatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAddOrUpdate();

        // Configure Supplier aggregate
        var supplierBuilder = builder.Entity<Supplier>();
        supplierBuilder.HasKey(s => new { s.Id, s.TenantId });
        supplierBuilder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        supplierBuilder.HasIndex(s => new { s.Code, s.TenantId }).IsUnique();
        supplierBuilder.Property(s => s.Name).HasMaxLength(255).IsRequired();
        supplierBuilder.Property(s => s.Email).HasMaxLength(255);
        supplierBuilder.Property(s => s.Phone).HasMaxLength(20);
        supplierBuilder.Property(s => s.IsActive).IsRequired();
        supplierBuilder.Property(s => s.CreatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAdd();
        supplierBuilder.Property(s => s.UpdatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAddOrUpdate();

        // Configure Address as owned entity
        supplierBuilder.OwnsOne(s => s.Address, addressBuilder =>
        {
            addressBuilder.Property(a => a.Street).HasMaxLength(255);
            addressBuilder.Property(a => a.City).HasMaxLength(100);
            addressBuilder.Property(a => a.State).HasMaxLength(50);
            addressBuilder.Property(a => a.PostalCode).HasMaxLength(20);
            addressBuilder.Property(a => a.Country).HasMaxLength(100);
        });
    }

    public ITransaction BeginTransaction()
    {
        var transaction = Database.BeginTransaction();
        return new TransactionAdapter(transaction);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ERP.Shared.Infrastructure.Exceptions.ConcurrencyException(
                "Data has been modified by another user. Please refresh and try again.",
                ex);
        }
    }
}

/// <summary>
/// Adapter for EF Core transaction to ITransaction interface.
/// </summary>
public class TransactionAdapter : ITransaction
{
    private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction;

    public TransactionAdapter(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.RollbackAsync(cancellationToken);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
            await _transaction.DisposeAsync();
    }
}
