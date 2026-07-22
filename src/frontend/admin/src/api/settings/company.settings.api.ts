import { apiClient } from '../client'

// IdentityService's CompanySettingsController is mounted under the Gateway's `/id/api/...` route.
// Company Settings has no System tier — a company profile is inherently per-tenant.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/v1/settings/company`

export interface TenantCompanySettings {
  tenantId: string
  companyName: string
  legalName: string | null
  taxCode: string | null
  address: string | null
  phone: string | null
  email: string | null
  logoUrl: string | null
  website: string | null
}

export type UpdateTenantCompanySettingsRequest = Omit<TenantCompanySettings, 'tenantId'>

export const companySettingsApi = {
  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantCompanySettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantCompanySettingsRequest) =>
    apiClient
      .put<TenantCompanySettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
