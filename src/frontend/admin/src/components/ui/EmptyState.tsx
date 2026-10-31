import type { ReactNode } from 'react'
import { AlertCircle, Inbox, Lock, SearchX } from 'lucide-react'
import { cx } from './cx'

export type EmptyStateVariant = 'empty' | 'no-results' | 'error' | 'forbidden'
const ICON = { empty: Inbox, 'no-results': SearchX, error: AlertCircle, forbidden: Lock }

interface EmptyStateProps {
  /** empty = nothing yet; no-results = filters hide everything (action: clear filters); error = load failed (action: retry); forbidden = no permission (no action). */
  variant?: EmptyStateVariant
  title: string
  description?: string
  /** One primary action, e.g. a Button. */
  action?: ReactNode
  className?: string
}

export function EmptyState({ variant = 'empty', title, description, action, className }: EmptyStateProps) {
  const Icon = ICON[variant]
  return (
    <div className={cx('flex flex-col items-center justify-center gap-2 px-4 py-10 text-center', className)}>
      <Icon size={48} strokeWidth={1.5} aria-hidden="true" className={variant === 'error' ? 'text-danger' : 'text-ink-muted'} />
      <p className="text-sm font-medium text-ink">{title}</p>
      {description && <p className="max-w-sm text-sm text-ink-secondary">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}
