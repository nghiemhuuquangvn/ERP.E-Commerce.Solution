import { cx } from './cx'

interface ProgressProps {
  /** 0-100; leave out while the amount is not known (the bar then only shows that something is happening). */
  value?: number | null
  /** Accessible name of the bar (what is progressing). */
  label: string
  className?: string
}

/** A horizontal progress bar. Announced to screen readers as a progressbar with its value; motion stops under reduced motion. */
export function Progress({ value, label, className }: ProgressProps) {
  const known = typeof value === 'number' && Number.isFinite(value)
  const percent = known ? Math.max(0, Math.min(100, value as number)) : 0
  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={known ? Math.round(percent) : undefined}
      className={cx('h-2 w-full overflow-hidden rounded-full bg-surface-muted', className)}
    >
      <div
        className={cx('h-full rounded-full bg-brand-action transition-[width] duration-200 motion-reduce:transition-none', !known && 'w-1/3 motion-safe:animate-pulse')}
        style={known ? { width: `${percent}%` } : undefined}
      />
    </div>
  )
}
