import * as signalR from '@microsoft/signalr'
import { jwtDecode } from 'jwt-decode'
import { useAuthStore } from '@/stores/auth.store'
import { useNotificationsStore } from '@/stores/notifications.store'
import { notificationsApi, type NotificationDto } from '@/api/notifications/notifications.api'
import { refreshAccessToken } from '@/api/client'

let connection: signalR.HubConnection | null = null

interface AccessTokenClaims {
  exp?: number
}

// A stored token can be stale by the time the bell actually tries to connect (e.g. it
// expired while this tab sat idle, or expired mid-flight between page load and this
// effect running). Reactively retrying after a 401 doesn't help here — SignalR's own
// negotiate call has no refresh-and-retry path of its own, unlike a normal axios call —
// so an expired token used to silently reach the hub and fail in a way nothing could
// recover from short of a full page reload. 10s skew matches typical clock-drift
// tolerance elsewhere in this app's JWT handling.
function isTokenExpiredOrExpiringSoon(token: string): boolean {
  try {
    const { exp } = jwtDecode<AccessTokenClaims>(token)
    if (!exp) return false
    return exp * 1000 < Date.now() + 10_000
  } catch {
    return true
  }
}

// NotificationService exposes this hub directly on its own port (VITE_NOTIFICATION_WS_URL),
// separate from the Gateway (VITE_API_URL) — Ocelot doesn't reliably proxy SignalR's
// WebSocket upgrade, so the hub bypasses the gateway on purpose (same as ChatBotService's hub).
function getConnection(): signalR.HubConnection {
  if (connection) return connection

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${import.meta.env.VITE_NOTIFICATION_WS_URL}/hubs/notifications`, {
      // A short single-use ticket, not the raw JWT — an admin's token carries 240+
      // permission claims (10KB+), which blows past Kestrel's default 8KB request-line
      // limit when put on a WebSocket URL (?access_token=...), failing the handshake
      // with a 414 before any app code runs. Minted fresh per call since SignalR invokes
      // this on every (re)connect and a ticket is single-use/short-lived.
      accessTokenFactory: async () => {
        let token = useAuthStore.getState().accessToken
        if (!token) return ''

        if (isTokenExpiredOrExpiringSoon(token)) {
          try {
            token = await refreshAccessToken()
          } catch {
            // Refresh failed (refresh token also expired/invalid) — refreshAccessToken()
            // already logged the user out; nothing left to try here.
            return ''
          }
        }

        try {
          return await notificationsApi.getHubTicket()
        } catch (err) {
          console.error('[notificationSignalR] Failed to obtain a hub ticket even with a confirmed-fresh token:', err)
          return ''
        }
      },
    })
    .withAutomaticReconnect()
    .build()

  connection.on('notificationReceived', (dto: NotificationDto) =>
    useNotificationsStore.getState().receiveNotification(dto),
  )

  connection.onreconnecting(() => useNotificationsStore.getState().setConnectionStatus('connecting'))
  // SignalR doesn't replay pushes missed while disconnected — reload from REST on every
  // (re)connect so the list/unread-count stay correct; the live push is additive on top.
  connection.onreconnected(() => {
    useNotificationsStore.getState().setConnectionStatus('connected')
    void useNotificationsStore.getState().loadInitial()
  })
  connection.onclose(() => useNotificationsStore.getState().setConnectionStatus('disconnected'))

  return connection
}

async function connect(): Promise<void> {
  const conn = getConnection()
  if (conn.state !== signalR.HubConnectionState.Disconnected) return
  useNotificationsStore.getState().setConnectionStatus('connecting')
  await conn.start()
  useNotificationsStore.getState().setConnectionStatus('connected')
}

export const notificationSignalR = {
  connect,

  async disconnect(): Promise<void> {
    if (!connection) return
    const conn = connection
    connection = null
    await conn.stop()
  },
}
