<template>
  <div :role="variant === 'danger' ? 'alert' : 'status'" :class="['flex items-start gap-3 rounded-md border px-3 py-2 text-sm', STYLE[variant]]">
    <component :is="ICON[variant]" :size="18" :stroke-width="1.75" aria-hidden="true" class="mt-0.5 shrink-0" />
    <div class="min-w-0 flex-1"><slot /></div>
  </div>
</template>

<script setup lang="ts">
import { AlertCircle, AlertTriangle, CheckCircle2, Info } from 'lucide-vue-next'

export type AlertVariant = 'info' | 'success' | 'warning' | 'danger'
withDefaults(defineProps<{ variant?: AlertVariant }>(), { variant: 'info' })

const STYLE: Record<AlertVariant, string> = {
  info: 'border-info/30 bg-info-soft text-info-soft-text',
  success: 'border-success/30 bg-success-soft text-success-soft-text',
  warning: 'border-warning/30 bg-warning-soft text-warning-soft-text',
  danger: 'border-danger/30 bg-danger-soft text-danger-soft-text',
}
const ICON = { info: Info, success: CheckCircle2, warning: AlertTriangle, danger: AlertCircle }
</script>
