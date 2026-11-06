using ERP.MFG.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.MFG.Infrastructure.Persistence;

/// <summary>
/// ManufacturingDB (PostgreSQL). Tenant isolation as in every service: query filter on every tenant-owned
/// entity (last in <see cref="OnModelCreating"/>) and <c>EnforceTenantOnSave</c> on both save paths.
/// The schema is created by the SQL scripts in <c>src/Database/Manufacturing</c>, never by EF.
/// </summary>
public class MfgDbContext : DbContext, ITenantScopedDbContext
{
    private readonly ITenantProvider _tenantProvider;

    public MfgDbContext(DbContextOptions<MfgDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentSequence>(e =>
        {
            e.ToTable("DocumentSequences");
            e.Property(x => x.DocType).HasMaxLength(10).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.DocType, x.PlantId, x.Period }).IsUnique();
        });

        ConfigureBaseEntities(modelBuilder);

        // Last, so it composes with any per-entity filter configured above.
        modelBuilder.ApplyTenantQueryFilters(this);
    }

    /// <summary>
    /// Common mapping of everything that derives from <see cref="BaseEntity"/>: ids come from the code, the SQL Server
    /// <c>RowVersion</c> and the domain event list are not columns, and optimistic locking uses PostgreSQL's
    /// <c>xmin</c> system column (a changed row = a new xmin, BR-GEN-03).
    /// </summary>
    private static void ConfigureBaseEntities(ModelBuilder modelBuilder)
    {
        foreach (var type in modelBuilder.Model.GetEntityTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)).ToList())
        {
            var entity = modelBuilder.Entity(type.ClrType);
            entity.Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
            entity.Ignore(nameof(BaseEntity.RowVersion));
            entity.Ignore(nameof(BaseEntity.DomainEvents));
            entity.Property(nameof(BaseEntity.Description)).HasMaxLength(2000);
            entity.Property(nameof(BaseEntity.CreatedBy)).HasMaxLength(200);
            entity.Property(nameof(BaseEntity.UpdatedBy)).HasMaxLength(200);
            entity.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        }
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
