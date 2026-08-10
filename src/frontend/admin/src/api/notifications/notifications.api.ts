import { gatewayClient as apiClient } from '../client'

// NotificationService's NotificationsController is mounted under the Gateway's
// `/ntf/api/...` route.
const NOTIFICATIONS_BASE = '/ntf/api/v1/notifications'

export interface NotificationDto {
  id: string
  recipientId: string
  recipientEmail: string
  channel: string
  subject: string
  body: string
  status: string
  priority: string
  createdAt: string
  isRead: boolean
  readAt: string | null
}

export interface NotificationHistoryResponse {
  total: number
  items: NotificationDto[]
}

export const notificationsApi = {
  getHistory: (recipientId: string, skip = 0, take = 20) =>
    apiClient
      .get<NotificationHistoryResponse>(`${NOTIFICATIONS_BASE}/history/${recipientId}`, {
        params: { skip, take },
      })
      .then((r) => r.data),

  getUnreadCount: (recipientId: string) =>
    apiClient
      .get<{ unreadCount: number }>(`${NOTIFICATIONS_BASE}/unread-count/${recipientId}`)
      .then((r) => r.data.unreadCount),

  markAsRead: (id: string) => apiClient.patch(`${NOTIFICATIONS_BASE}/${id}/read`).then(() => undefined),

  // Exchanges the caller's normal (large — full permission claim set) JWT for a short
  // single-use ticket, so the SignalR connection URL doesn't exceed Kestrel's/any
  // intermediate proxy's request-line size limit. See ERP.Shared.Authorization.HubTicketStore.
  getHubTicket: () =>
    apiClient.post<{ ticket: string }>(`${NOTIFICATIONS_BASE}/hub-ticket`).then((r) => r.data.ticket),
}
