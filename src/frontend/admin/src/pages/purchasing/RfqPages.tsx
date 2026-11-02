import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Trash2 } from 'lucide-react'
import { procurementApi } from '@/api/purchasing/procurement.api'
import type { Rfq, RfqSummary } from '@/api/purchasing/procurement.api'
import { suppliersApi } from '@/api/suppliers.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Badge, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, Spinner, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const money = (v: number) => new Intl.NumberFormat().format(v)
const day = (v: string | null | undefined) => (v ? new Date(v).toLocaleDateString() : '—')
const inputDate = (offsetDays = 0) => new Date(Date.now() + offsetDays * 86_400_000).toISOString().slice(0, 10)

// ─── Create ─────────────────────────────────────────────────────────────────

const NEW_LINE = { productCode: '', productName: '', quantity: '1', unit: 'pcs' }

function CreateRfqModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { data: suppliers = [] } = useQuery({ queryKey: ['po', 'suppliers', 'active'], queryFn: () => suppliersApi.list({ isActive: true, pageSize: 200 }) })
  const [title, setTitle] = useState('')
  const [neededBy, setNeededBy] = useState('')
  const [lines, setLines] = useState([{ ...NEW_LINE }])
  const [invited, setInvited] = useState<string[]>([])
  const [errors, setErrors] = useState<Partial<Record<'title' | 'neededBy' | 'lines', string>>>({})

  const create = useMutation({
    mutationFn: () =>
      procurementApi.createRfq({
        title,
        neededBy: neededBy || null,
        notes: null,
        lines: lines.filter((l) => l.productCode.trim()).map((l) => ({ productCode: l.productCode.trim(), productName: l.productName.trim() || l.productCode.trim(), quantity: Number(l.quantity), unit: l.unit || 'pcs' })),
        supplierIds: invited,
      }),
    onSuccess: (rfq) => {
      queryClient.invalidateQueries({ queryKey: ['po', 'rfqs'] })
      toast.success(t('common.saved'))
      navigate(`/purchasing/rfqs/${rfq.id}`)
    },
  })

  const set = (i: number, key: string, value: string) => setLines(lines.map((l, j) => (j === i ? { ...l, [key]: value } : l)))

  const submit = () => {
    const found: typeof errors = {}
    if (!title.trim()) found.title = t('common.required')
    if (neededBy && neededBy < inputDate()) found.neededBy = t('procurement.rfq.neededByPast')
    const filled = lines.filter((l) => l.productCode.trim())
    if (filled.length === 0 || filled.some((l) => !(Number(l.quantity) >= 1))) found.lines = t('procurement.rfq.linesRequired')
    setErrors(found)
    if (Object.keys(found).length === 0) create.mutate()
  }

  return (
    <Modal
      open
      size="lg"
      title={t('procurement.rfq.newTitle')}
      onClose={onClose}
      closeOnEsc={!create.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="rfq-form" loading={create.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="rfq-form" noValidate onSubmit={(e) => { e.preventDefault(); submit() }} className="space-y-4">
        <div className="grid grid-cols-3 gap-3">
          <FormField className="col-span-2" label={t('procurement.rfq.title')} required error={errors.title}>
            <Input value={title} onChange={(e) => setTitle(e.target.value)} />
          </FormField>
          <FormField label={t('procurement.rfq.neededBy')} error={errors.neededBy}>
            <Input type="date" min={inputDate()} value={neededBy} onChange={(e) => setNeededBy(e.target.value)} />
          </FormField>
        </div>

        <fieldset className="space-y-2">
          <legend className="mb-1 text-sm font-semibold text-ink-secondary">{t('procurement.rfq.lines')}</legend>
          {lines.map((l, i) => (
            <div key={i} className="grid grid-cols-12 items-center gap-2">
              <Input className="col-span-3" aria-label={`${t('procurement.productCode')} ${i + 1}`} placeholder={t('procurement.productCode')} value={l.productCode} onChange={(e) => set(i, 'productCode', e.target.value)} />
              <Input className="col-span-5" aria-label={`${t('procurement.productName')} ${i + 1}`} placeholder={t('procurement.productName')} value={l.productName} onChange={(e) => set(i, 'productName', e.target.value)} />
              <Input type="number" min={1} className="col-span-2" aria-label={`${t('procurement.qty')} ${i + 1}`} value={l.quantity} onChange={(e) => set(i, 'quantity', e.target.value)} />
              <Input className="col-span-1" aria-label={`${t('procurement.unit')} ${i + 1}`} value={l.unit} onChange={(e) => set(i, 'unit', e.target.value)} />
              <Button
                iconOnly
                variant="ghost"
                aria-label={t('procurement.rfq.removeLine', { n: i + 1 })}
                disabled={lines.length === 1}
                onClick={() => setLines(lines.filter((_, j) => j !== i))}
              >
                <Trash2 size={16} aria-hidden="true" />
              </Button>
            </div>
          ))}
          {errors.lines && <p role="alert" className="text-xs text-danger-soft-text">{errors.lines}</p>}
          <Button variant="outline" size="sm" onClick={() => setLines([...lines, { ...NEW_LINE }])}>{t('procurement.rfq.addLine')}</Button>
        </fieldset>

        <fieldset>
          <legend className="mb-1 text-sm font-semibold text-ink-secondary">{t('procurement.rfq.invite')}</legend>
          <div className="grid max-h-40 grid-cols-2 gap-1 overflow-y-auto rounded-md border border-line p-2 text-sm">
            {suppliers.map((s) => (
              <Checkbox
                key={s.id}
                label={s.name}
                checked={invited.includes(s.id)}
                onChange={(e) => setInvited(e.target.checked ? [...invited, s.id] : invited.filter((x) => x !== s.id))}
              />
            ))}
            {suppliers.length === 0 && <span className="text-ink-secondary">{t('procurement.rfq.noSuppliers')}</span>}
          </div>
        </fieldset>

        {create.isError && <Alert variant="danger">{serverMessage(create.error, t('common.error'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── List ───────────────────────────────────────────────────────────────────

export function RfqListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const [status, setStatus] = useState('')
  const [creating, setCreating] = useState(false)
  const { data = [], isLoading } = useQuery({ queryKey: ['po', 'rfqs', status], queryFn: () => procurementApi.rfqs(status || undefined) })
  const canManage = hasPermission('rfq:manage')

  const columns = useMemo(
    () => [
      { key: 'rfqNumber', label: t('procurement.rfq.number'), className: 'font-mono text-xs', render: (r: RfqSummary) => <Link className="text-brand-action hover:underline" to={`/purchasing/rfqs/${r.id}`}>{r.rfqNumber}</Link> },
      { key: 'title', label: t('procurement.rfq.title') },
      { key: 'lines', label: t('procurement.rfq.lines'), align: 'right' as const },
      { key: 'quotes', label: t('procurement.rfq.quotes'), align: 'right' as const, render: (r: RfqSummary) => `${r.quoted} / ${r.invited}` },
      { key: 'neededBy', label: t('procurement.rfq.neededBy'), render: (r: RfqSummary) => day(r.neededBy) },
      { key: 'status', label: t('common.status'), render: (r: RfqSummary) => <StatusBadge status={r.status} /> },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t('procurement.rfq.listTitle')}</h1>
          <p className="text-sm text-ink-secondary">{t('procurement.rfq.hint')}</p>
        </div>
        {canManage && <Button onClick={() => setCreating(true)}>{t('procurement.rfq.add')}</Button>}
      </div>
      <Select aria-label={t('common.status')} className="max-w-xs" value={status} onChange={(e) => setStatus(e.target.value)}>
        <option value="">{t('procurement.allStatuses')}</option>
        {['Draft', 'Sent', 'Awarded', 'Cancelled'].map((s) => <option key={s} value={s}>{s}</option>)}
      </Select>
      <DataTable
        columns={columns}
        data={data}
        isLoading={isLoading}
        emptyState={
          status ? (
            <EmptyState variant="no-results" title={t('common.noResults')} action={<Button variant="outline" size="sm" onClick={() => setStatus('')}>{t('common.clear')}</Button>} />
          ) : (
            <EmptyState title={t('procurement.rfq.empty')} action={canManage ? <Button onClick={() => setCreating(true)}>{t('procurement.rfq.add')}</Button> : undefined} />
          )
        }
      />
      {creating && <CreateRfqModal onClose={() => setCreating(false)} />}
    </div>
  )
}

// ─── Quotation form ─────────────────────────────────────────────────────────

function QuotationModal({ rfq, supplierId, onClose }: { rfq: Rfq; supplierId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const existing = rfq.quotations.find((q) => q.supplierId === supplierId)
  const [quoteNumber, setQuoteNumber] = useState(existing?.quoteNumber ?? '')
  const [validUntil, setValidUntil] = useState(existing ? existing.validUntil.slice(0, 10) : inputDate(14))
  const [currency, setCurrency] = useState(existing?.currencyCode ?? 'VND')
  const [lead, setLead] = useState(String(existing?.leadTimeDays ?? 7))
  const [prices, setPrices] = useState<Record<string, string>>(
    Object.fromEntries(rfq.lines.map((l) => [l.id, String(existing?.lines.find((x) => x.rfqLineId === l.id)?.unitPrice ?? '')])),
  )
  const [errors, setErrors] = useState<Partial<Record<'prices' | 'validUntil', string>>>({})

  const save = useMutation({
    mutationFn: () =>
      procurementApi.recordQuotation(rfq.id, {
        supplierId, quoteNumber, validUntil, currencyCode: currency, leadTimeDays: Number(lead) || 0, notes: null,
        unitPrices: Object.fromEntries(Object.entries(prices).map(([k, v]) => [k, Number(v)])),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['po', 'rfq', rfq.id] })
      queryClient.invalidateQueries({ queryKey: ['po', 'rfq-comparison', rfq.id] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  const submit = () => {
    const found: typeof errors = {}
    if (rfq.lines.some((l) => !(Number(prices[l.id]) >= 0) || prices[l.id] === '')) found.prices = t('common.required')
    if (!validUntil) found.validUntil = t('common.required')
    setErrors(found)
    if (Object.keys(found).length === 0) save.mutate()
  }

  const name = rfq.invitations.find((i) => i.supplierId === supplierId)?.supplierName ?? ''
  return (
    <Modal
      open
      size="md"
      title={t('procurement.rfq.quotationTitle')}
      onClose={onClose}
      closeOnEsc={!save.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="quotation-form" loading={save.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="quotation-form" noValidate onSubmit={(e) => { e.preventDefault(); submit() }} className="space-y-4">
        <p className="text-sm text-ink-secondary">{name}</p>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('procurement.rfq.quoteNumber')}>
            <Input value={quoteNumber} onChange={(e) => setQuoteNumber(e.target.value)} />
          </FormField>
          <FormField label={t('procurement.rfq.validUntil')} required error={errors.validUntil}>
            <Input type="date" min={inputDate()} value={validUntil} onChange={(e) => setValidUntil(e.target.value)} />
          </FormField>
          <FormField label={t('procurement.currency')}>
            <Input maxLength={3} value={currency} onChange={(e) => setCurrency(e.target.value.toUpperCase())} />
          </FormField>
          <FormField label={t('procurement.rfq.leadTime')}>
            <Input type="number" min={0} value={lead} onChange={(e) => setLead(e.target.value)} />
          </FormField>
        </div>
        <fieldset className="space-y-2">
          <legend className="mb-1 text-sm font-semibold text-ink-secondary">{t('procurement.rfq.unitPrices')}</legend>
          {rfq.lines.map((l) => (
            <div key={l.id} className="grid grid-cols-12 items-center gap-2 text-sm">
              <span className="col-span-7">{l.productName} <span className="text-ink-secondary">× {l.quantity} {l.unit}</span></span>
              <Input
                type="number"
                step="0.01"
                min={0}
                className="col-span-5"
                aria-label={t('procurement.rfq.priceFor', { name: l.productName })}
                aria-invalid={(errors.prices && prices[l.id] === '') || undefined}
                value={prices[l.id]}
                onChange={(e) => setPrices({ ...prices, [l.id]: e.target.value })}
              />
            </div>
          ))}
          {errors.prices && <p role="alert" className="text-xs text-danger-soft-text">{errors.prices}</p>}
        </fieldset>
        {save.isError && <Alert variant="danger">{serverMessage(save.error, t('common.error'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Detail ─────────────────────────────────────────────────────────────────

type Step =
  | { kind: 'send' }
  | { kind: 'cancel' }
  | { kind: 'award'; supplierId: string; supplierName: string }
  | { kind: 'decline'; supplierId: string; supplierName: string }

export function RfqDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const toast = useToast()
  const [quoting, setQuoting] = useState<string | null>(null)
  const [step, setStep] = useState<Step | null>(null)
  const canManage = hasPermission('rfq:manage')

  const { data: rfq } = useQuery({ queryKey: ['po', 'rfq', id], queryFn: () => procurementApi.rfq(id!), enabled: !!id })
  const { data: cmp } = useQuery({
    queryKey: ['po', 'rfq-comparison', id], queryFn: () => procurementApi.comparison(id!),
    enabled: !!id && !!rfq && rfq.quotations.length > 0,
  })
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['po', 'rfq', id] })
    queryClient.invalidateQueries({ queryKey: ['po', 'rfqs'] })
  }
  const after = {
    onSuccess: () => {
      refresh()
      toast.success(t('common.saved'))
      setStep(null)
    },
    onError: (e: unknown) => {
      setStep(null)
      toast.error(serverMessage(e, t('common.error')))
    },
  }
  const send = useMutation({ mutationFn: () => procurementApi.sendRfq(id!), ...after })
  const cancel = useMutation({ mutationFn: () => procurementApi.cancelRfq(id!), ...after })
  const decline = useMutation({ mutationFn: (supplierId: string) => procurementApi.declineRfq(id!, supplierId), ...after })
  const award = useMutation({
    mutationFn: (supplierId: string) => procurementApi.awardRfq(id!, supplierId),
    onSuccess: (r) => {
      refresh()
      toast.success(t('common.saved'))
      setStep(null)
      if (r.awardedPurchaseOrderId) navigate(`/orders/purchase/${r.awardedPurchaseOrderId}`)
    },
    onError: after.onError,
  })

  if (!rfq) return <Spinner label={t('common.loading')} />

  const confirmStep = () => {
    if (!step) return
    if (step.kind === 'send') send.mutate()
    else if (step.kind === 'cancel') cancel.mutate()
    else if (step.kind === 'award') award.mutate(step.supplierId)
    else decline.mutate(step.supplierId)
  }
  const stepText: Record<Step['kind'], [string, string, string]> = {
    send: [t('procurement.rfq.sendTitle'), t('procurement.rfq.sendConfirm', { number: rfq.rfqNumber }), t('procurement.rfq.send')],
    cancel: [t('procurement.rfq.cancelTitle'), t('procurement.rfq.cancelConfirm', { number: rfq.rfqNumber }), t('procurement.rfq.cancel')],
    award: [
      t('procurement.rfq.awardTitle', { supplier: step && 'supplierName' in step ? step.supplierName : '' }),
      t('procurement.rfq.awardConfirm', { supplier: step && 'supplierName' in step ? step.supplierName : '' }),
      t('procurement.rfq.award'),
    ],
    decline: [
      t('procurement.rfq.declineTitle'),
      t('procurement.rfq.declineConfirm', { supplier: step && 'supplierName' in step ? step.supplierName : '' }),
      t('procurement.rfq.declined'),
    ],
  }
  const [stepTitle, stepMessage, stepConfirm] = step ? stepText[step.kind] : ['', '', '']

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-3">
        <Link to="/purchasing/rfqs" className="text-sm text-ink-secondary hover:text-ink">{t('procurement.rfq.back')}</Link>
        <h1 className="text-xl font-semibold">{rfq.rfqNumber}</h1>
        <StatusBadge status={rfq.status} />
        <span className="text-ink-secondary">{rfq.title}</span>
        <div className="ml-auto flex gap-2">
          {canManage && rfq.status === 'Draft' && <Button onClick={() => setStep({ kind: 'send' })}>{t('procurement.rfq.send')}</Button>}
          {canManage && (rfq.status === 'Draft' || rfq.status === 'Sent') && <Button variant="outline" onClick={() => setStep({ kind: 'cancel' })}>{t('procurement.rfq.cancel')}</Button>}
          {rfq.awardedPurchaseOrderId && (
            <Link className="inline-flex h-9 items-center rounded-md border border-line-strong bg-surface px-4 text-sm font-medium text-ink hover:bg-surface-muted" to={`/orders/purchase/${rfq.awardedPurchaseOrderId}`}>
              {t('procurement.rfq.openOrder')}
            </Link>
          )}
        </div>
      </div>

      <div className="grid gap-5 md:grid-cols-2">
        <div className="card p-5">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-ink-secondary">{t('procurement.rfq.lines')}</h2>
          <table className="min-w-full text-sm">
            <tbody>{rfq.lines.map((l) => <tr key={l.id}><td className="py-1">{l.productName} <span className="font-mono text-xs text-ink-secondary">{l.productCode}</span></td><td className="py-1 text-right">{l.quantity} {l.unit}</td></tr>)}</tbody>
          </table>
          {rfq.neededBy && <p className="mt-2 text-xs text-ink-secondary">{t('procurement.rfq.neededBy')}: {day(rfq.neededBy)}</p>}
        </div>
        <div className="card p-5">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-ink-secondary">{t('procurement.rfq.suppliers')}</h2>
          <table className="min-w-full text-sm">
            <tbody>
              {rfq.invitations.map((i) => (
                <tr key={i.supplierId}>
                  <td className="py-1">{i.supplierName}</td>
                  <td className="py-1"><StatusBadge status={i.state} /></td>
                  <td className="py-1 text-right">
                    {canManage && rfq.status === 'Sent' && (
                      <div className="flex justify-end gap-3">
                        <Button
                          variant="link"
                          className="text-xs"
                          aria-label={`${i.state === 'Quoted' ? t('procurement.rfq.editQuote') : t('procurement.rfq.enterQuote')}: ${i.supplierName}`}
                          onClick={() => setQuoting(i.supplierId)}
                        >
                          {i.state === 'Quoted' ? t('procurement.rfq.editQuote') : t('procurement.rfq.enterQuote')}
                        </Button>
                        {i.state === 'Invited' && (
                          <Button
                            variant="link"
                            className="text-xs"
                            aria-label={`${t('procurement.rfq.declined')}: ${i.supplierName}`}
                            onClick={() => setStep({ kind: 'decline', supplierId: i.supplierId, supplierName: i.supplierName })}
                          >
                            {t('procurement.rfq.declined')}
                          </Button>
                        )}
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {cmp && cmp.suppliers.length > 0 && (
        <div className="card overflow-x-auto p-5">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-ink-secondary">{t('procurement.rfq.comparison')}</h2>
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs uppercase text-ink-secondary">
                <th className="pb-2">{t('procurement.productName')}</th>
                <th className="pb-2 text-right">{t('procurement.qty')}</th>
                {cmp.suppliers.map((s) => (
                  <th key={s.supplierId} className={`pb-2 text-right ${s.expired ? 'text-ink-muted' : ''}`}>
                    {s.supplierName}
                    <div className="text-[10px] normal-case text-ink-secondary">{s.quoteNumber} · {s.currencyCode} · {s.leadTimeDays}d · {t('procurement.rfq.validUntil')} {day(s.validUntil)}{s.expired ? ` (${t('procurement.rfq.expired')})` : ''}</div>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {cmp.lines.map((l) => (
                <tr key={l.lineId}>
                  <td className="py-2">{l.productName}</td>
                  <td className="py-2 text-right">{l.quantity}</td>
                  {cmp.suppliers.map((s) => {
                    const p = l.prices.find((x) => x.supplierId === s.supplierId)
                    return (
                      <td key={s.supplierId} className={`py-2 text-right tabular-nums ${p?.expired ? 'text-ink-muted' : ''}`}>
                        {p?.isLowest ? <Badge variant="success">{money(p.unitPrice)}</Badge> : p ? money(p.unitPrice) : '—'}
                      </td>
                    )
                  })}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t border-line-strong font-semibold">
                <td className="pt-3" colSpan={2}>{t('procurement.rfq.total')}</td>
                {cmp.suppliers.map((s) => <td key={s.supplierId} className="pt-3 text-right tabular-nums">{money(s.total)}{s.isWinner ? ` ★` : ''}</td>)}
              </tr>
              {canManage && rfq.status === 'Sent' && (
                <tr>
                  <td colSpan={2} />
                  {cmp.suppliers.map((s) => (
                    <td key={s.supplierId} className="pt-2 text-right">
                      <Button
                        size="sm"
                        aria-label={`${t('procurement.rfq.award')}: ${s.supplierName}`}
                        disabled={s.expired}
                        onClick={() => setStep({ kind: 'award', supplierId: s.supplierId, supplierName: s.supplierName })}
                      >
                        {t('procurement.rfq.award')}
                      </Button>
                    </td>
                  ))}
                </tr>
              )}
            </tfoot>
          </table>
        </div>
      )}

      {quoting && <QuotationModal rfq={rfq} supplierId={quoting} onClose={() => setQuoting(null)} />}

      <ConfirmDialog
        open={step !== null}
        title={stepTitle}
        message={stepMessage}
        confirmLabel={stepConfirm}
        danger={step?.kind === 'cancel'}
        onConfirm={confirmStep}
        onCancel={() => setStep(null)}
      />
    </div>
  )
}
