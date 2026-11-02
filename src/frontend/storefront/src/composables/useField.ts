import { computed, inject, provide } from 'vue'
import type { ComputedRef, InjectionKey } from 'vue'

export interface FieldContext {
  id: string
  describedBy: string | undefined
  invalid: boolean
  required: boolean
}

const FIELD_KEY: InjectionKey<ComputedRef<FieldContext>> = Symbol('form-field')

/** Called by FormField: hands the control inside it its id, aria-describedby, aria-invalid and aria-required. */
export function provideField(ctx: ComputedRef<FieldContext>) {
  provide(FIELD_KEY, ctx)
}

/** The attributes a control inside a FormField must carry; empty outside a FormField. Input / Select / Textarea / Checkbox call this themselves. */
export function useFieldProps() {
  const field = inject(FIELD_KEY, null)
  return computed(() => {
    const f = field?.value
    if (!f) return {}
    return {
      id: f.id,
      'aria-describedby': f.describedBy,
      'aria-invalid': f.invalid ? true : undefined,
      'aria-required': f.required ? true : undefined,
    }
  })
}
