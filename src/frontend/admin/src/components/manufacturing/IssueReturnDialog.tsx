import { useMemo, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Trash2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { mfgApi, type DocumentAnswer, type MaterialLineBody, type Order, type OrderMaterial } from '@/api/manufacturing/mfg.api'
import { inventoryApi, type InventoryLot } from '@/api/inventory.api'
import { Alert, Button, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { errorCodeOf } from './orderActions'
import { fmtQty, useIdempotencyKey } from './shared'

interface Entry { id: number; lineNo: number | ''; quantity: string; lotText: string; lotId: string | null }
const OVER_ISSUE = 'MFG_MAT_002'

const netIssued = (m: OrderMaterial) => m.issuedQty - m.returnedQty
const toNumber = (v: string) => (v.trim() === '' ? NaN : Number(v))

interface IssueReturnDialogProps {
  order: Order
  mode: 'issue' | 'return'
  /** Opens with one row for this material line, filled with what is still to issue (issue) or to return (return). */
  initialLineNo?: number
  onClose: () => void
  onDone: (answer: DocumentAnswer) => void
}

/**
 * S18 - issue material to the order, or return it. Lots are typed or scanned (a scanner types the code and presses Enter, which picks the
 * matching lot and never submits the form); Suggest fills first-expiry-first lots. The server decides every limit and says why when it refuses.
 */
export function IssueReturnDialog({ order, mode, initialLineNo, onClose, onDone }: IssueReturnDialogProps) {
  const { t } = useTranslation()
  const toast = useToast()
  const keyFor = useIdempotencyKey()
  // R234: a backflush material is booked by the operation report / receipt, never issued by hand
  const candidates = order.materials.filter((m) => (mode === 'issue' ? m.issueMethod !== 'Backflush' : netIssued(m) > 0))
  const first = candidates.find((m) => m.lineNo === initialLineNo)
  const [nextId, setNextId] = useState(2)
  const [entries, setEntries] = useState<Entry[]>(first ? [{ id: 1, lineNo: first.lineNo, quantity: defaultQuantity(first, mode), lotText: '', lotId: null }] : [])
  const [note, setNote] = useState('')
  const [overReasonId, setOverReasonId] = useState('')
  const [needOverReason, setNeedOverReason] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [lotErrors, setLotErrors] = useState<Record<number, string>>({})
  const [hints, setHints] = useState<Record<number, string>>({})

  const productIds = useMemo(() => [...new Set(entries.map((e) => order.materials.find((m) => m.lineNo === e.lineNo)?.componentItemId).filter((x): x is string => !!x))], [entries, order.materials])
  const { data: lotMap = {} } = useQuery({
    queryKey: ['mfg', 'lot-lookup', productIds.join(',')],
    queryFn: async () => {
      const out: Record<string, InventoryLot[]> = {}
      await Promise.all(productIds.map(async (id) => { try { out[id] = await inventoryApi.lotsByProduct(id) } catch { out[id] = [] } }))
      return out
    },
    enabled: productIds.length > 0,
    staleTime: 15_000,
  })
  const { data: reasons = [] } = useQuery({ queryKey: ['mfg', 'reasons'], queryFn: () => mfgApi.listReasons(), enabled: needOverReason, staleTime: 30_000 })
  const overReasons = reasons.filter((r) => r.type === 'OverIssue' && r.isActive)

  const materialOf = (e: Entry) => order.materials.find((m) => m.lineNo === e.lineNo)
  const lotsOf = (e: Entry) => lotMap[materialOf(e)?.componentItemId ?? ''] ?? []
  const patch = (id: number, p: Partial<Entry>) => setEntries((rows) => rows.map((r) => (r.id === id ? { ...r, ...p } : r)))

  /** Matches the typed / scanned lot number to a known lot of the material. */
  const resolveLot = (e: Entry, text = e.lotText): string | null => {
    const wanted = text.trim().toLowerCase()
    if (!wanted) { patch(e.id, { lotId: null, lotText: '' }); setLotErrors((x) => ({ ...x, [e.id]: '' })); return null }
    const hit = lotsOf(e).find((l) => l.lotNumber.toLowerCase() === wanted)
    if (hit) { patch(e.id, { lotId: hit.lotId, lotText: hit.lotNumber }); setLotErrors((x) => ({ ...x, [e.id]: '' })); return hit.lotId }
    patch(e.id, { lotId: null })
    setLotErrors((x) => ({ ...x, [e.id]: t('mfg.issue.unknownLot', { lot: text.trim() }) }))
    return null
  }

  const suggest = useMutation({
    mutationFn: async (e: Entry) => {
      const qty = toNumber(e.quantity)
      return { entry: e, answer: await mfgApi.suggestLots(order.id, e.lineNo as number, qty) }
    },
    onSuccess: ({ entry, answer }) => {
      const picks = answer.suggestion.lots.filter((l) => l.take > 0)
      const covered = answer.reservedRemaining >= answer.quantity
      setHints((h) => ({ ...h, [entry.id]: covered ? t('mfg.issue.reservedCovers', { qty: fmtQty(answer.reservedRemaining) }) : t('mfg.issue.shortfall', { qty: fmtQty(answer.suggestion.shortfall) }) }))
      if (covered || picks.length === 0) { patch(entry.id, { lotId: null, lotText: '' }); return }
      let id = nextId
      const rows: Entry[] = picks.map((l) => ({ id: id++, lineNo: entry.lineNo, quantity: String(l.take), lotText: l.lotNumber, lotId: l.lotId }))
      setNextId(id)
      setEntries((all) => all.flatMap((r) => (r.id === entry.id ? rows : [r])))
    },
    onError: (e) => setError(serverMessage(e, t('mfg.issue.suggestFailed'))),
  })

  const submit = useMutation({
    mutationFn: () => {
      const lines: MaterialLineBody[] = entries.map((e) => ({ lineNo: e.lineNo as number, quantity: toNumber(e.quantity), lotId: e.lotId }))
      const body = { lines, reasonCodeId: needOverReason && overReasonId ? overReasonId : null, note: note.trim() || null }
      return (mode === 'issue' ? mfgApi.issueMaterial : mfgApi.returnMaterial)(order.id, body, keyFor(body))
    },
    onSuccess: (answer) => { answer.pending ? toast.info(t('mfg.doc.acceptedPending', { doc: answer.document.docNo })) : toast.success(t('mfg.doc.postedAs', { doc: answer.document.docNo })); onDone(answer) },
    onError: (e) => {
      setError(serverMessage(e, t('common.failedToSave')))
      if (mode === 'issue' && errorCodeOf(e) === OVER_ISSUE) setNeedOverReason(true)
    },
  })

  const addLine = () => { setEntries((r) => [...r, { id: nextId, lineNo: '', quantity: '', lotText: '', lotId: null }]); setNextId(nextId + 1) }
  const valid = entries.length > 0 && entries.every((e) => e.lineNo !== '' && toNumber(e.quantity) > 0 && !lotErrors[e.id]) && (!needOverReason || !!overReasonId)

  const onSubmit = () => {
    setError(null)
    // a lot typed but not yet matched is matched now; one that matches nothing stops the submit
    let ok = true
    for (const e of entries) if (e.lotText.trim() && !e.lotId && resolveLot(e) === null) ok = false
    if (ok) submit.mutate()
  }

  return (
    <Modal
      open
      size="lg"
      title={t(mode === 'issue' ? 'mfg.issue.issueTitle' : 'mfg.issue.returnTitle', { no: order.orderNo })}
      onClose={onClose}
      closeOnEsc={!submit.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button disabled={!valid} loading={submit.isPending} onClick={onSubmit}>{t(mode === 'issue' ? 'mfg.issue.issue' : 'mfg.issue.return')}</Button></>}
    >
      <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); if (valid) onSubmit() }} noValidate>
        <p className="text-sm text-ink-secondary">{t(mode === 'issue' ? 'mfg.issue.issueHint' : 'mfg.issue.returnHint')}</p>
        {error && <Alert variant="danger">{error}</Alert>}
        {needOverReason && (
          <FormField label={t('mfg.issue.overReason')} required hint={t('mfg.issue.overReasonHint')}>
            <Select value={overReasonId} onChange={(e) => setOverReasonId(e.target.value)}>
              <option value="">{t('common.select')}</option>
              {overReasons.map((r) => <option key={r.id} value={r.id}>{r.code} - {r.name}</option>)}
            </Select>
          </FormField>
        )}
        {entries.length === 0 && <p className="text-sm text-ink-secondary">{t('mfg.issue.noLines')}</p>}
        <ul className="space-y-3">
          {entries.map((e, i) => {
            const m = materialOf(e)
            const listId = `lots-${e.id}`
            return (
              <li key={e.id} className="rounded-lg border border-line p-3">
                <div className="grid gap-3 sm:grid-cols-[2fr_1fr_1.5fr_auto] sm:items-end">
                  <FormField label={t('mfg.issue.material')}>
                    <Select value={e.lineNo} onChange={(ev) => { const line = Number(ev.target.value); const mat = order.materials.find((x) => x.lineNo === line); patch(e.id, { lineNo: line || '', quantity: mat ? defaultQuantity(mat, mode) : e.quantity, lotText: '', lotId: null }) }}>
                      <option value="">{t('common.select')}</option>
                      {candidates.map((c) => <option key={c.lineNo} value={c.lineNo}>{c.componentCode} - {c.componentName}</option>)}
                    </Select>
                  </FormField>
                  <FormField label={t('mfg.issue.quantity')}>
                    <Input type="number" step="any" min="0" value={e.quantity} onChange={(ev) => patch(e.id, { quantity: ev.target.value })} />
                  </FormField>
                  <FormField label={t('mfg.issue.lot')} error={lotErrors[e.id] || undefined} hint={e.lotId ? t('mfg.issue.lotFound') : t('mfg.issue.lotHint')}>
                    <Input
                      list={listId}
                      value={e.lotText}
                      autoComplete="off"
                      onChange={(ev) => patch(e.id, { lotText: ev.target.value, lotId: null })}
                      onBlur={() => e.lotText.trim() && !e.lotId && resolveLot(e)}
                      onKeyDown={(ev) => { if (ev.key === 'Enter') { ev.preventDefault(); resolveLot(e) } }}
                    />
                    <datalist id={listId}>{lotsOf(e).filter((l) => mode === 'return' || l.status === undefined || l.status === 'Available').map((l) => <option key={l.lotId} value={l.lotNumber}>{fmtQty(l.remainingQuantity)}</option>)}</datalist>
                  </FormField>
                  <div className="flex gap-2">
                    {mode === 'issue' && (
                      <Button type="button" size="sm" variant="secondary" disabled={e.lineNo === '' || !(toNumber(e.quantity) > 0)} loading={suggest.isPending && suggest.variables?.id === e.id} onClick={() => suggest.mutate(e)}>{t('mfg.issue.suggest')}</Button>
                    )}
                    <Button type="button" size="sm" variant="ghost" iconOnly aria-label={t('mfg.issue.removeLine', { n: i + 1 })} onClick={() => setEntries((rows) => rows.filter((r) => r.id !== e.id))}><Trash2 size={16} aria-hidden="true" /></Button>
                  </div>
                </div>
                {m && (
                  <p className="mt-2 text-xs text-ink-secondary">
                    {t('mfg.issue.figures', { required: fmtQty(m.requiredQty), issued: fmtQty(m.issuedQty), returned: fmtQty(m.returnedQty), reserved: fmtQty(m.reservedQty) })}
                    {hints[e.id] ? ` · ${hints[e.id]}` : ''}
                  </p>
                )}
              </li>
            )
          })}
        </ul>
        <Button type="button" variant="outline" size="sm" onClick={addLine}>{t('mfg.issue.addLine')}</Button>
        <FormField label={t('mfg.reasonDialog.note')}><Textarea rows={2} maxLength={500} value={note} onChange={(ev) => setNote(ev.target.value)} /></FormField>
      </form>
    </Modal>
  )
}

/** What is still to issue (required less net issued) or to return (net issued). */
function defaultQuantity(m: OrderMaterial, mode: 'issue' | 'return'): string {
  const q = mode === 'issue' ? m.requiredQty - netIssued(m) - m.consumedQty : netIssued(m) - m.consumedQty
  return q > 0 ? String(Math.round(q * 1e6) / 1e6) : ''
}
