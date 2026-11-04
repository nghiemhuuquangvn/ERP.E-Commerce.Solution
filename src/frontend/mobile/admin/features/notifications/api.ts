import { gatewayClient } from '@/api/client'
import type { Page } from '@/modules/usePagedList'

// NotificationService is mounted at the Gateway's /ntf prefix (same paths as the Admin web: admin/src/api/notifications/notifications.api.ts).
const BASE = '/ntf/api/v1/notifications'

/** The permission every inbox endpoint needs; without it the bell is not shown. */
export const NOTIFICATIONS_PERMISSION = 'notifications:view'

export interface AppNotification {
  id: string
  subject: string
  body: string
  channel?: string | number
  createdAt: string
  isRead: boolean
}

export const NOTIFICATIONS_PAGE_SIZE = 20

/** E-mail bodies can be HTML: the inbox shows text only. */
export function plainText(value?: string | null): string {
  return (value ?? '').replace(/<[^>]*>/g, ' ').replace(/&nbsp;/g, ' ').replace(/\s+/g, ' ').trim()
}

export const notificationsApi = {
  /** Newest first; the server answers a page object, an array is accepted too. */
  async history(userId: string, page: number): Promise<Page<AppNotification>> {
    const skip = (page - 1) * NOTIFICATIONS_PAGE_SIZE
    const { data } = await gatewayClient.get<{ items?: AppNotification[]; total?: number } | AppNotification[]>(
      `${BASE}/history/${encodeURIComponent(userId)}`,
      { params: { skip, take: NOTIFICATIONS_PAGE_SIZE } },
    )
    const items = (Array.isArray(data) ? data : data.items ?? []).map((n) => ({ ...n, subject: plainText(n.subject), body: plainText(n.body) }))
    const total = Array.isArray(data) ? skip + items.length + (items.length === NOTIFICATIONS_PAGE_SIZE ? 1 : 0) : data.total ?? items.length
    return { items, total }
  },

  async unreadCount(userId: string): Promise<number> {
    const { data } = await gatewayClient.get<{ unreadCount?: number } | number>(`${BASE}/unread-count/${encodeURIComponent(userId)}`)
    return typeof data === 'number' ? data : data.unreadCount ?? 0
  },

  async markRead(id: string): Promise<void> {
    await gatewayClient.patch(`${BASE}/${id}/read`)
  },
}
