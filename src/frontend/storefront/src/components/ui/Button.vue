<template>
  <component
    :is="to ? RouterLink : 'button'"
    v-bind="to ? { to } : { type, disabled: disabled || loading }"
    :aria-busy="loading || undefined"
    :class="[
      'inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50',
      VARIANT[variant],
      SIZE[size],
      fullWidth && 'w-full',
    ]"
  >
    <Spinner v-if="loading" :size="16" />
    <slot />
  </component>
</template>

<script setup lang="ts">
import { RouterLink } from 'vue-router'
import Spinner from './Spinner.vue'

export type ButtonVariant = 'primary' | 'outline' | 'ghost' | 'link' | 'danger'

withDefaults(
  defineProps<{
    variant?: ButtonVariant
    size?: 'sm' | 'md' | 'lg'
    loading?: boolean
    disabled?: boolean
    fullWidth?: boolean
    type?: 'button' | 'submit'
    /** Renders a router link styled as a button. */
    to?: string
  }>(),
  { variant: 'primary', size: 'md', type: 'button' },
)

const VARIANT: Record<ButtonVariant, string> = {
  primary: 'bg-brand-action text-on-brand hover:bg-brand-action-hover',
  outline: 'border border-line-strong bg-surface text-ink hover:bg-surface-muted',
  ghost: 'text-ink-secondary hover:bg-surface-muted',
  link: 'text-brand-action underline-offset-2 hover:underline',
  danger: 'bg-danger text-on-brand hover:bg-danger-hover',
}
const SIZE = { sm: 'h-8 px-3 text-sm', md: 'h-10 px-4 text-sm', lg: 'h-12 px-6 text-base' }
</script>
