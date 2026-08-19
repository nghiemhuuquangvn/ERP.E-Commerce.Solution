using System;

namespace ERP.Identity.Application.DTOs.Auth
{
    public class TenantSignupDto
    {
        /// <summary>
        /// Self-service "create my own tenant" — the entry point a brand-new deployment (or a
        /// brand-new customer of a multi-tenant deployment) uses to provision its first real
        /// tenant + admin user without an existing SuperAdmin. Distinct from
        /// TenantsController.Create (admin-only, tenant row only, no user/roles) and from
        /// RegisterDto.RegisterRequest (requires an already-existing TenantCode).
        /// </summary>
        public record TenantSignupRequest(
            string TenantCode,
            string TenantName,
            string AdminEmail,
            string AdminPassword,
            string? AdminFullName = null);
    }
}
