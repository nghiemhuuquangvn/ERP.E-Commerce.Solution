using ERP.Shared.Infrastructure.Data;
using ERP.HRM.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.HRM.Infrastructure.Persistence
{
    public class HrmDbContext : DbContext, ITenantScopedDbContext
    {
        public DbSet<EmployeeSnapshot> EmployeeSnapshots { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<PaySlip> PaySlips { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<WorkShift> WorkShifts { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        private readonly ITenantProvider _tenantProvider;

        /// <summary>
        /// Tenant of the current request (JWT / Gateway host tenant) or consumer message
        /// (TenantScope). Read by the tenant query filter on every query and used to
        /// stamp/validate every save.
        /// </summary>
        public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

        public HrmDbContext(DbContextOptions<HrmDbContext> options, ITenantProvider tenantProvider) : base(options)
        {
            _tenantProvider = tenantProvider;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(m => m.ProcessedOnUtc)
                .HasFilter("[ProcessedOnUtc] IS NULL");

            modelBuilder.Entity<AttendanceRecord>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.TotalHours).HasColumnType("decimal(6,2)");
                e.Property(x => x.OvertimeHours).HasColumnType("decimal(6,2)");
                e.Property(x => x.Notes).HasMaxLength(500);
                e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
                e.HasIndex(x => x.EmployeeId);
                e.HasIndex(x => x.Date);
            });

            modelBuilder.Entity<WorkShift>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Name).IsRequired().HasMaxLength(100);
                e.Ignore(x => x.ShiftHours);
                e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique(); // shift names are per tenant
            });

            modelBuilder.Entity<EmployeeSnapshot>(e =>
            {
                e.Property(x => x.EmployeeNumber).HasMaxLength(50);
                e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
                e.Property(x => x.Email).HasMaxLength(256);
            });

            modelBuilder.Entity<PaySlip>(e =>
            {
                e.Property(x => x.OvertimeHours).HasColumnType("decimal(6,2)");
                e.Property(x => x.OvertimePay).HasColumnType("decimal(18,2)");
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
                    // Must be assembly-qualified: OutboxProcessor.cs's Type.GetType(message.Type)
                    // runs in ERP.HRM.Infrastructure, a different assembly than these domain
                    // events (ERP.HRM.Domain) — Type.GetType's simple-name lookup only searches
                    // the calling assembly + mscorlib, so a bare FullName silently never
                    // resolved (every HRM outbox message was logged "Event type not found" and
                    // marked processed anyway — no HRM integration event has ever actually
                    // published). Matches the AssemblyQualifiedName pattern PurchaseOrderService
                    // and NotificationService's DbContexts already use correctly.
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
