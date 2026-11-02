<template>
  <label class="flex min-h-6 w-fit cursor-pointer items-center gap-2 text-sm text-ink" :class="{ 'cursor-not-allowed opacity-50': $attrs.disabled !== undefined && $attrs.disabled !== false }">
    <input
      type="checkbox"
      v-bind="$attrs"
      :checked="modelValue"
      class="h-4 w-4 rounded border-line-strong text-brand-action focus:ring-2 focus:ring-brand-action focus:ring-offset-2"
      @change="emit('update:modelValue', ($event.target as HTMLInputElement).checked)"
    />
    <span><slot>{{ label }}</slot></span>
  </label>
</template>

<script setup lang="ts">
defineOptions({ inheritAttrs: false })
defineProps<{ modelValue?: boolean; label?: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()
</script>
