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
}
