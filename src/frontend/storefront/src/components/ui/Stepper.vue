<template>
  <ol class="flex items-center gap-4" :aria-label="label">
    <template v-for="(step, i) in steps" :key="i">
      <li class="flex flex-col items-center gap-1" :aria-current="current === i + 1 ? 'step' : undefined">
        <span
          :class="[
            'flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium',
            current > i + 1 ? 'bg-success text-on-brand' : current === i + 1 ? 'bg-brand-action text-on-brand' : 'bg-surface-muted text-ink-secondary',
          ]"
        >
          <Check v-if="current > i + 1" :size="16" :stroke-width="1.75" aria-hidden="true" />
          <span v-else>{{ i + 1 }}</span>
        </span>
        <span class="text-xs text-ink-secondary">{{ step }}</span>
      </li>
      <li v-if="i < steps.length - 1" class="flex-1 border-t border-line" aria-hidden="true" />
    </template>
  </ol>
</template>

<script setup lang="ts">
import { Check } from 'lucide-vue-next'

defineProps<{ steps: string[]; current: number; label: string }>()
</script>
