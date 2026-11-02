<template>
  <div class="fixed bottom-20 right-4 z-toast flex flex-col items-end md:bottom-5 md:right-5">
    <div
      v-if="isOpen"
      class="mb-3 flex h-[32rem] w-80 flex-col overflow-hidden rounded-lg border border-line bg-surface shadow-xl sm:w-96"
    >
      <div class="flex items-center justify-between bg-brand-action px-4 py-3 text-on-brand">
        <span class="text-sm font-semibold">{{ t('chat.title') }}</span>
        <button
          type="button"
          class="text-on-brand/80 hover:text-on-brand"
          :aria-label="t('chat.close')"
          @click="isOpen = false"
        ><X :size="20" :stroke-width="1.75" aria-hidden="true" /></button>
      </div>

      <div ref="messageListEl" class="flex-1 space-y-3 overflow-y-auto px-3 py-3">
        <div v-if="messages.length === 0 && !chatStore.error" class="text-center text-xs text-ink-muted">
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
              ? 'bg-brand-action text-on-brand'
              : 'bg-surface-muted text-ink'"
          >
            {{ message.content }}<span v-if="message.streaming" class="animate-pulse">▍</span>
          </div>
        </div>

        <div v-if="toolLabel" class="text-xs italic text-ink-muted">{{ toolLabel }}</div>

        <div v-if="chatStore.error" class="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger-soft-text">
          {{ chatStore.error }}
        </div>

        <div v-if="isClosed" class="flex flex-col items-center gap-2 py-2">
          <span class="text-xs text-ink-muted">{{ t('chat.conversationClosed') }}</span>
          <button
            type="button"
            class="rounded-md bg-brand-action px-3 py-1.5 text-xs font-medium text-on-brand hover:bg-brand-action-hover"
            @click="startNewConversation"
          >{{ t('chat.newConversation') }}</button>
        </div>
      </div>

      <form class="flex items-center gap-2 border-t border-line px-3 py-3" @submit.prevent="onSend">
        <input
          v-model="draft"
          type="text"
          :placeholder="t('chat.inputPlaceholder')"
          :disabled="isClosed || chatStore.connectionStatus === 'connecting'"
          class="flex-1 rounded-md border border-line px-3 py-2 text-sm focus:border-brand-action focus:outline-none disabled:bg-surface-muted"
        />
        <button
          type="submit"
          :disabled="!draft.trim() || isClosed || chatStore.connectionStatus === 'connecting'"
          class="rounded-md bg-brand-action px-3 py-2 text-sm font-medium text-on-brand hover:bg-brand-action-hover disabled:cursor-not-allowed disabled:opacity-50"
        >{{ t('chat.send') }}</button>
      </form>
    </div>

    <button
      type="button"
      class="flex h-14 w-14 items-center justify-center rounded-full bg-brand-action text-2xl text-on-brand shadow-lg hover:bg-brand-action-hover"
      :aria-label="t('chat.toggle')"
      @click="toggleOpen"
    >
      <X v-if="isOpen" :size="24" :stroke-width="1.75" aria-hidden="true" />
      <MessageCircle v-else :size="24" :stroke-width="1.75" aria-hidden="true" />
    </button>
  </div>
</template>

<script setup lang="ts">
import { MessageCircle, X } from 'lucide-vue-next'
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
