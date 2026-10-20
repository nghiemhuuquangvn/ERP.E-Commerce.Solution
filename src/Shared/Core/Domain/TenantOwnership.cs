namespace ERP.Shared.Core.Domain
{
    /// <summary>
    /// A row that belongs to exactly one tenant. Every DbContext that maps such a type must be
    /// tenant-scoped (ERP.Shared.Infrastructure.Data.ITenantScopedDbContext): the tenant query
    /// filter and EnforceTenantOnSave work on this interface, so plain POCOs (Warehouse,
    /// Reporting) are isolated exactly like <see cref="BaseEntity"/>.
    /// </summary>
    public interface ITenantOwned
    {
        Guid TenantId { get; }
    }

    /// <summary>
    /// Marker for platform-wide rows (the System*Settings singletons, TenantId = Guid.Empty):
    /// never tenant-filtered; only the platform tenant (or a job/seed with no tenant) may
    /// change or delete them.
    /// </summary>
    public interface IPlatformWideEntity
    {
    }

    /// <summary>
    /// Marker for reference data shared by every tenant (countries, languages, currencies,
    /// units of measure): a tenant reads the platform tenant's rows plus its own additions,
    /// and only the platform tenant may change the shared rows.
    /// </summary>
    public interface ISharedReferenceData : ITenantOwned
    {
    }

    /// <summary>
    /// An event that crosses a service boundary carries the tenant it belongs to, so the
    /// consumer can run its handling in that tenant (TenantScope). Outbox writers stamp it
    /// from the entity that raised the event when the event didn't set it itself
    /// (ERP.Shared.Messaging.EventTenant.Stamp).
    /// </summary>
    public interface ITenantEvent
    {
        Guid TenantId { get; set; }
    }

    /// <summary>Well-known tenant ids shared by every layer.</summary>
    public static class WellKnownTenants
    {
        /// <summary>The platform (SYSTEM) tenant: owns platform administration and the
        /// shared reference data.</summary>
        public static readonly Guid Platform = new("00000000-0000-0000-0000-000000000001");
    }
}
