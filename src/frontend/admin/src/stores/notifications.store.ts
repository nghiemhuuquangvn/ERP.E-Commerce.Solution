import { create } from 'zustand'
import { useAuthStore } from '@/stores/auth.store'
import { notificationsApi, type NotificationDto } from '@/api/notifications/notifications.api'

export type NotificationConnectionStatus = 'disconnected' | 'connecting' | 'connected'

interface NotificationsState {
  items: NotificationDto[]
  unreadCount: number
  connectionStatus: NotificationConnectionStatus
  isLoading: boolean

  // REST (history + unread-count) is the source of truth on mount and on every
  // (re)connect — the live SignalR push is additive on top, not authoritative on its
  // own, since a push can be missed while the tab was closed/disconnected.
  loadInitial: () => Promise<void>
  receiveNotification: (dto: NotificationDto) => void
  markRead: (id: string) => Promise<void>
  setConnectionStatus: (status: NotificationConnectionStatus) => void
}

export const useNotificationsStore = create<NotificationsState>()((set, get) => ({
  items: [],
  unreadCount: 0,
  connectionStatus: 'disconnected',
  isLoading: false,

  loadInitial: async () => {
    const userId = useAuthStore.getState().user?.id
    if (!userId) return

    set({ isLoading: true })
    try {
      const [history, unreadCount] = await Promise.all([
        notificationsApi.getHistory(userId),
        notificationsApi.getUnreadCount(userId),
      ])
      set({ items: history.items, unreadCount, isLoading: false })
    } catch {
      set({ isLoading: false })
    }
  },

  receiveNotification: (dto) =>
    set((s) => ({
      items: [dto, ...s.items],
      unreadCount: s.unreadCount + (dto.isRead ? 0 : 1),
    })),

  markRead: async (id) => {
    const target = get().items.find((n) => n.id === id)
    if (!target || target.isRead) return

    // Optimistic — the dropdown should feel instant; a failed PATCH just leaves the
    // item read locally until the next loadInitial() reconciles it.
    set((s) => ({
      items: s.items.map((n) => (n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n)),
      unreadCount: Math.max(0, s.unreadCount - 1),
    }))

    try {
      await notificationsApi.markAsRead(id)
    } catch {
      // Leave the optimistic update in place — a background loadInitial() will
      // correct it if the PATCH genuinely failed server-side.
    }
  },

  setConnectionStatus: (status) => set({ connectionStatus: status }),
}))
