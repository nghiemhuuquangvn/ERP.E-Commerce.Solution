<template>
  <span :class="['inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium', STYLE[variant]]"><slot /></span>
</template>

<script setup lang="ts">
export type BadgeVariant = 'brand' | 'success' | 'warning' | 'danger' | 'neutral'
withDefaults(defineProps<{ variant?: BadgeVariant }>(), { variant: 'neutral' })

const STYLE: Record<BadgeVariant, string> = {
  brand: 'bg-brand-action text-on-brand',
  success: 'bg-success-soft text-success-soft-text',
  warning: 'bg-warning-soft text-warning-soft-text',
  danger: 'bg-danger-soft text-danger-soft-text',
  neutral: 'bg-surface-muted text-ink-secondary',
}
</script>
