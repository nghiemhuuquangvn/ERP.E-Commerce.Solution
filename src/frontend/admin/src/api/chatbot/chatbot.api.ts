import { apiClient } from '../client'

const CHATBOT_BASE = '/v1/chatbot'

// Channel 2 = Admin. Guest-only fields (name/email) don't apply here — Admin
// requires authentication and the backend reads the JWT for identity.
export const ADMIN_CHATBOT_CHANNEL = 2 as const

export type ConversationProvider = 1 | 2
export type ConversationStatus = 1 | 2 | 3
export type MessageRole = 1 | 2

export interface ConversationDto {
  id: string
  channel: typeof ADMIN_CHATBOT_CHANNEL
  provider: ConversationProvider
  status: ConversationStatus
  startedAt: string
  closedAt: string | null
  escalatedCaseId: string | null
}

export interface ConversationMessageDto {
  id: string
  role: MessageRole
  content: string
  toolName: string | null
  createdAt: string
}

export interface ConversationHistoryDto {
  conversation: ConversationDto
  messages: ConversationMessageDto[]
}

export const chatbotApi = {
  startConversation: () =>
    apiClient
      .post<ConversationDto>(`${CHATBOT_BASE}/conversations`, { channel: ADMIN_CHATBOT_CHANNEL })
      .then((r) => r.data),

  listMyConversations: () =>
    apiClient
      .get<ConversationDto[]>(`${CHATBOT_BASE}/conversations`, { params: { channel: ADMIN_CHATBOT_CHANNEL } })
      .then((r) => r.data),

  getHistory: (conversationId: string) =>
    apiClient.get<ConversationHistoryDto>(`${CHATBOT_BASE}/conversations/${conversationId}`).then((r) => r.data),

  // Exchanges the caller's normal (large — full permission claim set) JWT for a short
  // single-use ticket, so the SignalR connection URL doesn't exceed Kestrel's/any
  // intermediate proxy's request-line size limit. See ERP.Shared.Authorization.HubTicketStore.
  getHubTicket: () =>
    apiClient.post<{ ticket: string }>(`${CHATBOT_BASE}/hub-ticket`).then((r) => r.data.ticket),
}
