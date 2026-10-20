import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '@/stores/auth.store'
import { chatbotApi } from '@/api/chatbot.api'

export interface ChatSignalRCallbacks {
  onTextDelta: (delta: string) => void
  onToolCallStarted: (toolName: string) => void
  onToolCallCompleted: (toolName: string) => void
  onCompleted: () => void
  onError: (message: string) => void
}

let connection: signalR.HubConnection | null = null

function buildConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${import.meta.env.VITE_CHATBOT_WS_URL ?? ''}/hubs/chat`, {
      // A short single-use ticket, not the raw JWT — this platform's tokens carry the
      // caller's full permission set (240+ claims for an admin-ish role, 10KB+), which
      // blows past Kestrel's default 8KB request-line limit when put on a WebSocket URL
      // (?access_token=...), failing the handshake with a 414 before any app code runs.
      // Minted fresh per call since SignalR invokes this on every (re)connect and a
      // ticket is single-use/short-lived. Guests have no JWT to exchange.
      accessTokenFactory: async () => {
        if (!useAuthStore().accessToken) return ''
        try {
          return await chatbotApi.getHubTicket()
        } catch {
          return ''
        }
      },
    })
    .withAutomaticReconnect()
    .build()
}

export async function connectChatHub(callbacks: ChatSignalRCallbacks): Promise<void> {
  if (connection) return

  const hub = buildConnection()
  hub.on('textDelta', callbacks.onTextDelta)
  hub.on('toolCallStarted', callbacks.onToolCallStarted)
  hub.on('toolCallCompleted', callbacks.onToolCallCompleted)
  hub.on('completed', callbacks.onCompleted)
  hub.on('error', callbacks.onError)

  await hub.start()
  connection = hub
}

export async function disconnectChatHub(): Promise<void> {
  if (!connection) return
  await connection.stop()
  connection = null
}

export async function joinConversation(conversationId: string): Promise<void> {
  await connection?.invoke('JoinConversation', conversationId)
}

export async function sendChatMessage(conversationId: string, content: string): Promise<void> {
  await connection?.invoke('SendMessage', conversationId, content)
}

export function isChatHubConnected(): boolean {
  return connection?.state === signalR.HubConnectionState.Connected
}
