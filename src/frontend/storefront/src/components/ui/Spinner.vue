<template>
  <Loader2 :size="size" :stroke-width="1.75" class="animate-spin" :aria-hidden="label ? undefined : true" :aria-label="label" :role="label ? 'img' : undefined" />
</template>

<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'

withDefaults(defineProps<{ size?: number; label?: string }>(), { size: 20 })
</script>
