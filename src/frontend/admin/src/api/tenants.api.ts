import { gatewayClient as apiClient } from './client'

// IdentityService's TenantsController — list/get/create/update/activate/deactivate.
// List is also used as the read-only tenant selector on the Users/Roles pages
// (gated on identity:tenants:view there); full CRUD backs the dedicated Tenants
// admin page (identity:tenants:{view,create,edit,manage}).
const TENANTS_BASE = `/id/api/v1/tenants`

// TenantDto(Id, TenantCode, TenantName, Status) — Status: 1 = Active, 0 = Inactive
export interface Tenant {
  id: string
  tenantCode: string
  tenantName: string
  status: number
}

export interface CreateTenantRequest {
  tenantCode: string
  tenantName: string
}

export interface UpdateTenantRequest {
  tenantName: string
}

export interface MessageResponse {
  message: string
}

export const tenantsApi = {
  list: () => apiClient.get<Tenant[]>(TENANTS_BASE).then((r) => r.data),

  get: (id: string) => apiClient.get<Tenant>(`${TENANTS_BASE}/${id}`).then((r) => r.data),

  create: (body: CreateTenantRequest) =>
    apiClient.post<Tenant>(TENANTS_BASE, body).then((r) => r.data),

  update: (id: string, body: UpdateTenantRequest) =>
    apiClient.put<MessageResponse>(`${TENANTS_BASE}/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<MessageResponse>(`${TENANTS_BASE}/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<MessageResponse>(`${TENANTS_BASE}/${id}/deactivate`).then((r) => r.data),
}
