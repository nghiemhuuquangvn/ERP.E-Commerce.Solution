using ERP.Shared.Core.Domain;

namespace ERP.PRM.Infrastructure.Models;

public class PromotionProductCache : ITenantOwned
{
    public Guid ProductId { get; set; }
    /// <summary>Owning tenant — stamped by PromotionDbContext on insert, from the ProductCreated event's TenantScope (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime SyncedAt { get; set; }
}
