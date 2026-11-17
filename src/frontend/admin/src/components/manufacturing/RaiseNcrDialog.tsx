import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type Ncr, type OperationReport } from '@/api/manufacturing/mfg.api'
import { Alert, Button, FormField, Input, Modal, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { fmtQty } from './shared'

/** T4.10.1: raise a non-conformance report by hand from an operation report. It records the finding (no stock or operation figure changes); the quantity is at most what the report counted. */
export function RaiseNcrDialog({ orderId, report, onClose, onDone }: { orderId: string; report: OperationReport; onClose: () => void; onDone: (ncr: Ncr) => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const counted = report.goodQty + report.scrapQty + report.reworkQty
  const [qty, setQty] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState<string | null>(null)
  const create = useMutation({
    mutationFn: () => mfgApi.createNcr({ orderId, reportId: report.id, quantity: Number(qty), description: description.trim() }),
    onSuccess: (ncr) => { toast.success(t('mfg.ncr.created', { ncr: ncr.ncr.ncrNo })); onDone(ncr) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const valid = Number(qty) > 0 && Number(qty) <= counted && description.trim().length > 0

  return (
    <Modal
      open
      size="sm"
      title={t('mfg.ncr.raiseTitle', { doc: report.docNo })}
      onClose={onClose}
      closeOnEsc={!create.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button disabled={!valid} loading={create.isPending} onClick={() => create.mutate()}>{t('mfg.ncr.raise')}</Button></>}
    >
      <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); if (valid) create.mutate() }} noValidate>
        <p className="text-sm text-ink-secondary">{t('mfg.ncr.raiseHint', { qty: fmtQty(counted), seq: report.operationSeq })}</p>
        {error && <Alert variant="danger">{error}</Alert>}
        <FormField label={t('mfg.ncr.qty')} required><Input type="number" step="any" min="0" max={counted} value={qty} onChange={(e) => setQty(e.target.value)} /></FormField>
        <FormField label={t('mfg.ncr.description')} required><Textarea rows={3} maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} /></FormField>
      </form>
    </Modal>
  )
}
