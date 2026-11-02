<template>
  <select
    v-bind="{ ...field, ...$attrs }"
    :value="modelValue"
    :class="[CONTROL, 'h-10 py-1.5', invalid ? BAD : OK]"
    @change="emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
  >
    <slot />
  </select>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useFieldProps } from '@/composables/useField'
import { BAD, CONTROL, OK } from './controlStyles'

defineOptions({ inheritAttrs: false })
const props = defineProps<{ modelValue?: string | number | null; error?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const field = useFieldProps()
const invalid = computed(() => props.error || field.value['aria-invalid'] === true)
</script>
