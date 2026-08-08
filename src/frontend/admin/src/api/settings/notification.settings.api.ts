import { gatewayClient as apiClient } from '../client'

// NotificationService's SettingsController is mounted under the Gateway's `/ntf/api/...` route.
const SETTINGS_BASE = `/ntf/api/v1/settings/notification`

export interface SystemNotificationSettings {
  emailChannelAvailable: boolean
  smsChannelAvailable: boolean
  pushChannelAvailable: boolean
}

export type UpdateSystemNotificationSettingsRequest = SystemNotificationSettings

export interface TenantNotificationSettings {
  tenantId: string
  emailEnabledByDefault: boolean
  smsEnabledByDefault: boolean
  pushEnabledByDefault: boolean
}

export type UpdateTenantNotificationSettingsRequest = Omit<TenantNotificationSettings, 'tenantId'>

export const notificationSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemNotificationSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemNotificationSettingsRequest) =>
    apiClient.put<SystemNotificationSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient
      .get<TenantNotificationSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`)
      .then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantNotificationSettingsRequest) =>
    apiClient
      .put<TenantNotificationSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
