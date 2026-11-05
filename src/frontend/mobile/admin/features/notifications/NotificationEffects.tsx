import { useEffect, useRef } from 'react'
import { useRouter } from 'expo-router'
import { useAuthStore } from '@/stores/auth.store'
import { configureForegroundAlerts, onAlertTapped } from './localAlert'
import { registerThisDevice } from './push'
import { notificationTarget } from './target'
import { useCanSeeNotifications } from './useUnreadCount'
import { useNewNotificationAlert } from './useNewNotificationAlert'

/** Mounted once while signed in: phone alerts for new messages, opening the record when one is tapped, and (when switched on) push registration. */
export function NotificationEffects() {
  const allowed = useCanSeeNotifications()
  return allowed ? <Active /> : null
}

function Active() {
  const router = useRouter()
  const routerRef = useRef(router)
  routerRef.current = router
  useNewNotificationAlert()

  // Registered once. A tap on a phone notification is handled once (the system can hand the same response over again when a listener is
  // attached) and goes to the record with `navigate`, which reuses a screen that is already open instead of stacking a copy of it.
  useEffect(() => {
    configureForegroundAlerts()
    void registerThisDevice()
    return onAlertTapped((data) => {
      const target = notificationTarget(data, (p) => useAuthStore.getState().hasPermission(p)) ?? '/notifications'
      routerRef.current.navigate(target as never)
    })
  }, [])

  return null
}
