using ERP.CRM.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.CRM.Infrastructure.Persistence
{
    public class CrmDbContext : DbContext
    {
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Lead> Leads { get; set; }
        public DbSet<Opportunity> Opportunities { get; set; }
        public DbSet<Case> Cases { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(m => m.ProcessedOnUtc)
                .HasFilter("[ProcessedOnUtc] IS NULL");
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
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
                    Type = domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
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
