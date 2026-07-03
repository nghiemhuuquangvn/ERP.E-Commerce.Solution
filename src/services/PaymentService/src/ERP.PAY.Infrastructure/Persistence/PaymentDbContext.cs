using ERP.PAY.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.PAY.Infrastructure.Persistence;

public class PaymentDbContext : DbContext
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SavedPaymentMethod> SavedPaymentMethods => Set<SavedPaymentMethod>();
    public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            e.Property(x => x.TransactionRef).HasMaxLength(50);
            e.Property(x => x.GatewayTransactionId).HasMaxLength(200);
            e.Property(x => x.GatewayProvider).HasMaxLength(100);
            e.Property(x => x.FailureReason).HasMaxLength(1000);
            e.Property(x => x.FraudReviewReason).HasMaxLength(500);
            e.HasIndex(x => x.OrderId).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.TransactionRef);
            e.OwnsMany(x => x.Refunds, r =>
            {
                r.HasKey(x => x.Id);
                r.Property(x => x.Id).ValueGeneratedNever();
                r.Property(x => x.Amount).HasPrecision(18, 2);
                r.Property(x => x.Reason).IsRequired().HasMaxLength(500);
                r.Property(x => x.GatewayRefundId).HasMaxLength(200);
                r.Property(x => x.FailureReason).HasMaxLength(500);
                r.ToTable("PaymentRefunds");
            });
        });

        modelBuilder.Entity<Settlement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<SavedPaymentMethod>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Provider).IsRequired().HasMaxLength(50);
            e.Property(x => x.Token).IsRequired().HasMaxLength(500);
            e.Property(x => x.Brand).HasMaxLength(50);
            e.Property(x => x.Last4).HasMaxLength(4);
            e.HasIndex(x => x.CustomerId);
        });

        modelBuilder.Entity<InstallmentPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.PaymentId).IsUnique();
            e.OwnsMany(x => x.Installments, i =>
            {
                i.WithOwner().HasForeignKey("InstallmentPlanId");
                i.HasKey(x => x.Id);
                i.Property(x => x.Id).ValueGeneratedNever();
                i.Property(x => x.Amount).HasPrecision(18, 2);
                i.ToTable("InstallmentLines");
            });
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.ProcessedOnUtc);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEntities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any()).ToList();

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
}
