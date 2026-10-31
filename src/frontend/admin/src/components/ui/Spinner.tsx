import { Loader2 } from 'lucide-react'
import { cx } from './cx'

interface SpinnerProps {
  size?: number
  className?: string
  /** Accessible name when the spinner stands alone; inside a button the button's own label is enough. */
  label?: string
}

export function Spinner({ size = 16, className, label }: SpinnerProps) {
  return (
    <span role={label ? 'status' : undefined} aria-label={label} className="inline-flex">
      <Loader2 size={size} strokeWidth={1.75} aria-hidden="true" className={cx('animate-spin motion-reduce:animate-none', className)} />
    </span>
  )
}
