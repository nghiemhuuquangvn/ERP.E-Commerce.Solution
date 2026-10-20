import { apiClient } from './client'

export const ChatChannel = { Storefront: 1 } as const
export type ConversationStatus = 1 | 2 | 3 // 1=Active, 2=Escalated, 3=Closed
export type ChatProvider = 1 | 2 // 1=Claude, 2=ChatGpt
export type MessageRole = 1 | 2 // 1=User, 2=Assistant

export interface StartConversationRequest {
  channel: typeof ChatChannel.Storefront
  /** Not sent: the guest's tenant is the storefront host's (resolved server-side). */
  guestTenantId?: string
  guestSessionId?: string
}

export interface ConversationDto {
  id: string
  channel: number
  provider: ChatProvider
  status: ConversationStatus
  startedAt: string
  closedAt: string | null
  escalatedCaseId: string | null
}

export interface ChatMessageDto {
  id: string
  role: MessageRole
  content: string
  toolName: string | null
  createdAt: string
}

export interface ConversationHistoryDto {
  conversation: ConversationDto
  messages: ChatMessageDto[]
}

export const chatbotApi = {
  startConversation: (body: StartConversationRequest) =>
    apiClient.post<ConversationDto>('/v1/chatbot/conversations', body).then((r) => r.data),

  getHistory: (conversationId: string) =>
    apiClient
      .get<ConversationHistoryDto>(`/v1/chatbot/conversations/${conversationId}`)
      .then((r) => r.data),

  // Exchanges the caller's normal (large — full permission claim set) JWT for a short
  // single-use ticket, so the SignalR connection URL doesn't exceed Kestrel's/any
  // intermediate proxy's request-line size limit. See ERP.Shared.Authorization.HubTicketStore.
  getHubTicket: () =>
    apiClient.post<{ ticket: string }>('/v1/chatbot/hub-ticket').then((r) => r.data.ticket),
}
