import * as signalR from '@microsoft/signalr'
import { jwtDecode } from 'jwt-decode'
import { useAuthStore } from '@/stores/auth.store'
import { useChatbotStore } from '@/stores/chatbot.store'
import { chatbotApi } from '@/api/chatbot/chatbot.api'
import { refreshAccessToken } from '@/api/client'

interface AccessTokenClaims {
  exp?: number
}

// See notificationSignalR.ts's matching helper for the full rationale — a stored token
// can be stale by the time a hub connection is actually attempted, and unlike a normal
// axios call, SignalR's own negotiate request has no reactive refresh-and-retry path of
// its own, so an expired token used to silently reach the hub and fail unrecoverably.
function isTokenExpiredOrExpiringSoon(token: string): boolean {
  try {
    const { exp } = jwtDecode<AccessTokenClaims>(token)
    if (!exp) return false
    return exp * 1000 < Date.now() + 10_000
  } catch {
    return true
  }
}

const TOOL_LABEL_KEYS: Record<string, string> = {
  get_my_orders: 'chatbot.tools.orders',
  get_order_by_id: 'chatbot.tools.orders',
  search_products: 'chatbot.tools.products',
  get_stock_availability: 'chatbot.tools.products',
  get_shipment_by_order: 'chatbot.tools.shipment',
  get_active_promotions: 'chatbot.tools.promotions',
  escalate_to_human: 'chatbot.tools.escalate',
}

let connection: signalR.HubConnection | null = null

// ChatBotService exposes this hub directly on its own port (VITE_CHATBOT_WS_URL),
// separate from the Gateway (VITE_API_URL) — Ocelot doesn't reliably proxy
// SignalR's WebSocket upgrade, so the hub bypasses the gateway on purpose.
function getConnection(): signalR.HubConnection {
  if (connection) return connection

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${import.meta.env.VITE_CHATBOT_WS_URL ?? ''}/hubs/chat`, {
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
            return ''
          }
        }

        try {
          return await chatbotApi.getHubTicket()
        } catch (err) {
          console.error('[chatSignalR] Failed to obtain a hub ticket even with a confirmed-fresh token:', err)
          return ''
        }
      },
    })
    .withAutomaticReconnect()
    .build()

  connection.on('textDelta', (delta: string) => useChatbotStore.getState().appendDelta(delta))

  connection.on('toolCallStarted', (toolName: string) =>
    useChatbotStore.getState().setToolIndicator({
      labelKey: TOOL_LABEL_KEYS[toolName] ?? 'chatbot.tools.generic',
      toolName,
    }),
  )

  connection.on('toolCallCompleted', () => useChatbotStore.getState().setToolIndicator(null))

  connection.on('completed', () => useChatbotStore.getState().finalizeTurn())

  connection.on('error', (message: string) => useChatbotStore.getState().setError(message))

  connection.onreconnecting(() => useChatbotStore.getState().setConnectionStatus('connecting'))
  connection.onreconnected(() => useChatbotStore.getState().setConnectionStatus('connected'))
  connection.onclose(() => useChatbotStore.getState().setConnectionStatus('disconnected'))

  return connection
}

async function connect(): Promise<void> {
  const conn = getConnection()
  if (conn.state !== signalR.HubConnectionState.Disconnected) return
  useChatbotStore.getState().setConnectionStatus('connecting')
  await conn.start()
  useChatbotStore.getState().setConnectionStatus('connected')
}

export const chatSignalR = {
  connect,

  async joinConversation(conversationId: string): Promise<void> {
    await connect()
    await getConnection().invoke('JoinConversation', conversationId)
  },

  async sendMessage(conversationId: string, content: string): Promise<void> {
    await connect()
    await getConnection().invoke('SendMessage', conversationId, content)
  },

  async disconnect(): Promise<void> {
    if (!connection) return
    const conn = connection
    connection = null
    await conn.stop()
  },
}
