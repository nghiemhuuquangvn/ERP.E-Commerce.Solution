<template>
  <textarea
    v-bind="{ ...field, ...$attrs }"
    :value="modelValue"
    :rows="rows"
    :class="[CONTROL, 'py-2', invalid ? BAD : OK]"
    @input="emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useFieldProps } from '@/composables/useField'
import { BAD, CONTROL, OK } from './controlStyles'

defineOptions({ inheritAttrs: false })
const props = withDefaults(defineProps<{ modelValue?: string | null; error?: boolean; rows?: number }>(), { rows: 3 })
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const field = useFieldProps()
const invalid = computed(() => props.error || field.value['aria-invalid'] === true)
</script>
