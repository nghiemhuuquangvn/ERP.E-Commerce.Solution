import { useState } from 'react'
import { useTranslation } from 'react-i18next'

interface ReasonDialogProps {
  open: boolean
  title: string
  message?: string
  confirmLabel: string
  /** Shows a date field (e.g. the date of a reversal); the chosen value is passed to onConfirm, empty = default. */
  withDate?: boolean
  dateLabel?: string
  busy?: boolean
  error?: string | null
  danger?: boolean
  onConfirm: (reason: string, date: string) => void
  onCancel: () => void
}

/** A confirmation that needs a written reason (reverse / void a journal entry, reopen a fiscal period). */
export function ReasonDialog({
  open,
  title,
  message,
  confirmLabel,
  withDate = false,
  dateLabel,
  busy = false,
  error,
  danger = false,
  onConfirm,
  onCancel,
}: ReasonDialogProps) {
  const { t } = useTranslation()
  const [reason, setReason] = useState('')
  const [date, setDate] = useState('')
  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-2 text-base font-semibold text-gray-900">{title}</h3>
        {message && <p className="mb-4 text-sm text-gray-600">{message}</p>}
        <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.reason')}</label>
        <textarea className="input mb-3" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
        {withDate && (
          <>
            <label className="mb-1 block text-sm font-medium text-gray-700">{dateLabel}</label>
            <input type="date" className="input mb-3" value={date} onChange={(e) => setDate(e.target.value)} />
          </>
        )}
        {error && <p className="mb-3 text-xs text-red-600">{error}</p>}
        <div className="flex justify-end gap-3">
          <button onClick={onCancel} className="btn-outline">
            {t('common.cancel')}
          </button>
          <button
            disabled={busy || reason.trim().length === 0}
            onClick={() => onConfirm(reason.trim(), date)}
            className={danger ? 'btn-danger' : 'btn-primary'}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}
