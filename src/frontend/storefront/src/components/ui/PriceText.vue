<template>
  <span class="inline-flex flex-wrap items-baseline gap-x-2">
    <span v-if="free" class="font-semibold text-success-soft-text">{{ freeLabel }}</span>
    <span v-else :class="['font-bold tabular-nums text-ink', size === 'lg' ? 'text-3xl' : size === 'sm' ? 'text-sm' : 'text-base']">{{ format(amount) }}</span>
    <s v-if="hasDiscount" class="text-sm tabular-nums text-ink-muted">{{ format(oldAmount!) }}</s>
    <span v-if="hasDiscount" class="text-sm font-medium text-danger-soft-text">−{{ percentOff }}%</span>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { formatUsd } from '@/composables/useMoney'

const props = withDefaults(
  defineProps<{
    amount: number
    /** The price before the discount: shown struck through with the percentage off. */
    oldAmount?: number
    free?: boolean
    freeLabel?: string
    size?: 'sm' | 'md' | 'lg'
    format?: (n: number) => string
  }>(),
  { size: 'md', format: formatUsd, freeLabel: '' },
)

const hasDiscount = computed(() => !!props.oldAmount && props.oldAmount > props.amount)
const percentOff = computed(() => (props.oldAmount ? Math.round(((props.oldAmount - props.amount) / props.oldAmount) * 100) : 0))
</script>
