<template>
  <div>
    <label :for="id" class="mb-1 block text-sm font-medium text-ink-secondary">
      {{ label }}
      <span v-if="required" aria-hidden="true" class="ml-0.5 text-danger">*</span>
    </label>
    <slot />
    <p v-if="error" :id="errorId" role="alert" class="mt-1 text-xs text-danger-soft-text">{{ error }}</p>
    <p v-else-if="hint" :id="hintId" class="mt-1 text-xs text-ink-secondary">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed, useId } from 'vue'
import { provideField } from '@/composables/useField'

const props = defineProps<{
  label: string
  hint?: string
  /** The validation message; replaces the hint. This is the only place validation text is rendered. */
  error?: string
  required?: boolean
}>()

const id = useId()
const hintId = `${id}-hint`
const errorId = `${id}-error`

provideField(
  computed(() => ({
    id,
    describedBy: props.error ? errorId : props.hint ? hintId : undefined,
    invalid: !!props.error,
    required: !!props.required,
  })),
)
</script>
