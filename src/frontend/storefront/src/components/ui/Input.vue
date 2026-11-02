<template>
  <input
    v-bind="{ ...field, ...$attrs }"
    :value="modelValue"
    :class="[CONTROL, 'h-10 py-2', invalid ? BAD : OK]"
    @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useFieldProps } from '@/composables/useField'
import { BAD, CONTROL, OK } from './controlStyles'

defineOptions({ inheritAttrs: false })
const props = defineProps<{
  modelValue?: string | number | null
  /** Marks the control invalid when it is used without a FormField that has an error. */
  error?: boolean
}>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const field = useFieldProps()
const invalid = computed(() => props.error || field.value['aria-invalid'] === true)
</script>
