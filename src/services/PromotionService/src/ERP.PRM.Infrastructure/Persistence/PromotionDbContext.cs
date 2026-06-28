using ERP.PRM.Domain.Entities;
using ERP.PRM.Infrastructure.Models;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.PRM.Infrastructure.Persistence;

public class PromotionDbContext : DbContext
{
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<PromotionProductCache> ProductCache => Set<PromotionProductCache>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public PromotionDbContext(DbContextOptions<PromotionDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Promotion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.Property(x => x.DiscountValue).HasPrecision(18, 4);
            e.Property(x => x.MinOrderAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.Status);
            e.OwnsMany(x => x.Coupons, c =>
            {
                c.HasKey(x => x.Id);
                c.Property(x => x.Id).ValueGeneratedNever();
                c.Property(x => x.Code).IsRequired().HasMaxLength(50);
                c.HasIndex(x => x.Code).IsUnique();
                c.ToTable("Coupons");
            });
        });

        modelBuilder.Entity<LoyaltyAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.UserId).IsUnique();
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
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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
}
