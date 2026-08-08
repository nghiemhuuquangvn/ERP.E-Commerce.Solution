import { gatewayClient as apiClient } from './client'

// IdentityService's TenantsController. List-only wrapper for the admin/Users and
// Roles pages' tenant selector — gated on identity:tenants:view, only ever shown
// to callers who can legitimately cross tenants (see UsersPage.tsx/RolesPermissionsPage.tsx).
const TENANTS_BASE = `/id/api/v1/tenants`

// TenantDto(Id, TenantCode, TenantName, Status)
export interface Tenant {
  id: string
  tenantCode: string
  tenantName: string
  status: number
}

export const tenantsApi = {
  list: () => apiClient.get<Tenant[]>(TENANTS_BASE).then((r) => r.data),
}
