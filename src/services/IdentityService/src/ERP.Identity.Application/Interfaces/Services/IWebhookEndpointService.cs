using ERP.Identity.Application.DTOs.Webhook;

namespace ERP.Identity.Application.Interfaces.Services
{
    public interface IWebhookEndpointService
    {
        Task<IReadOnlyList<WebhookEndpointDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct);
        Task<WebhookEndpointDto> CreateAsync(Guid tenantId, CreateWebhookEndpointRequest request, CancellationToken ct);

        /// <summary>Throws UnauthorizedAccessException if the endpoint belongs to a
        /// different tenant than <paramref name="callerTenantId"/>.</summary>
        Task UpdateAsync(Guid id, Guid callerTenantId, UpdateWebhookEndpointRequest request, CancellationToken ct);
        Task ActivateAsync(Guid id, Guid callerTenantId, CancellationToken ct);
        Task DeactivateAsync(Guid id, Guid callerTenantId, CancellationToken ct);
        Task DeleteAsync(Guid id, Guid callerTenantId, CancellationToken ct);
    }
}
