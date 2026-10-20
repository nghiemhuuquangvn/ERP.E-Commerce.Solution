using ERP.Shared.Infrastructure.Data;
using ERP.PRM.Domain.Entities;
using ERP.PRM.Infrastructure.Models;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;

namespace ERP.PRM.Infrastructure.Persistence;

public class PromotionDbContext : DbContext, ITenantScopedDbContext
{
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<FlashSale> FlashSales => Set<FlashSale>();
    public DbSet<FlashSaleItem> FlashSaleItems => Set<FlashSaleItem>();
    public DbSet<PromotionProductCache> ProductCache => Set<PromotionProductCache>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    private readonly ITenantProvider _tenantProvider;

    /// <summary>
    /// Tenant of the current request (JWT / Gateway host tenant) or consumer message
    /// (TenantScope). Read by the tenant query filter on every query and used to
    /// stamp/validate every save.
    /// </summary>
    public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

    public PromotionDbContext(DbContextOptions<PromotionDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Promotion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.Property(x => x.DiscountValue).HasPrecision(18, 4);
            e.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);
            e.Property(x => x.MinOrderAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.Status, x.StartDate, x.EndDate });

            // Store List<Guid>/List<string> as JSON strings
            e.Property(x => x.ApplicableProductIds)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<Guid>>(v) ?? new List<Guid>())
                .Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            e.Property(x => x.ApplicableCategoryIds)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v),
                    v => JsonConvert.DeserializeObject<List<string>>(v) ?? new List<string>())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            e.OwnsMany(x => x.Coupons, c =>
            {
                c.HasKey(x => x.Id);
                c.Property(x => x.Id).ValueGeneratedNever();
                c.Property(x => x.Code).IsRequired().HasMaxLength(50);
                c.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); // coupon codes are per tenant
                c.ToTable("Coupons");
            });
        });

        modelBuilder.Entity<LoyaltyAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasMany(x => x.Transactions)
                .WithOne()
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoyaltyTransaction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.TransactionType).HasMaxLength(20);
            e.HasIndex(x => x.AccountId);
            e.HasIndex(x => x.UserId);
            e.ToTable("LoyaltyTransactions");
        });

        modelBuilder.Entity<FlashSale>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.Status, x.StartTime, x.EndTime });
            e.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.FlashSaleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.ToTable("FlashSales");
        });

        modelBuilder.Entity<FlashSaleItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ProductName).IsRequired().HasMaxLength(300);
            e.Property(x => x.OriginalPrice).HasPrecision(18, 2);
            e.Property(x => x.SalePrice).HasPrecision(18, 2);
            e.HasIndex(x => new { x.FlashSaleId, x.ProductId }).IsUnique();
            e.ToTable("FlashSaleItems");
        });

        modelBuilder.Entity<PromotionProductCache>(e =>
        {
            e.HasKey(x => x.ProductId);
            e.Property(x => x.ProductId).ValueGeneratedNever();
            e.Property(x => x.Category).IsRequired().HasMaxLength(200);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasIndex(x => x.Category);
            e.ToTable("PromotionProductCache");
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
