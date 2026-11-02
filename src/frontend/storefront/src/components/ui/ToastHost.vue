<template>
  <div class="pointer-events-none fixed inset-x-0 bottom-20 z-toast flex flex-col items-center gap-2 px-4 sm:bottom-6 sm:items-start sm:pl-6" aria-live="polite">
    <div
      v-for="toast in toasts"
      :key="toast.id"
      :role="toast.kind === 'error' ? 'alert' : 'status'"
      :class="['pointer-events-auto flex max-w-sm items-start gap-2 rounded-md border px-3 py-2 text-sm shadow-md', STYLE[toast.kind]]"
    >
      <component :is="ICON[toast.kind]" :size="18" :stroke-width="1.75" aria-hidden="true" class="mt-0.5 shrink-0" />
      <span class="flex-1">{{ toast.message }}</span>
      <button type="button" class="rounded p-0.5 hover:bg-surface-muted" :aria-label="t('common.close')" @click="dismiss(toast.id)">
        <X :size="16" :stroke-width="1.75" aria-hidden="true" />
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { AlertCircle, CheckCircle2, Info, X } from 'lucide-vue-next'
import { useI18n } from 'vue-i18n'
import { useToast } from '@/composables/useToast'

const { t } = useI18n()
const { toasts, dismiss } = useToast()

const STYLE = {
  success: 'border-success/30 bg-success-soft text-success-soft-text',
  error: 'border-danger/30 bg-danger-soft text-danger-soft-text',
  info: 'border-info/30 bg-info-soft text-info-soft-text',
}
const ICON = { success: CheckCircle2, error: AlertCircle, info: Info }
</script>
