import { create } from 'zustand'
import { chatbotApi, type ConversationDto } from '@/api/chatbot/chatbot.api'
import { chatSignalR } from '@/services/chatSignalR'

export type ChatRole = 'user' | 'assistant'

export interface ChatMessage {
  id: string
  role: ChatRole
  content: string
  toolName?: string | null
  createdAt: string
  // True only for the assistant bubble currently receiving textDelta events.
  streaming?: boolean
}

export interface ToolIndicator {
  labelKey: string
  toolName: string
}

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected'

function localMessageId(prefix: string) {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2)}`
}

interface ChatbotState {
  conversation: ConversationDto | null
  messages: ChatMessage[]
  connectionStatus: ConnectionStatus
  toolIndicator: ToolIndicator | null
  error: string | null
  isSending: boolean
  isLoadingHistory: boolean

  startConversation: () => Promise<void>
  openConversation: (conversationId: string) => Promise<void>
  sendMessage: (content: string) => Promise<void>

  // Called by services/chatSignalR.ts hub event handlers — not meant to be
  // invoked directly from page components.
  appendDelta: (delta: string) => void
  setToolIndicator: (indicator: ToolIndicator | null) => void
  finalizeTurn: () => void
  setError: (message: string | null) => void
  setConnectionStatus: (status: ConnectionStatus) => void
}

export const useChatbotStore = create<ChatbotState>()((set, get) => ({
  conversation: null,
  messages: [],
  connectionStatus: 'disconnected',
  toolIndicator: null,
  error: null,
  isSending: false,
  isLoadingHistory: false,

  startConversation: async () => {
    set({ error: null, isLoadingHistory: true })
    try {
      const conversation = await chatbotApi.startConversation()
      set({ conversation, messages: [], isLoadingHistory: false })
      await chatSignalR.joinConversation(conversation.id)
    } catch (e) {
      set({ isLoadingHistory: false, error: 'chatbot.errors.startFailed' })
      throw e
    }
  },

  openConversation: async (conversationId) => {
    set({ error: null, isLoadingHistory: true })
    try {
      const history = await chatbotApi.getHistory(conversationId)
      const messages: ChatMessage[] = history.messages.map((m) => ({
        id: m.id,
        role: m.role === 1 ? 'user' : 'assistant',
        content: m.content,
        toolName: m.toolName,
        createdAt: m.createdAt,
      }))
      set({ conversation: history.conversation, messages, isLoadingHistory: false })
      await chatSignalR.joinConversation(history.conversation.id)
    } catch (e) {
      set({ isLoadingHistory: false, error: 'chatbot.errors.loadFailed' })
      throw e
    }
  },

  sendMessage: async (content) => {
    const trimmed = content.trim()
    const { conversation } = get()
    if (!conversation || !trimmed) return

    const userMessage: ChatMessage = {
      id: localMessageId('user'),
      role: 'user',
      content: trimmed,
      createdAt: new Date().toISOString(),
    }
    // Optimistic append — the hub doesn't echo the user's own message back.
    set((s) => ({ messages: [...s.messages, userMessage], isSending: true, error: null }))

    try {
      await chatSignalR.sendMessage(conversation.id, trimmed)
    } catch (e) {
      set({ isSending: false, error: 'chatbot.errors.sendFailed' })
    }
  },

  appendDelta: (delta) =>
    set((s) => {
      const messages = [...s.messages]
      const last = messages[messages.length - 1]
      if (last && last.role === 'assistant' && last.streaming) {
        messages[messages.length - 1] = { ...last, content: last.content + delta }
      } else {
        messages.push({
          id: localMessageId('assistant'),
          role: 'assistant',
          content: delta,
          createdAt: new Date().toISOString(),
          streaming: true,
        })
      }
      return { messages, isSending: true }
    }),

  setToolIndicator: (indicator) => set({ toolIndicator: indicator }),

  finalizeTurn: () =>
    set((s) => ({
      messages: s.messages.map((m) => (m.streaming ? { ...m, streaming: false } : m)),
      isSending: false,
      toolIndicator: null,
    })),

  setError: (message) => set({ error: message, isSending: false, toolIndicator: null }),

  setConnectionStatus: (status) => set({ connectionStatus: status }),
}))
