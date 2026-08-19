using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IWebhookEndpointRepository : IGenericRepository<WebhookEndpoint>
    {
        Task<IReadOnlyList<WebhookEndpoint>> ListByTenantAsync(Guid tenantId, CancellationToken ct);

        /// <summary>Every active endpoint for a tenant — event-type filtering happens in
        /// application code (WebhookEndpoint.Matches), since EventTypesRaw is a delimited
        /// string not safely queryable via a SQL substring match.</summary>
        Task<IReadOnlyList<WebhookEndpoint>> ListActiveByTenantAsync(Guid tenantId, CancellationToken ct);
    }
}
