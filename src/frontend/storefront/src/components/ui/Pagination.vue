<template>
  <nav v-if="totalPages > 1" class="flex items-center justify-center gap-2" :aria-label="label">
    <Button variant="outline" :disabled="page <= 1" @click="emit('update:page', page - 1)">{{ prevLabel }}</Button>
    <span class="px-3 text-sm text-ink-secondary" aria-current="page">{{ pageLabel }}</span>
    <Button variant="outline" :disabled="page >= totalPages" @click="emit('update:page', page + 1)">{{ nextLabel }}</Button>
  </nav>
</template>

<script setup lang="ts">
import Button from './Button.vue'

defineProps<{ page: number; totalPages: number; label: string; prevLabel: string; nextLabel: string; pageLabel: string }>()
const emit = defineEmits<{ 'update:page': [page: number] }>()
</script>
