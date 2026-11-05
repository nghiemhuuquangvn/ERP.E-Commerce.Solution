import { useEffect } from 'react'
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
  useNewNotificationAlert()

  useEffect(() => {
    configureForegroundAlerts()
    void registerThisDevice()
    return onAlertTapped((data) => {
      const target = notificationTarget(data, (p) => useAuthStore.getState().hasPermission(p))
      if (target) router.push(target as never)
      else router.push('/notifications' as never)
    })
  }, [router])

  return null
}
