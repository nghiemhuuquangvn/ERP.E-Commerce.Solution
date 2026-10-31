import { createContext, useContext, useId } from 'react'
import type { ReactNode } from 'react'
import { cx } from './cx'

interface FieldContextValue {
  id: string
  describedBy?: string
  invalid: boolean
  required: boolean
}
const FieldContext = createContext<FieldContextValue | null>(null)

/**
 * The attributes a control inside a FormField must carry (id, aria-describedby, aria-invalid, aria-required).
 * Input / Textarea / Select call this themselves; a custom control can spread the result.
 */
export function useFieldProps() {
  const f = useContext(FieldContext)
  if (!f) return {}
  return {
    id: f.id,
    'aria-describedby': f.describedBy,
    'aria-invalid': f.invalid || undefined,
    'aria-required': f.required || undefined,
  }
}

interface FormFieldProps {
  label: string
  hint?: string
  /** The validation message; replaces the hint. This is the only place validation text is rendered. */
  error?: string
  required?: boolean
  className?: string
  children: ReactNode
}

export function FormField({ label, hint, error, required = false, className, children }: FormFieldProps) {
  const id = useId()
  const hintId = `${id}-hint`
  const errorId = `${id}-error`
  const describedBy = error ? errorId : hint ? hintId : undefined
  return (
    <FieldContext.Provider value={{ id, describedBy, invalid: !!error, required }}>
      <div className={className}>
        <label htmlFor={id} className="mb-1 block text-sm font-medium text-ink-secondary">
          {label}
          {required && <span aria-hidden="true" className="ml-0.5 text-danger">*</span>}
        </label>
        {children}
        {error ? (
          <p id={errorId} role="alert" className="mt-1 text-xs text-danger-soft-text">{error}</p>
        ) : hint ? (
          <p id={hintId} className={cx('mt-1 text-xs text-ink-secondary')}>{hint}</p>
        ) : null}
      </div>
    </FieldContext.Provider>
  )
}
