using ERP.Shared.Authorization;

namespace ERP.SO.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMiddleware(this IServiceCollection services)
        {
            // Empty starting point — UserContextHeaderMiddleware fully repopulates every
            // field from IUserContextService.GetCurrentUser() (JWT claims) on every
            // request before any controller runs, so no prefill is needed here. This
            // used to read X-User-Id/X-Permissions/X-Roles/X-Token-Expires headers
            // directly, which is exactly the forgeable trust boundary this fix closes.
            services.AddScoped<UserContext>();
            services.AddHttpContextAccessor();
            services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
            // Tenant isolation for SalesOrderDbContext: JWT tenant, Gateway host tenant for
            // anonymous requests, or an explicit TenantScope set by consumers/jobs.
            services.AddRequestTenantProvider();

            return services;
        }
    }
}
