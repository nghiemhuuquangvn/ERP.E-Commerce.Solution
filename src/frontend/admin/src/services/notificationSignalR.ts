import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '@/stores/auth.store'
import { useNotificationsStore } from '@/stores/notifications.store'
import { notificationsApi, type NotificationDto } from '@/api/notifications/notifications.api'

let connection: signalR.HubConnection | null = null

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
        if (!useAuthStore.getState().accessToken) return ''
        try {
          return await notificationsApi.getHubTicket()
        } catch {
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
