import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type DocumentAnswer, type Order, type ReceiptLineBody } from '@/api/manufacturing/mfg.api'
import { Alert, Button, FormField, Input, Modal, Textarea, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'
import { fmtQty, useIdempotencyKey } from './shared'

interface Row { outputLineNo: number | null; code: string; name: string; planned: number; received: number; quantity: string; lot: string }

interface ReceiptDialogProps {
  order: Order
  onClose: () => void
  onDone: (answer: DocumentAnswer) => void
}

/**
 * S19 - receive finished goods: the main product and, when the order has them, co- and by-products. A lot number may be typed or scanned; left empty
 * it is generated from the plant's format. The server enforces the limits (MFG_FG_001), lot uniqueness (MFG_FG_002) and the backflush of unbound materials.
 */
export function ReceiptDialog({ order, onClose, onDone }: ReceiptDialogProps) {
  const { t } = useTranslation()
  const toast = useToast()
  const keyFor = useIdempotencyKey()
  const { hasPermission } = usePermission()
  const canReadSettings = hasPermission('manufacturing:masterdata:view')
  const { data: settings } = useQuery({ queryKey: ['mfg', 'settings', order.plantId], queryFn: () => mfgApi.getSettings(order.plantId), enabled: canReadSettings, staleTime: 60_000 })
  const tolerance = settings?.values.overProductionTolerancePct
  const ceiling = (planned: number) => (tolerance == null ? null : planned * (1 + tolerance / 100))

  const [rows, setRows] = useState<Row[]>(() => [
    { outputLineNo: null, code: order.itemCode, name: order.itemName, planned: order.quantity, received: order.completedQty, quantity: '', lot: '' },
    ...order.outputs.filter((o) => o.lineType !== 'Component').map((o) => ({ outputLineNo: o.lineNo, code: o.itemCode, name: o.itemName, planned: o.plannedQty, received: o.receivedQty, quantity: '', lot: '' })),
  ])
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const patch = (i: number, p: Partial<Row>) => setRows((all) => all.map((r, j) => (j === i ? { ...r, ...p } : r)))
  const lines = rows.filter((r) => Number(r.quantity) > 0)

  const submit = useMutation({
    mutationFn: () => {
      const body: { lines: ReceiptLineBody[]; note: string | null } = { lines: lines.map((r) => ({ outputLineNo: r.outputLineNo, quantity: Number(r.quantity), lotNumber: r.lot.trim() || null })), note: note.trim() || null }
      return mfgApi.receiveGoods(order.id, body, keyFor(body))
    },
    onSuccess: (answer) => { answer.pending ? toast.info(t('mfg.doc.acceptedPending', { doc: answer.document.docNo })) : toast.success(t('mfg.doc.postedAs', { doc: answer.document.docNo })); onDone(answer) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })

  return (
    <Modal
      open
      size="lg"
      title={t('mfg.receipt.title', { no: order.orderNo })}
      onClose={onClose}
      closeOnEsc={!submit.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button disabled={lines.length === 0} loading={submit.isPending} onClick={() => { setError(null); submit.mutate() }}>{t('mfg.receipt.receive')}</Button></>}
    >
      <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); if (lines.length > 0) { setError(null); submit.mutate() } }} noValidate>
        <p className="text-sm text-ink-secondary">{t('mfg.receipt.hint')}</p>
        {error && <Alert variant="danger">{error}</Alert>}
        <ul className="space-y-3">
          {rows.map((r, i) => {
            const max = ceiling(r.planned)
            return (
              <li key={r.outputLineNo ?? 'main'} className="rounded-lg border border-line p-3">
                <div className="mb-2 flex flex-wrap items-baseline gap-2">
                  <span className="font-mono text-sm font-semibold text-ink">{r.code}</span>
                  <span className="text-sm text-ink-secondary">{r.name}</span>
                  <span className="text-xs text-ink-secondary">{r.outputLineNo == null ? t('mfg.doc.mainProduct') : t('mfg.doc.output', { n: r.outputLineNo })}</span>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <FormField label={t('mfg.receipt.quantity')} hint={t('mfg.receipt.figures', { received: fmtQty(r.received), planned: fmtQty(r.planned), toGo: fmtQty(Math.max(0, r.planned - r.received)) }) + (max != null ? ` · ${t('mfg.receipt.ceiling', { max: fmtQty(max) })}` : '')}>
                    <Input type="number" step="any" min="0" value={r.quantity} onChange={(e) => patch(i, { quantity: e.target.value })} />
                  </FormField>
                  <FormField label={t('mfg.receipt.lot')} hint={t('mfg.receipt.lotHint')}>
                    <Input
                      value={r.lot}
                      autoComplete="off"
                      placeholder={t('mfg.receipt.lotAuto')}
                      onChange={(e) => patch(i, { lot: e.target.value })}
                      onKeyDown={(e) => { if (e.key === 'Enter') e.preventDefault() }}   // a scanner ends with Enter: it fills the field, it must not submit the form
                    />
                  </FormField>
                </div>
              </li>
            )
          })}
        </ul>
        <FormField label={t('mfg.reasonDialog.note')}><Textarea rows={2} maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} /></FormField>
      </form>
    </Modal>
  )
}
