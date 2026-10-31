import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { FormField } from '@/components/ui/FormField'
import { Input, Textarea } from '@/components/ui/Input'
import { Modal } from '@/components/ui/Modal'

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

  // A reopened dialog starts empty instead of showing the last reason.
  useEffect(() => {
    if (open) {
      setReason('')
      setDate('')
    }
  }, [open])

  return (
    <Modal
      open={open}
      title={title}
      onClose={onCancel}
      size="sm"
      closeOnEsc={!busy}
      footer={
        <>
          <Button variant="outline" onClick={onCancel}>
            {t('common.cancel')}
          </Button>
          <Button
            variant={danger ? 'danger' : 'primary'}
            loading={busy}
            disabled={reason.trim().length === 0}
            onClick={() => onConfirm(reason.trim(), date)}
          >
            {confirmLabel}
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        {message && <p className="text-sm text-ink-secondary">{message}</p>}
        <FormField label={t('common.reason')}>
          <Textarea value={reason} onChange={(e) => setReason(e.target.value)} />
        </FormField>
        {withDate && (
          <FormField label={dateLabel ?? ''}>
            <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </FormField>
        )}
        {error && <Alert variant="danger">{error}</Alert>}
      </div>
    </Modal>
  )
}
