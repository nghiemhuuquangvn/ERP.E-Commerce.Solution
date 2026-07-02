using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Events;
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
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<PriceList> PriceLists => Set<PriceList>();
        public DbSet<ProductTranslation> ProductTranslations => Set<ProductTranslation>();
        public DbSet<CategoryTranslation> CategoryTranslations => Set<CategoryTranslation>();
        public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
        public DbSet<ProductAttributeOption> ProductAttributeOptions => Set<ProductAttributeOption>();
        public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
        public DbSet<ProductExternalMapping> ProductExternalMappings => Set<ProductExternalMapping>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<SearchIndexQueueEntry> SearchIndexQueue => Set<SearchIndexQueueEntry>();

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

            // SearchIndexQueue table configuration (internal processing queue for the
            // derived Elasticsearch index — mirrors OutboxMessage's minimal shape)
            modelBuilder.Entity<SearchIndexQueueEntry>(entity =>
            {
                entity.ToTable("SearchIndexQueue");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.RequestedAt).IsRequired();

                entity.HasIndex(e => e.IndexedAt)
                    .HasDatabaseName("IX_SearchIndexQueue_Pending")
                    .HasFilter("[IndexedAt] IS NULL");
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

            // ProductSearchIndexRequestedDomainEvent is a derived-index-refresh signal, not an
            // integration event — it never goes to Kafka/OutboxMessages. Instead it upserts/
            // bumps a row in SearchIndexQueue (deduplicated by ProductId), polled by
            // SearchIndexProcessor. Every other domain event type keeps flowing through the
            // OutboxMessages path completely unchanged.
            var searchIndexEvents = domainEvents.OfType<ProductSearchIndexRequestedDomainEvent>().ToList();
            var otherEvents = domainEvents.Except(searchIndexEvents).ToList();

            // Store outbox-bound domain events in Outbox
            foreach (var domainEvent in otherEvents)
            {
                var outboxMessage = new OutboxMessage(
                    type: domainEvent.GetType().FullName!,
                    content: JsonConvert.SerializeObject(domainEvent, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })
                );

                await OutboxMessages.AddAsync(outboxMessage, cancellationToken);
            }

            // Upsert/bump SearchIndexQueue rows for the affected ProductIds, batched to avoid N+1
            if (searchIndexEvents.Any())
            {
                var affectedProductIds = searchIndexEvents.Select(e => e.ProductId).Distinct().ToList();
                var now = DateTime.UtcNow;

                var existingPending = await SearchIndexQueue
                    .Where(q => q.IndexedAt == null && affectedProductIds.Contains(q.ProductId))
                    .ToListAsync(cancellationToken);

                var existingByProductId = existingPending
                    .GroupBy(q => q.ProductId)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var productId in affectedProductIds)
                {
                    if (existingByProductId.TryGetValue(productId, out var pendingEntry))
                    {
                        pendingEntry.RequestedAt = now;
                    }
                    else
                    {
                        await SearchIndexQueue.AddAsync(new SearchIndexQueueEntry
                        {
                            ProductId = productId,
                            RequestedAt = now
                        }, cancellationToken);
                    }
                }
            }

            // Save outbox messages / search index queue rows
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
