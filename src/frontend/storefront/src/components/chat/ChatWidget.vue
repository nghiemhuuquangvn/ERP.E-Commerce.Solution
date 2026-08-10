<template>
  <div class="fixed bottom-5 right-5 z-50 flex flex-col items-end">
    <div
      v-if="isOpen"
      class="mb-3 flex h-[32rem] w-80 flex-col overflow-hidden rounded-lg border border-gray-200 bg-white shadow-xl sm:w-96"
    >
      <div class="flex items-center justify-between bg-brand-500 px-4 py-3 text-white">
        <span class="text-sm font-semibold">{{ t('chat.title') }}</span>
        <button
          type="button"
          class="text-white/80 hover:text-white"
          :aria-label="t('chat.close')"
          @click="isOpen = false"
        >✕</button>
      </div>

      <div ref="messageListEl" class="flex-1 space-y-3 overflow-y-auto px-3 py-3">
        <div v-if="messages.length === 0 && !chatStore.error" class="text-center text-xs text-gray-400">
          {{ t('chat.greeting') }}
        </div>

        <div
          v-for="message in messages"
          :key="message.id"
          class="flex"
          :class="message.role === 'user' ? 'justify-end' : 'justify-start'"
        >
          <div
            class="max-w-[85%] whitespace-pre-wrap rounded-lg px-3 py-2 text-sm"
            :class="message.role === 'user'
              ? 'bg-brand-500 text-white'
              : 'bg-gray-100 text-gray-800'"
          >
            {{ message.content }}<span v-if="message.streaming" class="animate-pulse">▍</span>
          </div>
        </div>

        <div v-if="toolLabel" class="text-xs italic text-gray-400">{{ toolLabel }}</div>

        <div v-if="chatStore.error" class="rounded-md bg-red-50 px-3 py-2 text-xs text-red-600">
          {{ chatStore.error }}
        </div>

        <div v-if="isClosed" class="flex flex-col items-center gap-2 py-2">
          <span class="text-xs text-gray-400">{{ t('chat.conversationClosed') }}</span>
          <button
            type="button"
            class="rounded-md bg-brand-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-600"
            @click="startNewConversation"
          >{{ t('chat.newConversation') }}</button>
        </div>
      </div>

      <form class="flex items-center gap-2 border-t border-gray-200 px-3 py-3" @submit.prevent="onSend">
        <input
          v-model="draft"
          type="text"
          :placeholder="t('chat.inputPlaceholder')"
          :disabled="isClosed || chatStore.connectionStatus === 'connecting'"
          class="flex-1 rounded-md border border-gray-200 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none disabled:bg-gray-50"
        />
        <button
          type="submit"
          :disabled="!draft.trim() || isClosed || chatStore.connectionStatus === 'connecting'"
          class="rounded-md bg-brand-500 px-3 py-2 text-sm font-medium text-white hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-50"
        >{{ t('chat.send') }}</button>
      </form>
    </div>

    <button
      type="button"
      class="flex h-14 w-14 items-center justify-center rounded-full bg-brand-500 text-2xl text-white shadow-lg hover:bg-brand-600"
      :aria-label="t('chat.toggle')"
      @click="toggleOpen"
    >{{ isOpen ? '✕' : '💬' }}</button>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useChatStore } from '@/stores/chat.store'

const { t } = useI18n()
const chatStore = useChatStore()

const isOpen = ref(false)
const draft = ref('')
const messageListEl = ref<HTMLElement | null>(null)

const messages = computed(() => chatStore.messages)
const isClosed = computed(
  () => chatStore.conversationStatus !== null && chatStore.conversationStatus !== 1,
)
const toolLabel = computed(() =>
  chatStore.activeToolLabelKey ? t(`chat.tool.${chatStore.activeToolLabelKey}`) : null,
)

watch(
  () => [messages.value.length, messages.value[messages.value.length - 1]?.content],
  async () => {
    await nextTick()
    messageListEl.value?.scrollTo({ top: messageListEl.value.scrollHeight })
  },
)

async function toggleOpen() {
  isOpen.value = !isOpen.value
  if (isOpen.value) await chatStore.startOrResumeConversation()
}

async function onSend() {
  const text = draft.value
  draft.value = ''
  await chatStore.sendMessage(text)
}

async function startNewConversation() {
  await chatStore.closeChat()
  chatStore.resetConversation()
  await chatStore.startOrResumeConversation()
}
</script>
