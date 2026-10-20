using ERP.Identity.Application.DTOs.Tenant;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TenantService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IReadOnlyList<TenantDto>> ListAllAsync(CancellationToken ct)
        {
            var tenants = await _unitOfWork.Tenants.ListAllAsync(ct);
            return tenants.Select(t => new TenantDto(t.Id, t.TenantCode, t.TenantName, t.Status)).ToList();
        }

        public async Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id, ct);
            if (tenant == null)
                return null;

            return new TenantDto(tenant.Id, tenant.TenantCode, tenant.TenantName, tenant.Status);
        }

        public async Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct)
        {
            var existingTenant = await _unitOfWork.Tenants.ExistsByCodeAsync(request.TenantCode, ct);
            if (existingTenant)
                throw new InvalidOperationException($"Tenant with code '{request.TenantCode}' already exists");

            var tenant = AuthTenant.Create(request.TenantCode, request.TenantName);
            await _unitOfWork.Tenants.AddAsync(tenant, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new TenantDto(tenant.Id, tenant.TenantCode, tenant.TenantName, tenant.Status);
        }

        public async Task UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id, ct);
            if (tenant == null)
                throw new InvalidOperationException($"Tenant with id '{id}' not found");

            tenant.TenantName = request.TenantName;
            await _unitOfWork.Tenants.UpdateAsync(tenant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task ActivateAsync(Guid id, CancellationToken ct)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id, ct);
            if (tenant == null)
                throw new InvalidOperationException($"Tenant with id '{id}' not found");

            tenant.Activate();
            await _unitOfWork.Tenants.UpdateAsync(tenant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task DeactivateAsync(Guid id, CancellationToken ct)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id, ct);
            if (tenant == null)
                throw new InvalidOperationException($"Tenant with id '{id}' not found");

            tenant.Deactivate();
            await _unitOfWork.Tenants.UpdateAsync(tenant, ct);
            // A deactivated tenant's users must lose access in every service now.
            _unitOfWork.Revocations.TenantChanged(tenant.Id);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
