import { apiClient } from '../client'

// SharedMasterDataService's SettingsController is mounted under the Gateway's `/mst/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/mst/api/settings/currency`

export interface SystemCurrencySettings {
  multiCurrencyAllowed: boolean
}

export type UpdateSystemCurrencySettingsRequest = SystemCurrencySettings

export interface TenantCurrencySettings {
  tenantId: string
  defaultCurrencyCode: string
  exchangeRateSource: string
  decimalDigits: number
}

export type UpdateTenantCurrencySettingsRequest = Omit<TenantCurrencySettings, 'tenantId'>

export const currencySettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemCurrencySettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemCurrencySettingsRequest) =>
    apiClient.put<SystemCurrencySettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantCurrencySettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantCurrencySettingsRequest) =>
    apiClient
      .put<TenantCurrencySettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
