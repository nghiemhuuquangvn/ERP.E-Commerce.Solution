import type { ReactNode } from 'react'
import { cx } from './cx'

export type BadgeVariant = 'success' | 'warning' | 'danger' | 'info' | 'muted' | 'brand'

const STYLE: Record<BadgeVariant, string> = {
  success: 'bg-success-soft text-success-soft-text',
  warning: 'bg-warning-soft text-warning-soft-text',
  danger: 'bg-danger-soft text-danger-soft-text',
  info: 'bg-info-soft text-info-soft-text',
  muted: 'bg-muted-soft text-muted-soft-text',
  brand: 'bg-brand-soft text-brand-soft-text',
}
const DOT: Record<BadgeVariant, string> = {
  success: 'bg-success',
  warning: 'bg-warning',
  danger: 'bg-danger',
  info: 'bg-info',
  muted: 'bg-muted',
  brand: 'bg-brand-action',
}

interface BadgeProps {
  variant?: BadgeVariant
  /** A small coloured dot before the text. The text always carries the meaning, never the colour alone. */
  dot?: boolean
  className?: string
  children: ReactNode
}

export function Badge({ variant = 'muted', dot = false, className, children }: BadgeProps) {
  return (
    <span className={cx('inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium', STYLE[variant], className)}>
      {dot && <span aria-hidden="true" className={cx('h-1.5 w-1.5 rounded-full', DOT[variant])} />}
      {children}
    </span>
  )
}
