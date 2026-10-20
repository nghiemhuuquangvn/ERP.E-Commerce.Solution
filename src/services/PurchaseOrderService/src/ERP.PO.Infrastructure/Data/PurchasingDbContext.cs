using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.PO.Infrastructure.Data;

/// <summary>
/// DbContext for Purchase Order Service.
/// Manages PurchaseOrder, PurchaseOrderItem, and Supplier aggregates.
/// Enforces tenant isolation and optimistic locking patterns.
/// </summary>
public class PurchasingDbContext : DbContext, IUnitOfWork, ITenantScopedDbContext
{
    private readonly IUserContextService _userContextService;

    /// <summary>
    /// Re-read by the tenant query filter on every query (see
    /// TenantFilterConfiguration.ApplyTenantQueryFilters) — never baked into the model.
    /// </summary>
    public Guid? CurrentTenantId =>
        TenantScope.Current
        ?? (Guid.TryParse(_userContextService.GetCurrentUser().TenantId, out var tenantId) && tenantId != Guid.Empty
            ? tenantId
            : null);

    public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; } = null!;
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<SupplierRating> SupplierRatings { get; set; } = null!;
    public DbSet<SupplierItem> SupplierItems { get; set; } = null!;
    public DbSet<GoodsReceipt> GoodsReceipts { get; set; } = null!;
    public DbSet<PurchaseRequest> PurchaseRequests { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public PurchasingDbContext(DbContextOptions<PurchasingDbContext> options, IUserContextService userContextService)
        : base(options)
    {
        _userContextService = userContextService;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure PurchaseOrder aggregate
        var purchaseOrderBuilder = builder.Entity<PurchaseOrder>();
        purchaseOrderBuilder.HasKey(po => new { po.Id, po.TenantId });
        purchaseOrderBuilder.Property(po => po.PONumber).HasMaxLength(50).IsRequired();
        purchaseOrderBuilder.Property(po => po.Status).IsRequired();
        purchaseOrderBuilder.Property(po => po.CreatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAdd();
        purchaseOrderBuilder.Property(po => po.UpdatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAddOrUpdate();
        purchaseOrderBuilder.OwnsOne(po => po.TotalAmount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("TotalAmount_Amount").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("TotalAmount_Currency").HasMaxLength(10);
        });

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
        purchaseOrderBuilder.Navigation(po => po.Supplier).AutoInclude();

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
        itemBuilder.OwnsOne(poi => poi.UnitPrice, m =>
        {
            m.Property(x => x.Amount).HasColumnName("UnitPrice_Amount").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("UnitPrice_Currency").HasMaxLength(10);
        });
        itemBuilder.Ignore(poi => poi.TotalPrice);

        // Configure Supplier aggregate
        var supplierBuilder = builder.Entity<Supplier>();
        supplierBuilder.HasKey(s => new { s.Id, s.TenantId });
        supplierBuilder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        supplierBuilder.HasIndex(s => new { s.Code, s.TenantId }).IsUnique();
        supplierBuilder.Property(s => s.Name).HasMaxLength(255).IsRequired();
        supplierBuilder.Property(s => s.Email).HasMaxLength(255);
        supplierBuilder.Property(s => s.Phone).HasMaxLength(20);
        supplierBuilder.Property(s => s.TaxCode).HasMaxLength(50);
        supplierBuilder.Property(s => s.PaymentTermDays).IsRequired();
        supplierBuilder.Property(s => s.CurrencyCode).HasMaxLength(3).IsRequired();
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

        // Configure SupplierRating
        var ratingBuilder = builder.Entity<SupplierRating>();
        ratingBuilder.HasKey(r => new { r.Id, r.TenantId });
        ratingBuilder.Property(r => r.Comment).HasMaxLength(2000);
        ratingBuilder.Property(r => r.RatedBy).HasMaxLength(256).IsRequired();
        ratingBuilder.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAdd();
        ratingBuilder.HasIndex(r => new { r.SupplierId, r.TenantId });

        // Configure SupplierItem — ProductId/VariantId are plain columns (cross-service
        // reference to ProductService's Product/ProductVariant by Id only, no DB FK).
        var supplierItemBuilder = builder.Entity<SupplierItem>();
        supplierItemBuilder.HasKey(si => new { si.Id, si.TenantId });
        supplierItemBuilder.Property(si => si.SupplierItemCode).HasMaxLength(50);
        supplierItemBuilder.Property(si => si.LastPurchasePrice).HasPrecision(18, 2);
        supplierItemBuilder.Property(si => si.CreatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAdd();
        supplierItemBuilder.Property(si => si.UpdatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAddOrUpdate();
        supplierItemBuilder.HasIndex(si => new { si.SupplierId, si.TenantId });
        supplierItemBuilder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(si => new { si.SupplierId, si.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        // Configure GoodsReceipt
        var receiptBuilder = builder.Entity<GoodsReceipt>();
        receiptBuilder.HasKey(r => new { r.Id, r.TenantId });
        receiptBuilder.Property(r => r.ReceiptNumber).HasMaxLength(50).IsRequired();
        receiptBuilder.Property(r => r.ReceivedBy).HasMaxLength(256).IsRequired();
        receiptBuilder.Property(r => r.Notes).HasMaxLength(1000);
        receiptBuilder.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAdd();
        receiptBuilder.HasIndex(r => new { r.PurchaseOrderId, r.TenantId });

        // Configure PurchaseRequest — plain Id key, no TenantId/composite key and not
        // covered by the tenant query filter below, since it doesn't extend BaseEntity
        // (the upstream StockAlertEvent signal carries no tenant concept).
        var purchaseRequestBuilder = builder.Entity<PurchaseRequest>();
        purchaseRequestBuilder.HasKey(pr => pr.Id);
        purchaseRequestBuilder.Property(pr => pr.ProductName).HasMaxLength(255).IsRequired();
        purchaseRequestBuilder.Property(pr => pr.Status).IsRequired();
        purchaseRequestBuilder.Property(pr => pr.CreatedAt).IsRequired();
        purchaseRequestBuilder.HasIndex(pr => pr.ProductId);
        purchaseRequestBuilder.HasIndex(pr => pr.Status);

        // Configure Outbox pattern table
        builder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.ProcessedOnUtc);
        });

        // Multi-tenant isolation for every BaseEntity. Previously ConfigureTenantFilter baked
        // the FIRST request's tenant into EF's once-per-process cached model, so every later
        // request (any tenant) was filtered by that one tenant — or not at all if the model
        // was first built without a user (background job).
        builder.ApplyTenantQueryFilters(this);
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
            ChangeTracker.EnforceTenantOnSave(CurrentTenantId);

            var domainEntities = ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.Entity.DomainEvents.Any()).ToList();
            domainEntities.ForEach(e => EventTenant.StampFrom(e.Entity));

            var outboxMessages = domainEntities
                .SelectMany(e => e.Entity.DomainEvents)
                .Select(ev => new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = ev.GetType().AssemblyQualifiedName ?? ev.GetType().Name,
                    Content = JsonConvert.SerializeObject(ev, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }),
                    OccurredOnUtc = ev.OccurredOn
                }).ToList();

            domainEntities.ForEach(e => e.Entity.ClearDomainEvents());

            var result = await base.SaveChangesAsync(cancellationToken);

            if (outboxMessages.Any())
            {
                OutboxMessages.AddRange(outboxMessages);
                await base.SaveChangesAsync(cancellationToken);
            }

            return result;
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
