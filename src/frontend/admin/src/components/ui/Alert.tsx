import type { ReactNode } from 'react'
import { AlertCircle, AlertTriangle, CheckCircle2, Info, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cx } from './cx'

export type AlertVariant = 'info' | 'success' | 'warning' | 'danger'

const STYLE: Record<AlertVariant, string> = {
  info: 'border-info/30 bg-info-soft text-info-soft-text',
  success: 'border-success/30 bg-success-soft text-success-soft-text',
  warning: 'border-warning/30 bg-warning-soft text-warning-soft-text',
  danger: 'border-danger/30 bg-danger-soft text-danger-soft-text',
}
const ICON = { info: Info, success: CheckCircle2, warning: AlertTriangle, danger: AlertCircle }

interface AlertProps {
  variant?: AlertVariant
  title?: string
  /** Shows a dismiss button. */
  onClose?: () => void
  className?: string
  children: ReactNode
}

/** An inline, persistent message inside a form or card (a refused action, a warning). Danger is announced at once. */
export function Alert({ variant = 'info', title, onClose, className, children }: AlertProps) {
  const { t } = useTranslation()
  const Icon = ICON[variant]
  return (
    <div role={variant === 'danger' ? 'alert' : 'status'} className={cx('flex items-start gap-3 rounded-md border px-3 py-2 text-sm', STYLE[variant], className)}>
      <Icon size={18} strokeWidth={1.75} aria-hidden="true" className="mt-0.5 shrink-0" />
      <div className="min-w-0 flex-1">
        {title && <p className="font-medium">{title}</p>}
        <div>{children}</div>
      </div>
      {onClose && (
        <button type="button" onClick={onClose} aria-label={t('common.close')} className="rounded p-0.5 hover:bg-black/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action">
          <X size={16} strokeWidth={1.75} aria-hidden="true" />
        </button>
      )}
    </div>
  )
}
