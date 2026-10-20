using ERP.Shared.Infrastructure.Data;
using ERP.MST.Domain.Entities;
using ERP.MST.Domain.Entities.Settings;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.MST.Infrastructure.Persistence
{
    public class MasterDataDbContext : DbContext, ITenantScopedDbContext
    {
        public DbSet<Department> Departments { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<Province> Provinces { get; set; }
        public DbSet<District> Districts { get; set; }
        public DbSet<Ward> Wards { get; set; }
        public DbSet<SupportedLanguage> SupportedLanguages { get; set; }

        public DbSet<DepartmentTranslation> DepartmentTranslations { get; set; }
        public DbSet<PositionTranslation> PositionTranslations { get; set; }
        public DbSet<UnitOfMeasureTranslation> UnitOfMeasureTranslations { get; set; }
        public DbSet<CountryTranslation> CountryTranslations { get; set; }
        public DbSet<ProvinceTranslation> ProvinceTranslations { get; set; }
        public DbSet<DistrictTranslation> DistrictTranslations { get; set; }
        public DbSet<WardTranslation> WardTranslations { get; set; }

        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<SystemCurrencySettings> SystemCurrencySettings { get; set; }
        public DbSet<TenantCurrencySettings> TenantCurrencySettings { get; set; }

        public DbSet<Company> Companies { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Division> Divisions { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<BusinessUnit> BusinessUnits { get; set; }
        public DbSet<CostCenter> CostCenters { get; set; }
        public DbSet<ProfitCenter> ProfitCenters { get; set; }
        public DbSet<OrganizationCalendar> OrganizationCalendars { get; set; }
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<EmployeeTeamAssignment> EmployeeTeamAssignments { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<ExchangeRate> ExchangeRates { get; set; }
        public DbSet<PaymentTerm> PaymentTerms { get; set; }

        private readonly ITenantProvider _tenantProvider;

        /// <summary>
        /// Tenant of the current request (JWT / Gateway host tenant) or consumer message
        /// (TenantScope). Read by the tenant query filter on every query and used to
        /// stamp/validate every save.
        /// </summary>
        public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

        public MasterDataDbContext(DbContextOptions<MasterDataDbContext> options, ITenantProvider tenantProvider) : base(options)
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

            modelBuilder.Entity<SystemCurrencySettings>(e =>
            {
                e.ToTable("SystemCurrencySettings");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<TenantCurrencySettings>(e =>
            {
                e.ToTable("TenantCurrencySettings");
                e.HasKey(x => x.Id);
                e.Property(x => x.DefaultCurrencyCode).IsRequired().HasMaxLength(3);
                e.Property(x => x.ExchangeRateSource).IsRequired().HasMaxLength(50);
                e.HasIndex(x => x.TenantId).IsUnique();
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
                    // Must be assembly-qualified — see the identical fix/explanation in
                    // HRMService's HrmDbContext.cs (same bug: Type.GetType() in the
                    // OutboxProcessor's assembly can't resolve a bare FullName from a
                    // different assembly, so a bare FullName here means every domain
                    // event silently never gets published — including EmployeeHired,
                    // which NotificationService's live mst-topic consumer expects).
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
