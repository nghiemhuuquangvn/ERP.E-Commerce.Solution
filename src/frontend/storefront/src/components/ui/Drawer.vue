<template>
  <Teleport to="body">
    <div v-if="open" class="fixed inset-0 z-modal flex bg-ink/45" :class="side === 'right' ? 'justify-end' : 'justify-start'" @mousedown.self="emit('close')">
      <div
        ref="panel"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="titleId"
        tabindex="-1"
        class="flex h-full w-80 max-w-[85vw] flex-col bg-surface shadow-lg outline-none"
        @keydown="onKeydown"
      >
        <div class="flex items-center justify-between border-b border-line px-4 py-3">
          <h2 :id="titleId" class="text-base font-semibold text-ink">{{ title }}</h2>
          <button type="button" class="rounded-md p-1.5 text-ink-muted hover:bg-surface-muted" :aria-label="closeLabel" @click="emit('close')">
            <X :size="20" :stroke-width="1.75" aria-hidden="true" />
          </button>
        </div>
        <div class="flex-1 overflow-y-auto p-4"><slot /></div>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import { X } from 'lucide-vue-next'

const props = withDefaults(defineProps<{ open: boolean; title: string; closeLabel: string; side?: 'left' | 'right' }>(), { side: 'left' })
const emit = defineEmits<{ close: [] }>()

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
const titleId = useId()
const panel = ref<HTMLElement | null>(null)
let opener: HTMLElement | null = null
let savedOverflow = ''

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    e.stopPropagation()
    emit('close')
    return
  }
  if (e.key !== 'Tab' || !panel.value) return
  const items = Array.from(panel.value.querySelectorAll<HTMLElement>(FOCUSABLE))
  if (items.length === 0) return e.preventDefault()
  const first = items[0]
  const last = items[items.length - 1]
  if (e.shiftKey && (document.activeElement === first || document.activeElement === panel.value)) {
    e.preventDefault()
    last.focus()
  } else if (!e.shiftKey && document.activeElement === last) {
    e.preventDefault()
    first.focus()
  }
}

function release() {
  document.body.style.overflow = savedOverflow
  opener?.focus()
  opener = null
}

watch(
  () => props.open,
  async (isOpen, wasOpen) => {
    if (isOpen) {
      opener = document.activeElement as HTMLElement | null
      savedOverflow = document.body.style.overflow
      document.body.style.overflow = 'hidden'
      await nextTick()
      ;(panel.value?.querySelector<HTMLElement>(FOCUSABLE) ?? panel.value)?.focus()
    } else if (wasOpen) {
      release()
    }
  },
)

onBeforeUnmount(() => {
  if (props.open) release()
})
</script>
