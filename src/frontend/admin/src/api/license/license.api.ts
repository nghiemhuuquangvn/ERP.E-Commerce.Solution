import axios from 'axios'
import { gatewayClient as apiClient } from '../client'

// IdentityService's ModuleDefinitions/LicensePlans/TenantLicenses controllers are mounted
// under the Gateway's `/id/api/license/...` route. Kept as a sibling of `api/settings/`
// (not inside it) because License is deliberately NOT a normal self-service Settings
// category: tenant admins may only VIEW their own tenant's license (read-only); only a
// caller holding `license:tenant:issue` (SuperAdmin) may issue/renew/suspend/revoke, and
// only via explicitly picking a tenant — never "my own tenant" self-service.
const LICENSE_BASE = `/id/api/v1/license`

export interface LicenseModule {
  moduleCode: string
  moduleName: string
  isActive: boolean
}

export interface UpsertLicenseModuleRequest {
  moduleName: string
  isActive: boolean
}

export interface LicensePlan {
  id: string
  planCode: string
  planName: string
  description?: string | null
  maxUsers?: number | null
  isActive: boolean
  moduleCodes: string[]
}

export interface UpsertLicensePlanRequest {
  planName: string
  description?: string
  maxUsers?: number
  isActive: boolean
  moduleCodes: string[]
}

export type TenantLicenseStatus = 'Active' | 'Suspended' | 'Revoked'

export interface TenantLicense {
  id: string
  tenantId: string
  planId?: string | null
  maxUsers?: number | null
  licenseKey: string
  issuedAt: string
  startsAt: string
  expiresAt?: string | null
  status: TenantLicenseStatus
  isCurrentlyValid: boolean
  issuedBy: string
  notes?: string | null
  moduleCodes: string[]
}

export interface IssueTenantLicenseRequest {
  planId?: string
  moduleCodes?: string[]
  maxUsers?: number
  startsAt?: string
  expiresAt?: string
  notes?: string
}

export interface RenewTenantLicenseRequest {
  expiresAt?: string
  maxUsers?: number
  moduleCodes?: string[]
  notes?: string
}

export const licenseApi = {
  getModules: () => apiClient.get<LicenseModule[]>(`${LICENSE_BASE}/modules`).then((r) => r.data),

  upsertModule: (moduleCode: string, body: UpsertLicenseModuleRequest) =>
    apiClient
      .put<LicenseModule>(`${LICENSE_BASE}/modules/${encodeURIComponent(moduleCode)}`, body)
      .then((r) => r.data),

  getPlans: () => apiClient.get<LicensePlan[]>(`${LICENSE_BASE}/plans`).then((r) => r.data),

  upsertPlan: (planCode: string, body: UpsertLicensePlanRequest) =>
    apiClient
      .put<LicensePlan>(`${LICENSE_BASE}/plans/${encodeURIComponent(planCode)}`, body)
      .then((r) => r.data),

  // Returns null when the tenant has no license issued yet (backend responds 404).
  getTenantLicense: (tenantId: string) =>
    apiClient
      .get<TenantLicense>(`${LICENSE_BASE}/tenant/${tenantId}`)
      .then((r) => r.data)
      .catch((err: unknown) => {
        if (axios.isAxiosError(err) && err.response?.status === 404) return null
        throw err
      }),

  issueTenantLicense: (tenantId: string, body: IssueTenantLicenseRequest) =>
    apiClient.post<TenantLicense>(`${LICENSE_BASE}/tenant/${tenantId}/issue`, body).then((r) => r.data),

  renewTenantLicense: (tenantId: string, body: RenewTenantLicenseRequest) =>
    apiClient.post<TenantLicense>(`${LICENSE_BASE}/tenant/${tenantId}/renew`, body).then((r) => r.data),

  suspendTenantLicense: (tenantId: string) =>
    apiClient.post<void>(`${LICENSE_BASE}/tenant/${tenantId}/suspend`).then((r) => r.data),

  revokeTenantLicense: (tenantId: string) =>
    apiClient.post<void>(`${LICENSE_BASE}/tenant/${tenantId}/revoke`).then((r) => r.data),
}
