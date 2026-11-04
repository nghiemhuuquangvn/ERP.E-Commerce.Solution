import { useQuery } from '@tanstack/react-query'
import { useAuthStore } from '@/stores/auth.store'
import { NOTIFICATIONS_PERMISSION, notificationsApi } from './api'

/** The unread number is refreshed this often while the app is in the foreground (react-query pauses it in the background). */
export const UNREAD_POLL_MS = 60_000

export const unreadKey = (userId?: string) => ['notifications', 'unread', userId] as const
export const inboxKey = (userId?: string) => ['notifications', 'inbox', userId] as const

/** Whether the signed-in user has an inbox at all (the token holds notifications:view). */
export function useCanSeeNotifications() {
  return useAuthStore((s) => s.permissions.includes(NOTIFICATIONS_PERMISSION))
}

/** Number of unread notifications; undefined while unknown or when the call fails (no badge is better than a wrong one). */
export function useUnreadCount() {
  const userId = useAuthStore((s) => s.user?.id)
  const allowed = useCanSeeNotifications()
  const q = useQuery({
    queryKey: unreadKey(userId),
    queryFn: () => notificationsApi.unreadCount(userId as string),
    enabled: allowed && !!userId,
    refetchInterval: UNREAD_POLL_MS,
    retry: false,
  })
  return q.isError ? undefined : q.data
}
