using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.SO.Infrastructure.EventBus
{
    /// <summary>
    /// Consumers have no HTTP user, so SalesOrderDbContext would see no tenant (and return
    /// nothing). Payment/Warehouse/Shipping events are always about one of OUR orders, so the
    /// order itself is the authority on the tenant: look it up across tenants, then handle
    /// the message inside <c>TenantScope.Begin(tenant)</c>. If the event also carries a
    /// tenant (ITenantEvent) it must agree — a mismatch is dropped, never "fixed".
    /// </summary>
    public static class OrderTenant
    {
        public static async Task<Guid?> ResolveAsync(
            SalesOrderDbContext db, Guid orderId, string json, ILogger logger, CancellationToken ct)
        {
            var owner = await db.SalesOrders
                .IgnoreQueryFilters() // deliberate cross-tenant lookup: we don't know the tenant yet
                .Where(o => o.Id == orderId)
                .Select(o => (Guid?)o.TenantId)
                .FirstOrDefaultAsync(ct);

            if (owner is not { } tenantId || tenantId == Guid.Empty)
            {
                logger.LogWarning("Event for unknown order {OrderId} ignored (no tenant)", orderId);
                return null;
            }

            if (EventTenant.TryRead(json, out var eventTenant) && eventTenant != tenantId)
            {
                logger.LogWarning("Event for order {OrderId} carries tenant {EventTenant} but the order belongs to {OrderTenant} — ignored",
                    orderId, eventTenant, tenantId);
                return null;
            }

            return tenantId;
        }
    }
}
