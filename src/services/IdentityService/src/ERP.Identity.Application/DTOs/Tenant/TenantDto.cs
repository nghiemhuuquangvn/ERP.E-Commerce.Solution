namespace ERP.Identity.Application.DTOs.Tenant;

public record TenantDto(Guid Id, string TenantCode, string TenantName, byte Status);

public record CreateTenantRequest(string TenantCode, string TenantName);

public record UpdateTenantRequest(string TenantName);
