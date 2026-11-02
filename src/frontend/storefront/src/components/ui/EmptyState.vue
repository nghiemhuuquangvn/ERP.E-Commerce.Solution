<template>
  <div class="flex flex-col items-center gap-3 px-4 py-16 text-center">
    <div class="text-ink-muted"><slot name="icon"><PackageSearch :size="40" :stroke-width="1.75" aria-hidden="true" /></slot></div>
    <p class="text-base font-medium text-ink">{{ title }}</p>
    <p v-if="description" class="max-w-md text-sm text-ink-secondary">{{ description }}</p>
    <div v-if="$slots.default" class="mt-1"><slot /></div>
  </div>
</template>

<script setup lang="ts">
import { PackageSearch } from 'lucide-vue-next'

defineProps<{ title: string; description?: string }>()
</script>
