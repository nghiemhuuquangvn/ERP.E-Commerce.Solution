using ERP.Shared.Infrastructure.Data;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Entities.Settings;
using ERP.PRD.Domain.Events;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.PRD.Infrastructure.Persistence
{
    public class ProductDbContext : DbContext, ITenantScopedDbContext
    {
        private readonly ITenantProvider _tenantProvider;

        /// <summary>
        /// Tenant of the current request (JWT / Gateway host tenant) or consumer message
        /// (TenantScope). Read by the tenant query filter on every query and used to
        /// stamp/validate every save.
        /// </summary>
        public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

        public ProductDbContext(DbContextOptions<ProductDbContext> options, ITenantProvider tenantProvider) : base(options)
        {
            _tenantProvider = tenantProvider;
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
        public DbSet<ProductBundleItem> ProductBundleItems => Set<ProductBundleItem>();
        public DbSet<ProductRelation> ProductRelations => Set<ProductRelation>();
        public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
        public DbSet<Brand> Brands => Set<Brand>();
        public DbSet<ProductUomConversion> ProductUomConversions => Set<ProductUomConversion>();
        public DbSet<SystemStoreProductSettings> SystemStoreProductSettings => Set<SystemStoreProductSettings>();
        public DbSet<TenantStoreProductSettings> TenantStoreProductSettings => Set<TenantStoreProductSettings>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply entity configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<SystemStoreProductSettings>(entity =>
            {
                entity.ToTable("SystemStoreProductSettings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<TenantStoreProductSettings>(entity =>
            {
                entity.ToTable("TenantStoreProductSettings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.StoreName).HasMaxLength(200);
                entity.Property(e => e.StoreDescription).HasMaxLength(2000);
                entity.Property(e => e.LogoUrl).HasMaxLength(500);
                entity.HasIndex(e => e.TenantId).IsUnique();
            });

            // Outbox table configuration
            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.OccurredOnUtc).IsRequired();

                // Index for pending messages (used by OutboxProcessor)
                entity.HasIndex(e => e.ProcessedOnUtc)
                    .HasDatabaseName("IX_OutboxMessages_ProcessedOnUtc_Pending")
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

            modelBuilder.Entity<ProductBundleItem>(entity =>
            {
                entity.ToTable("ProductBundleItems");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.BundleProductId);
                entity.HasIndex(e => new { e.BundleProductId, e.ComponentProductId }).IsUnique();
            });

            modelBuilder.Entity<ProductRelation>(entity =>
            {
                entity.ToTable("ProductRelations");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ProductId, e.RelationType });
                entity.HasIndex(e => new { e.ProductId, e.RelatedProductId, e.RelationType }).IsUnique();
            });

            modelBuilder.Entity<ProductReview>(entity =>
            {
                entity.ToTable("ProductReviews");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Comment).HasMaxLength(2000);
                entity.HasIndex(e => new { e.ProductId, e.IsApproved });
            });

            modelBuilder.Entity<Brand>(entity =>
            {
                entity.ToTable("Brands");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.LogoUrl).HasMaxLength(500);
                entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasFilter("[IsDeleted] = 0"); // brand codes are per tenant
            });

            modelBuilder.Entity<ProductUomConversion>(entity =>
            {
                entity.ToTable("ProductUomConversions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ConversionFactor).HasColumnType("decimal(18,6)");
                entity.HasIndex(e => e.ProductId);
                entity.HasIndex(e => new { e.ProductId, e.FromUomId, e.ToUomId }).IsUnique();
            });

            // PriceListItem has no DbSet (only reachable via PriceList.Items) and no
            // other configuration, so by convention EF would map it to a table named
            // after the bare CLR class name ("PriceListItem") — but the actual table
            // is named "PriceListItems" (plural), causing "Invalid object name
            // 'PriceListItem'" on every query that touches PriceList.Items. Every other
            // column (including the shadow FK PriceListId) already matches convention.
            modelBuilder.Entity<PriceListItem>(entity =>
            {
                entity.ToTable("PriceListItems");
                entity.HasKey(e => e.Id);
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
}
