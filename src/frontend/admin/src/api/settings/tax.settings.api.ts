import { gatewayClient as apiClient } from '../client'

// AccountingService's SettingsController is mounted under the Gateway's `/acc/api/...` route.
const SETTINGS_BASE = `/acc/api/v1/settings/tax`

export interface SystemTaxSettings {
  regionalTaxConfigAllowed: boolean
}

export type UpdateSystemTaxSettingsRequest = SystemTaxSettings

export interface TenantTaxSettings {
  tenantId: string
  companyTaxCode: string | null
  defaultTaxRatePercent: number
  pricesIncludeTax: boolean
}

export type UpdateTenantTaxSettingsRequest = Omit<TenantTaxSettings, 'tenantId'>

export const taxSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemTaxSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemTaxSettingsRequest) =>
    apiClient.put<SystemTaxSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantTaxSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantTaxSettingsRequest) =>
    apiClient.put<TenantTaxSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body).then((r) => r.data),
}
