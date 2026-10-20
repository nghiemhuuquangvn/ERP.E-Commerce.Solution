using ERP.Shared.Infrastructure.Data;
using ERP.POS.Application.EventHandlers;
using ERP.POS.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.POS.Infrastructure.Persistence;

public class POSDbContext : DbContext, ITenantScopedDbContext
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<PosProductCache> ProductCache => Set<PosProductCache>();
    public DbSet<PosPromotionCache> PromotionCache => Set<PosPromotionCache>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    private readonly ITenantProvider _tenantProvider;

    /// <summary>
    /// Tenant of the current request (JWT / Gateway host tenant) or consumer message
    /// (TenantScope). Read by the tenant query filter on every query and used to
    /// stamp/validate every save.
    /// </summary>
    public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

    public POSDbContext(DbContextOptions<POSDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.SubTotal).HasPrecision(18, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            e.Property(x => x.TaxAmount).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.VoidReason).HasMaxLength(500);
            e.HasIndex(x => x.TerminalId);
            e.HasIndex(x => x.Status);
            e.OwnsMany(x => x.Items, item =>
            {
                item.HasKey(i => i.Id);
                item.Property(i => i.Id).ValueGeneratedNever();
                item.Property(i => i.ProductName).IsRequired().HasMaxLength(300);
                item.Property(i => i.UnitPrice).HasPrecision(18, 2);
                item.Property(i => i.TaxRate).HasPrecision(5, 2);
                item.Property(i => i.DiscountAmount).HasPrecision(18, 2);
                item.Property(i => i.LineTotal).HasPrecision(18, 2);
                item.Property(i => i.TaxAmount).HasPrecision(18, 2);
                item.ToTable("TransactionItems");
            });
            e.OwnsMany(x => x.Tenders, tender =>
            {
                tender.HasKey(t => t.Id);
                tender.Property(t => t.Id).ValueGeneratedNever();
                tender.Property(t => t.PaymentMethod).IsRequired().HasMaxLength(50);
                tender.Property(t => t.Amount).HasPrecision(18, 2);
                tender.ToTable("TransactionTenders");
            });
        });

        modelBuilder.Entity<CashShift>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.OpeningBalance).HasPrecision(18, 2);
            e.Property(x => x.ClosingBalance).HasPrecision(18, 2);
            e.Property(x => x.TotalSales).HasPrecision(18, 2);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.TerminalId, x.Status });
            e.OwnsMany(x => x.Denominations, d =>
            {
                d.HasKey(x => x.Id);
                d.Property(x => x.Id).ValueGeneratedNever();
                d.Property(x => x.Denomination).HasPrecision(18, 2);
                d.ToTable("CashDenominationLines");
            });
        });

        modelBuilder.Entity<Terminal>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<PosProductCache>(e =>
        {
            e.HasKey(x => x.ProductId);
            e.Property(x => x.ProductId).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.Property(x => x.Sku).IsRequired().HasMaxLength(100);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasIndex(x => x.Sku);
            e.HasIndex(x => x.IsActive);
            e.ToTable("PosProductCache");
        });

        modelBuilder.Entity<PosPromotionCache>(e =>
        {
            e.HasKey(x => x.PromotionId);
            e.Property(x => x.PromotionId).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.Property(x => x.DiscountValue).HasPrecision(18, 4);
            e.HasIndex(x => new { x.StartDate, x.EndDate });
            e.ToTable("PosPromotionCache");
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        // Last, so it composes with any per-entity filter configured above.
        modelBuilder.ApplyTenantQueryFilters(this);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Stamp/validate the tenant first, so the events serialized below carry it.
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            EventTenant.StampFrom(entry.Entity);

        var domainEntities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any()).ToList();

        var outboxMessages = domainEntities.SelectMany(e => e.Entity.DomainEvents)
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
}
