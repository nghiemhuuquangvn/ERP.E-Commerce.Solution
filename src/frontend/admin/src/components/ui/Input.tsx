import { forwardRef } from 'react'
import type { InputHTMLAttributes, SelectHTMLAttributes, TextareaHTMLAttributes, ReactNode } from 'react'
import { cx } from './cx'
import { useFieldProps } from './FormField'

const CONTROL =
  'block w-full rounded-md border bg-surface px-3 text-sm text-ink shadow-sm placeholder:text-ink-muted ' +
  'focus:outline-none focus:ring-1 disabled:cursor-not-allowed disabled:opacity-50'
const OK = 'border-line-strong focus:border-brand-action focus:ring-brand-action'
const BAD = 'border-danger focus:border-danger focus:ring-danger'

/** Merges the FormField linkage with the props passed explicitly (explicit wins). */
function useControl(error: boolean | undefined, rest: { id?: string; 'aria-invalid'?: unknown }) {
  const field = useFieldProps()
  const invalid = error || field['aria-invalid'] === true
  return { field, invalid, props: { ...field, ...(rest.id ? { id: rest.id } : {}), 'aria-invalid': invalid || undefined } }
}

interface ErrorProp {
  /** Marks the control invalid when it is used without a FormField that has an error. */
  error?: boolean
}

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & ErrorProp>(function Input({ error, className, ...rest }, ref) {
  const { invalid, props } = useControl(error, rest)
  return <input ref={ref} {...rest} {...props} className={cx(CONTROL, 'h-9 py-2', invalid ? BAD : OK, className)} />
})

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement> & ErrorProp>(function Textarea({ error, className, rows = 3, ...rest }, ref) {
  const { invalid, props } = useControl(error, rest)
  return <textarea ref={ref} rows={rows} {...rest} {...props} className={cx(CONTROL, 'py-2', invalid ? BAD : OK, className)} />
})

/** The native select, styled; use it for a few fixed options. */
export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement> & ErrorProp>(function Select({ error, className, children, ...rest }, ref) {
  const { invalid, props } = useControl(error, rest)
  return (
    <select ref={ref} {...rest} {...props} className={cx(CONTROL, 'h-9 py-1.5', invalid ? BAD : OK, className)}>
      {children}
    </select>
  )
})

interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: ReactNode
}

/** A checkbox whose label is part of the click target (min 24 px hit area). */
export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(function Checkbox({ label, className, ...rest }, ref) {
  return (
    <label className={cx('inline-flex min-h-6 cursor-pointer items-center gap-2 text-sm text-ink', rest.disabled && 'cursor-not-allowed opacity-50', className)}>
      <input
        ref={ref}
        type="checkbox"
        {...rest}
        className="h-4 w-4 rounded border-line-strong text-brand-action focus:ring-2 focus:ring-brand-action focus:ring-offset-2"
      />
      <span>{label}</span>
    </label>
  )
})
