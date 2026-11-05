import { useEffect, useRef } from 'react'
import { useAuthStore } from '@/stores/auth.store'
import { notificationsApi } from './api'
import { ensureAlertPermission, showLocalAlert } from './localAlert'
import { useUnreadCount } from './useUnreadCount'

/** At most this many phone notifications for one growth of the count (a burst is not a flood). */
export const MAX_ALERTS_PER_UPDATE = 3

/**
 * Raises a phone notification when the unread count GROWS while the app is open (the count is polled every 60 s, so an alert is at most a
 * minute late). The first value after sign-in only sets the baseline, a count that falls or stays raises nothing, and a message is shown once.
 */
export function useNewNotificationAlert(): void {
  const userId = useAuthStore((s) => s.user?.id)
  const count = useUnreadCount()
  const previous = useRef<number | undefined>(undefined)
  const shown = useRef(new Set<string>())

  useEffect(() => {
    void ensureAlertPermission()
  }, [])

  useEffect(() => {
    if (count === undefined || !userId) return
    const before = previous.current
    previous.current = count
    if (before === undefined || count <= before) return

    let cancelled = false
    void (async () => {
      try {
        const page = await notificationsApi.history(userId, 1)
        const fresh = page.items.filter((n) => !n.isRead && !shown.current.has(n.id)).slice(0, Math.min(MAX_ALERTS_PER_UPDATE, count - before))
        for (const n of fresh) {
          if (cancelled) return
          shown.current.add(n.id)
          await showLocalAlert(n)
        }
      } catch {
        /* the bell already shows the new count */
      }
    })()
    return () => {
      cancelled = true
    }
  }, [count, userId])
}
