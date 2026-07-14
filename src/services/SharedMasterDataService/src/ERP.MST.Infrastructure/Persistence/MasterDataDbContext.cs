using ERP.MST.Domain.Entities;
using ERP.MST.Domain.Entities.Settings;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.MST.Infrastructure.Persistence
{
    public class MasterDataDbContext : DbContext
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

        public MasterDataDbContext(DbContextOptions<MasterDataDbContext> options) : base(options)
        {
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
