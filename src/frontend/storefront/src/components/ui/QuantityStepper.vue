<template>
  <div class="inline-flex items-center rounded-md border border-line-strong" role="group" :aria-label="label">
    <button type="button" class="flex h-9 w-9 items-center justify-center text-ink-secondary hover:bg-surface-muted disabled:opacity-40" :aria-label="decreaseLabel" :disabled="modelValue <= min" @click="set(modelValue - 1)">
      <Minus :size="16" :stroke-width="1.75" aria-hidden="true" />
    </button>
    <span class="min-w-8 px-2 text-center text-sm font-medium tabular-nums" role="status" aria-live="polite">{{ modelValue }}</span>
    <button type="button" class="flex h-9 w-9 items-center justify-center text-ink-secondary hover:bg-surface-muted disabled:opacity-40" :aria-label="increaseLabel" :disabled="max !== undefined && modelValue >= max" @click="set(modelValue + 1)">
      <Plus :size="16" :stroke-width="1.75" aria-hidden="true" />
    </button>
  </div>
</template>

<script setup lang="ts">
import { Minus, Plus } from 'lucide-vue-next'

const props = withDefaults(defineProps<{ modelValue: number; min?: number; max?: number; label: string; decreaseLabel: string; increaseLabel: string }>(), { min: 1 })
const emit = defineEmits<{ 'update:modelValue': [value: number] }>()

function set(n: number) {
  const clamped = Math.max(props.min, props.max !== undefined ? Math.min(props.max, n) : n)
  if (clamped !== props.modelValue) emit('update:modelValue', clamped)
}
</script>
