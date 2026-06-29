using ERP.PRD.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.PRD.Infrastructure.Persistence
{
    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<PriceList> PriceLists => Set<PriceList>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply entity configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Outbox table configuration
            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.OccurredOnUtc).IsRequired();

                // Index for pending messages (used by OutboxProcessor)
                entity.HasIndex(e => e.ProcessedOnUtc)
                    .HasName("IX_OutboxMessages_ProcessedOnUtc_Pending")
                    .HasFilter("[ProcessedOnUtc] IS NULL");
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Capture domain events before saving
            var entries = ChangeTracker.Entries<BaseEntity>();
            var domainEvents = new List<IDomainEvent>();

            foreach (var entry in entries)
            {
                if (entry.Entity.DomainEvents.Any())
                {
                    domainEvents.AddRange(entry.Entity.DomainEvents);
                }
            }

            // Save entities
            var result = await base.SaveChangesAsync(cancellationToken);

            // Store domain events in Outbox
            foreach (var domainEvent in domainEvents)
            {
                var outboxMessage = new OutboxMessage(
                    type: domainEvent.GetType().FullName!,
                    content: JsonConvert.SerializeObject(domainEvent, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })
                );

                await OutboxMessages.AddAsync(outboxMessage, cancellationToken);
            }

            // Save outbox messages
            if (domainEvents.Any())
            {
                await base.SaveChangesAsync(cancellationToken);
            }

            // Clear domain events from entities
            foreach (var entry in entries)
            {
                entry.Entity.ClearDomainEvents();
            }

            return result;
        }
    }
}
