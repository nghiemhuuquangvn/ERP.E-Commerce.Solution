import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { mfgApi, type OrderReasonType } from '@/api/manufacturing/mfg.api'
import { Alert, Button, FormField, Modal, Select, Textarea } from '@/components/ui'

interface ReasonCodeDialogProps {
  open: boolean
  title: string
  message?: string
  /** The type of reason the server wants (Cancel, Hold, ShortClose, OverIssue ...). */
  type: OrderReasonType
  confirmLabel: string
  busy?: boolean
  error?: string | null
  danger?: boolean
  onConfirm: (reasonCodeId: string, note: string) => void
  onCancel: () => void
}

/** A confirmation that needs a reason code of one type from the plant's catalog, plus an optional note. */
export function ReasonCodeDialog({ open, title, message, type, confirmLabel, busy = false, error, danger = false, onConfirm, onCancel }: ReasonCodeDialogProps) {
  const { t } = useTranslation()
  const [reasonId, setReasonId] = useState('')
  const [note, setNote] = useState('')
  const { data: reasons = [], isLoading } = useQuery({ queryKey: ['mfg', 'reasons'], queryFn: () => mfgApi.listReasons(), enabled: open, staleTime: 30_000 })
  const usable = reasons.filter((r) => r.type === type && r.isActive)

  useEffect(() => {
    if (open) { setReasonId(''); setNote('') }
  }, [open])

  return (
    <Modal
      open={open}
      title={title}
      size="sm"
      onClose={onCancel}
      closeOnEsc={!busy}
      footer={
        <>
          <Button variant="outline" onClick={onCancel} disabled={busy}>{t('common.cancel')}</Button>
          <Button variant={danger ? 'danger' : 'primary'} disabled={!reasonId} loading={busy} onClick={() => onConfirm(reasonId, note.trim())}>{confirmLabel}</Button>
        </>
      }
    >
      <div className="space-y-4">
        {message && <p className="text-sm text-ink-secondary">{message}</p>}
        {error && <Alert variant="danger">{error}</Alert>}
        {!isLoading && usable.length === 0 && (
          <Alert variant="warning">
            {t('mfg.reasonDialog.none', { type: t(`mfg.reason.${type}`) })} <Link className="font-medium text-brand-action underline" to="/manufacturing/catalogs">{t('mfg.nav.catalogs')}</Link>
          </Alert>
        )}
        <FormField label={t('mfg.reasonDialog.reason')} required>
          <Select value={reasonId} onChange={(e) => setReasonId(e.target.value)}>
            <option value="">{t('common.select')}</option>
            {usable.map((r) => <option key={r.id} value={r.id}>{r.code} - {r.name}</option>)}
          </Select>
        </FormField>
        <FormField label={t('mfg.reasonDialog.note')}>
          <Textarea value={note} onChange={(e) => setNote(e.target.value)} rows={2} maxLength={500} />
        </FormField>
      </div>
    </Modal>
  )
}
