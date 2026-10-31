import { cx } from './cx'

interface SwitchProps {
  checked: boolean
  onChange: (checked: boolean) => void
  /** The accessible name (what the switch turns on or off). */
  label: string
  disabled?: boolean
  className?: string
}

/** An on / off setting that applies immediately (follow it with a toast for the result). */
export function Switch({ checked, onChange, label, disabled, className }: SwitchProps) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={cx(
        'relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action focus-visible:ring-offset-2',
        'disabled:cursor-not-allowed disabled:opacity-50',
        checked ? 'bg-brand-action' : 'bg-ink-muted',
        className,
      )}
    >
      <span
        aria-hidden="true"
        className={cx('inline-block h-5 w-5 rounded-full bg-surface shadow transition-transform', checked ? 'translate-x-5' : 'translate-x-0.5')}
      />
    </button>
  )
}
