import { gatewayClient as apiClient } from '../client'

// PaymentService's SettingsController is mounted under the Gateway's `/pay/api/...` route.
const SETTINGS_BASE = `/pay/api/v1/settings/payment`

export interface SystemPaymentSettings {
  vnPayAvailable: boolean
  manualBankTransferAvailable: boolean
}

export type UpdateSystemPaymentSettingsRequest = SystemPaymentSettings

// hasVnPaySecretKey reports whether a key is stored — the encrypted value itself
// is never returned by the backend.
export interface TenantPaymentSettings {
  tenantId: string
  vnPayEnabled: boolean
  hasVnPaySecretKey: boolean
  sandboxMode: boolean
}

// vnPaySecretKey is plaintext, sent only when the admin wants to change it —
// leave empty/undefined to keep the currently stored key.
export interface UpdateTenantPaymentSettingsRequest {
  vnPayEnabled: boolean
  vnPaySecretKey?: string
  sandboxMode: boolean
}

export const paymentSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemPaymentSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemPaymentSettingsRequest) =>
    apiClient.put<SystemPaymentSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantPaymentSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantPaymentSettingsRequest) =>
    apiClient
      .put<TenantPaymentSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
