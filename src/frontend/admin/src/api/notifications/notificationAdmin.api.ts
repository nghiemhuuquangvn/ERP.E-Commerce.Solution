import { gatewayClient as apiClient } from '../client'

// NotificationService, through the Gateway's `/ntf/api/...` route (ROADMAP P1-6): the tenant's notification history and the templates.
const BASE = '/ntf/api/v1'

export interface SentNotification {
  id: string
  recipientId: string
  recipientEmail: string
  channel: string
  subject: string
  body: string
  status: string
  priority: string
  retryCount: number
  errorMessage: string | null
  sentAt: string | null
  createdAt: string
  logs: { eventType: string; details: string; eventTimestamp: string }[]
}

export interface SentNotificationPage {
  total: number
  items: SentNotification[]
}

export interface NotificationTemplate {
  id: string
  code: string
  name: string
  channel: string
  subject: string
  body: string
  variables: string | null
  isActive: boolean
  isPlatformDefault: boolean
}

/** Channels as the API takes them (numeric enum). */
export const TEMPLATE_CHANNELS = { Email: 0, Sms: 1, Push: 2, InApp: 3 } as const

export interface TemplateInput {
  name: string
  subject: string
  body: string
  variables?: string | null
}

export interface TemplatePreview {
  subject: string
  body: string
  /** {placeholders} in the text that no declared variable covers. */
  unknownPlaceholders: string[]
}

export interface NotificationHistoryParams {
  status?: string
  channel?: string
  search?: string
  skip?: number
  take?: number
}

export const notificationAdminApi = {
  history: (params: NotificationHistoryParams) =>
    apiClient.get<SentNotificationPage>(`${BASE}/notification-history`, { params }).then((r) => r.data),

  templates: () => apiClient.get<NotificationTemplate[]>(`${BASE}/templates`).then((r) => r.data),

  createTemplate: (body: TemplateInput & { code: string; channel: number }) =>
    apiClient.post<NotificationTemplate>(`${BASE}/templates`, body).then((r) => r.data),
  updateTemplate: (id: string, body: TemplateInput) => apiClient.put<NotificationTemplate>(`${BASE}/templates/${id}`, body).then((r) => r.data),
  setTemplateActive: (id: string, active: boolean) =>
    apiClient.post<NotificationTemplate>(`${BASE}/templates/${id}/${active ? 'activate' : 'deactivate'}`).then((r) => r.data),
  /** A shop copies a platform default into a template of its own. */
  overrideTemplate: (id: string) => apiClient.post<NotificationTemplate>(`${BASE}/templates/${id}/override`).then((r) => r.data),
  /** Deletes the shop's own template (the platform default is used again). */
  deleteTemplate: (id: string) => apiClient.delete(`${BASE}/templates/${id}`).then(() => undefined),
  preview: (body: { subject: string; body: string; variables: Record<string, string> }) =>
    apiClient.post<TemplatePreview>(`${BASE}/templates/preview`, body).then((r) => r.data),
}
