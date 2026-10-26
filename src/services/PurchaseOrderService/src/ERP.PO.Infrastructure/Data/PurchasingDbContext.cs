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
    public DbSet<RequestForQuotation> RequestsForQuotation { get; set; } = null!;
    public DbSet<RfqLine> RfqLines { get; set; } = null!;
    public DbSet<RfqInvitation> RfqInvitations { get; set; } = null!;
    public DbSet<SupplierQuotation> SupplierQuotations { get; set; } = null!;
    public DbSet<QuotationLine> QuotationLines { get; set; } = null!;
    public DbSet<SupplierInvoice> SupplierInvoices { get; set; } = null!;
    public DbSet<SupplierInvoiceLine> SupplierInvoiceLines { get; set; } = null!;
    public DbSet<SupplierInvoiceMatchLine> SupplierInvoiceMatchLines { get; set; } = null!;
    public DbSet<VendorReturn> VendorReturns { get; set; } = null!;
    public DbSet<VendorReturnLine> VendorReturnLines { get; set; } = null!;
    public DbSet<PurchasingSettings> PurchasingSettings { get; set; } = null!;
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
        // The column has always been POId (001_CreatePurchasingDB.sql, seed data, indexes); mapping the
        // default name made every goods receipt insert fail with "Invalid column name 'PurchaseOrderId'".
        receiptBuilder.Property(r => r.PurchaseOrderId).HasColumnName("POId");
        receiptBuilder.HasIndex(r => new { r.PurchaseOrderId, r.TenantId });

        // Configure PurchaseRequest — plain Id key (not BaseEntity). Tenant-owned since
        // 2026-10-20: StockAlertEvent now carries the warehouse's tenant, so the request is
        // created in it and covered by the tenant query filter below (ITenantOwned).
        var purchaseRequestBuilder = builder.Entity<PurchaseRequest>();
        purchaseRequestBuilder.HasKey(pr => pr.Id);
        purchaseRequestBuilder.Property(pr => pr.ProductName).HasMaxLength(255).IsRequired();
        purchaseRequestBuilder.Property(pr => pr.ProductCode).HasMaxLength(100).IsRequired();
        purchaseRequestBuilder.Property(pr => pr.UnitPrice).HasPrecision(18, 2);
        purchaseRequestBuilder.Property(pr => pr.Note).HasMaxLength(500);
        purchaseRequestBuilder.HasIndex(pr => new { pr.TenantId, pr.ProductCode });
        purchaseRequestBuilder.Property(pr => pr.Status).IsRequired();
        purchaseRequestBuilder.Property(pr => pr.CreatedAt).IsRequired();
        purchaseRequestBuilder.HasIndex(pr => pr.ProductId);
        purchaseRequestBuilder.HasIndex(pr => pr.Status);
        purchaseRequestBuilder.HasIndex(pr => new { pr.TenantId, pr.Status });

        // ── P1-8: RFQ, quotations, supplier invoices, vendor returns, settings. Same keying as the order: (Id, TenantId).
        var rfq = builder.Entity<RequestForQuotation>();
        rfq.ToTable("RequestForQuotations");
        rfq.HasKey(x => new { x.Id, x.TenantId });
        rfq.Property(x => x.RfqNumber).HasMaxLength(50).IsRequired();
        rfq.Property(x => x.Title).HasMaxLength(255).IsRequired();
        rfq.Property(x => x.Notes).HasMaxLength(1000);
        rfq.HasIndex(x => new { x.TenantId, x.RfqNumber }).IsUnique();
        rfq.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.RfqId, x.TenantId }).OnDelete(DeleteBehavior.Cascade);
        rfq.HasMany(x => x.Invitations).WithOne().HasForeignKey(x => new { x.RfqId, x.TenantId }).OnDelete(DeleteBehavior.Cascade);
        rfq.HasMany(x => x.Quotations).WithOne().HasForeignKey(x => new { x.RfqId, x.TenantId }).OnDelete(DeleteBehavior.Cascade);
        rfq.Navigation(x => x.Lines).AutoInclude();
        rfq.Navigation(x => x.Invitations).AutoInclude();
        rfq.Navigation(x => x.Quotations).AutoInclude();

        var rfqLine = builder.Entity<RfqLine>();
        rfqLine.HasKey(x => new { x.Id, x.TenantId });
        rfqLine.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
        rfqLine.Property(x => x.ProductName).HasMaxLength(255).IsRequired();
        rfqLine.Property(x => x.Unit).HasMaxLength(20);
        rfqLine.Property(x => x.Quantity).HasPrecision(18, 2);

        var rfqInvitation = builder.Entity<RfqInvitation>();
        rfqInvitation.HasKey(x => new { x.Id, x.TenantId });

        var quotation = builder.Entity<SupplierQuotation>();
        quotation.HasKey(x => new { x.Id, x.TenantId });
        quotation.Property(x => x.QuoteNumber).HasMaxLength(50);
        quotation.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        quotation.Property(x => x.Notes).HasMaxLength(1000);
        quotation.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.QuotationId, x.TenantId }).OnDelete(DeleteBehavior.Cascade);
        quotation.Navigation(x => x.Lines).AutoInclude();

        var quotationLine = builder.Entity<QuotationLine>();
        quotationLine.HasKey(x => new { x.Id, x.TenantId });
        quotationLine.Property(x => x.UnitPrice).HasPrecision(18, 4);

        var invoice = builder.Entity<SupplierInvoice>();
        invoice.HasKey(x => new { x.Id, x.TenantId });
        invoice.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        invoice.Property(x => x.PONumber).HasMaxLength(50);
        invoice.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        invoice.Property(x => x.NetAmount).HasPrecision(18, 2);
        invoice.Property(x => x.TaxAmount).HasPrecision(18, 2);
        invoice.Property(x => x.ReceivedValue).HasPrecision(18, 2);
        invoice.Property(x => x.HoldReasons).HasMaxLength(2000);
        invoice.Property(x => x.ApprovalReason).HasMaxLength(1000);
        invoice.Property(x => x.RejectionReason).HasMaxLength(1000);
        invoice.Property(x => x.ApprovedBy).HasMaxLength(256);
        invoice.Ignore(x => x.TotalAmount);
        invoice.HasIndex(x => new { x.TenantId, x.SupplierId, x.InvoiceNumber }).IsUnique();
        invoice.HasIndex(x => new { x.TenantId, x.PurchaseOrderId });
        invoice.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.InvoiceId, x.TenantId }).OnDelete(DeleteBehavior.Cascade);
        invoice.HasMany(x => x.MatchLines).WithOne().HasForeignKey(x => new { x.InvoiceId, x.TenantId }).OnDelete(DeleteBehavior.Cascade);
        invoice.Navigation(x => x.Lines).AutoInclude();
        invoice.Navigation(x => x.MatchLines).AutoInclude();

        var invoiceLine = builder.Entity<SupplierInvoiceLine>();
        invoiceLine.HasKey(x => new { x.Id, x.TenantId });
        invoiceLine.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
        invoiceLine.Property(x => x.Quantity).HasPrecision(18, 2);
        invoiceLine.Property(x => x.UnitPrice).HasPrecision(18, 4);

        var matchLine = builder.Entity<SupplierInvoiceMatchLine>();
        matchLine.HasKey(x => new { x.Id, x.TenantId });
        matchLine.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
        matchLine.Property(x => x.Verdict).HasMaxLength(50).IsRequired();
        foreach (var p in new[] { nameof(SupplierInvoiceMatchLine.Ordered), nameof(SupplierInvoiceMatchLine.Received), nameof(SupplierInvoiceMatchLine.Returned),
                                  nameof(SupplierInvoiceMatchLine.PreviouslyInvoiced), nameof(SupplierInvoiceMatchLine.Invoiced), nameof(SupplierInvoiceMatchLine.QuantityVariance) })
            matchLine.Property(p).HasPrecision(18, 2);
        matchLine.Property(x => x.OrderPrice).HasPrecision(18, 4);
        matchLine.Property(x => x.InvoicedPrice).HasPrecision(18, 4);
        matchLine.Property(x => x.PriceVariancePercent).HasPrecision(9, 2);

        var vendorReturn = builder.Entity<VendorReturn>();
        vendorReturn.HasKey(x => new { x.Id, x.TenantId });
        vendorReturn.Property(x => x.ReturnNumber).HasMaxLength(50).IsRequired();
        vendorReturn.Property(x => x.PONumber).HasMaxLength(50);
        vendorReturn.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        vendorReturn.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        vendorReturn.Property(x => x.CreditNoteNumber).HasMaxLength(50);
        vendorReturn.Property(x => x.CreditAmount).HasPrecision(18, 2);
        vendorReturn.Ignore(x => x.Value);
        vendorReturn.HasIndex(x => new { x.TenantId, x.ReturnNumber }).IsUnique();
        vendorReturn.HasIndex(x => new { x.TenantId, x.PurchaseOrderId });
        vendorReturn.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.ReturnId, x.TenantId }).OnDelete(DeleteBehavior.Cascade);
        vendorReturn.Navigation(x => x.Lines).AutoInclude();

        var vendorReturnLine = builder.Entity<VendorReturnLine>();
        vendorReturnLine.HasKey(x => new { x.Id, x.TenantId });
        vendorReturnLine.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
        vendorReturnLine.Property(x => x.ProductName).HasMaxLength(255);
        vendorReturnLine.Property(x => x.Quantity).HasPrecision(18, 2);
        vendorReturnLine.Property(x => x.UnitPrice).HasPrecision(18, 4);

        var settings = builder.Entity<PurchasingSettings>();
        settings.ToTable("PurchasingSettings");
        settings.HasKey(x => new { x.Id, x.TenantId });
        settings.Property(x => x.QuantityTolerancePercent).HasPrecision(9, 2);
        settings.Property(x => x.PriceTolerancePercent).HasPrecision(9, 2);
        settings.HasIndex(x => x.TenantId).IsUnique();

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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
