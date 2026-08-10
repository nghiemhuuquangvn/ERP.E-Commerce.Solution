import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { i18n } from '@/i18n'
import { chatbotApi } from '@/api/chatbot.api'
import { useAuthStore } from '@/stores/auth.store'
import {
  connectChatHub,
  disconnectChatHub,
  joinConversation,
  sendChatMessage,
} from '@/services/chatSignalR'

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  streaming: boolean
}

const GUEST_SESSION_KEY = 'chatbot_guest_session_id'

const TOOL_LABEL_KEYS: Record<string, string> = {
  get_my_orders: 'orders',
  get_order_by_id: 'orders',
  search_products: 'products',
  get_stock_availability: 'products',
  get_shipment_by_order: 'shipping',
  get_active_promotions: 'promotions',
  escalate_to_human: 'escalate',
}

function loadGuestSessionId(): string {
  const existing = localStorage.getItem(GUEST_SESSION_KEY)
  if (existing) return existing
  const generated = crypto.randomUUID()
  localStorage.setItem(GUEST_SESSION_KEY, generated)
  return generated
}

export const useChatStore = defineStore('chat', () => {
  const conversationId = ref<string | null>(null)
  const conversationStatus = ref<1 | 2 | 3 | null>(null)
  const messages = ref<ChatMessage[]>([])
  const connectionStatus = ref<'disconnected' | 'connecting' | 'connected'>('disconnected')
  const guestSessionId = ref(loadGuestSessionId())
  const activeTool = ref<string | null>(null)
  const error = ref<string | null>(null)

  const isConnected = computed(() => connectionStatus.value === 'connected')
  const activeToolLabelKey = computed(() =>
    activeTool.value ? (TOOL_LABEL_KEYS[activeTool.value] ?? 'generic') : null,
  )

  function appendAssistantDelta(delta: string) {
    const last = messages.value[messages.value.length - 1]
    if (last && last.role === 'assistant' && last.streaming) {
      last.content += delta
    } else {
      messages.value.push({ id: crypto.randomUUID(), role: 'assistant', content: delta, streaming: true })
    }
  }

  function finishStreaming() {
    const last = messages.value[messages.value.length - 1]
    if (last && last.role === 'assistant') last.streaming = false
    activeTool.value = null
  }

  async function startOrResumeConversation() {
    if (conversationId.value && connectionStatus.value !== 'disconnected') return

    error.value = null
    connectionStatus.value = 'connecting'
    try {
      const authStore = useAuthStore()
      const conversation = authStore.isAuthenticated
        ? await chatbotApi.startConversation({ channel: 1 })
        : await chatbotApi.startConversation({
            channel: 1,
            guestTenantId: import.meta.env.VITE_TENANT_ID,
            guestSessionId: guestSessionId.value,
          })

      conversationId.value = conversation.id
      conversationStatus.value = conversation.status

      await connectChatHub({
        onTextDelta: appendAssistantDelta,
        onToolCallStarted: (toolName) => {
          activeTool.value = toolName
        },
        onToolCallCompleted: (toolName) => {
          if (activeTool.value === toolName) activeTool.value = null
        },
        onCompleted: finishStreaming,
        onError: (message) => {
          error.value = message
          finishStreaming()
        },
      })
      await joinConversation(conversation.id)
      connectionStatus.value = 'connected'
    } catch {
      connectionStatus.value = 'disconnected'
      error.value = i18n.global.t('chat.errors.connectFailed')
    }
  }

  async function sendMessage(text: string) {
    const trimmed = text.trim()
    if (!trimmed || !conversationId.value) return

    messages.value.push({ id: crypto.randomUUID(), role: 'user', content: trimmed, streaming: false })
    error.value = null
    try {
      await sendChatMessage(conversationId.value, trimmed)
    } catch {
      error.value = i18n.global.t('chat.errors.sendFailed')
    }
  }

  async function closeChat() {
    await disconnectChatHub()
    connectionStatus.value = 'disconnected'
  }

  function resetConversation() {
    conversationId.value = null
    conversationStatus.value = null
    messages.value = []
    activeTool.value = null
    error.value = null
  }

  return {
    conversationId,
    conversationStatus,
    messages,
    connectionStatus,
    guestSessionId,
    activeTool,
    activeToolLabelKey,
    error,
    isConnected,
    startOrResumeConversation,
    sendMessage,
    closeChat,
    resetConversation,
  }
})
