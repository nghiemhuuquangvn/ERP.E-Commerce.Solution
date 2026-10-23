using ERP.Identity.Application.DTOs.Webhook;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Shared.Security;

namespace ERP.Identity.Infrastructure.Services
{
    public class WebhookEndpointService : IWebhookEndpointService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISecretProtector _secretProtector;

        public WebhookEndpointService(IUnitOfWork unitOfWork, ISecretProtector secretProtector)
        {
            _unitOfWork = unitOfWork;
            _secretProtector = secretProtector;
        }

        private string? Protect(string? secret) =>
            string.IsNullOrEmpty(secret) ? null : SecretEnvelope.Seal(_secretProtector, secret);

        public async Task<IReadOnlyList<WebhookEndpointDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct)
        {
            var endpoints = await _unitOfWork.WebhookEndpoints.ListByTenantAsync(tenantId, ct);
            return endpoints.Select(ToDto).ToList();
        }

        public async Task<WebhookEndpointDto> CreateAsync(Guid tenantId, CreateWebhookEndpointRequest request, CancellationToken ct)
        {
            var endpoint = WebhookEndpoint.Create(tenantId, request.Name, request.Url, Protect(request.Secret), request.EventTypes ?? new List<string>());
            await _unitOfWork.WebhookEndpoints.AddAsync(endpoint, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToDto(endpoint);
        }

        public async Task UpdateAsync(Guid id, Guid callerTenantId, UpdateWebhookEndpointRequest request, CancellationToken ct)
        {
            var endpoint = await GetOwnedAsync(id, callerTenantId, ct);
            endpoint.Update(request.Name, request.Url, Protect(request.Secret), request.EventTypes ?? new List<string>());
            await _unitOfWork.WebhookEndpoints.UpdateAsync(endpoint, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task ActivateAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            var endpoint = await GetOwnedAsync(id, callerTenantId, ct);
            endpoint.Activate();
            await _unitOfWork.WebhookEndpoints.UpdateAsync(endpoint, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task DeactivateAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            var endpoint = await GetOwnedAsync(id, callerTenantId, ct);
            endpoint.Deactivate();
            await _unitOfWork.WebhookEndpoints.UpdateAsync(endpoint, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            var endpoint = await GetOwnedAsync(id, callerTenantId, ct);
            await _unitOfWork.WebhookEndpoints.DeleteAsync(endpoint, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        private async Task<WebhookEndpoint> GetOwnedAsync(Guid id, Guid callerTenantId, CancellationToken ct)
        {
            var endpoint = await _unitOfWork.WebhookEndpoints.GetByIdAsync(id, ct)
                ?? throw new InvalidOperationException("Webhook endpoint not found");

            if (endpoint.TenantId != callerTenantId)
                throw new UnauthorizedAccessException("Webhook endpoint belongs to a different tenant");

            return endpoint;
        }

        private static WebhookEndpointDto ToDto(WebhookEndpoint w) =>
            new(w.Id, w.TenantId, w.Name, w.Url, !string.IsNullOrEmpty(w.Secret), w.IsActive, w.EventTypes, w.CreatedAt);
    }
}
