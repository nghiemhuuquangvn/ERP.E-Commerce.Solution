import { useEffect } from 'react'
import { refreshAccessToken } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'
import { markActivity, msUntilExpiry } from '@/lib/sessionActivity'

const ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'wheel', 'touchstart'] as const
const THROTTLE_MS = 15_000
const REFRESH_BEFORE_MS = 2 * 60_000

/**
 * Keeps a session alive while the PERSON works, even without API calls (typing a long form): every input is recorded,
 * and when the access token expires within two minutes it is refreshed in advance. Without input, nothing is
 * refreshed and the server ends the session after the tenant's idle timeout (BR-SES-01, Sessions.md).
 */
export function useSessionKeepAlive() {
  useEffect(() => {
    let lastHandled = 0
    markActivity() // opening a page counts as activity

    const onActivity = () => {
      const now = Date.now()
      markActivity(now)
      if (now - lastHandled < THROTTLE_MS) return
      lastHandled = now

      const { isAuthenticated, accessToken } = useAuthStore.getState()
      if (!isAuthenticated || !accessToken) return
      const left = msUntilExpiry(accessToken, now)
      if (left !== null && left < REFRESH_BEFORE_MS) refreshAccessToken().catch(() => undefined)
    }

    for (const event of ACTIVITY_EVENTS) window.addEventListener(event, onActivity, { passive: true })
    return () => {
      for (const event of ACTIVITY_EVENTS) window.removeEventListener(event, onActivity)
    }
  }, [])
}
