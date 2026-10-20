using ERP.CRM.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.CRM.Infrastructure.Persistence
{
    public class CrmDbContext : DbContext, ITenantScopedDbContext
    {
        private readonly ITenantProvider _tenantProvider;

        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Lead> Leads { get; set; }
        public DbSet<Opportunity> Opportunities { get; set; }
        public DbSet<Case> Cases { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<ContactTag> ContactTags { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        public CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantProvider tenantProvider) : base(options)
        {
            _tenantProvider = tenantProvider;
        }

        /// <summary>
        /// Tenant of the current request (JWT) or consumer message (TenantScope). Read by the
        /// tenant query filter on every query and used to stamp/validate every save — CRM had
        /// no tenant isolation at all before 2026-10-20 (every tenant shared all contacts,
        /// leads, opportunities and cases).
        /// </summary>
        public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(m => m.ProcessedOnUtc)
                .HasFilter("[ProcessedOnUtc] IS NULL");

            modelBuilder.Entity<Activity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Subject).IsRequired().HasMaxLength(300);
                e.Property(x => x.Notes).HasMaxLength(4000);
                e.Property(x => x.Source).HasMaxLength(200);
                e.HasIndex(x => x.ContactId);
                e.HasIndex(x => new { x.ContactId, x.Status });
                e.HasIndex(x => x.OpportunityId);
                e.HasIndex(x => x.CreatedAt);
            });

            modelBuilder.Entity<ContactTag>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Tag).IsRequired().HasMaxLength(50);
                e.HasIndex(x => new { x.ContactId, x.Tag }).IsUnique();
                e.HasIndex(x => x.Tag);
            });

            // Last, so it composes with any per-entity filter configured above.
            modelBuilder.ApplyTenantQueryFilters(this);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ChangeTracker.EnforceTenantOnSave(CurrentTenantId);

            var aggregateRoots = ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.Entity.DomainEvents.Count != 0)
                .Select(e => e.Entity)
                .ToList();

            var domainEvents = aggregateRoots
                .SelectMany(aggregate => aggregate.DomainEvents)
                .ToList();

            foreach (var domainEvent in domainEvents)
            {
                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    // Must be assembly-qualified — see the identical fix/explanation in
                    // HRMService's HrmDbContext.cs (same bug: Type.GetType() in the
                    // OutboxProcessor's assembly can't resolve a bare FullName from a
                    // different assembly, so a bare FullName here means every domain
                    // event silently never gets published).
                    Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().Name,
                    Content = JsonConvert.SerializeObject(domainEvent, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    }),
                    OccurredOnUtc = domainEvent.OccurredOn,
                    ProcessedOnUtc = null
                };

                await OutboxMessages.AddAsync(outboxMessage, cancellationToken);
            }

            var result = await base.SaveChangesAsync(cancellationToken);

            foreach (var aggregateRoot in aggregateRoots)
                aggregateRoot.ClearDomainEvents();

            return result;
        }
    }
}
