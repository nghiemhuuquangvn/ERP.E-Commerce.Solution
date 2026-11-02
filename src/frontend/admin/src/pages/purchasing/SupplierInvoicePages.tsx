import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { procurementApi } from '@/api/purchasing/procurement.api'
import type { SupplierInvoiceSummary } from '@/api/purchasing/procurement.api'
import { purchaseOrdersApi } from '@/api/purchaseOrders.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Badge, Button, EmptyState, FormField, Input, Modal, Select, Spinner, Switch, Textarea, useToast } from '@/components/ui'
import type { BadgeVariant } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const money = (v: number) => new Intl.NumberFormat().format(v)
const day = (v: string | null | undefined) => (v ? new Date(v).toLocaleDateString() : '—')
const inputDate = (offsetDays = 0) => new Date(Date.now() + offsetDays * 86_400_000).toISOString().slice(0, 10)

// ─── Record an invoice ──────────────────────────────────────────────────────

function RecordInvoiceModal({ presetOrderId, onClose }: { presetOrderId?: string; onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [orderId, setOrderId] = useState(presetOrderId ?? '')
  const [number, setNumber] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(inputDate())
  const [dueDate, setDueDate] = useState('')
  const [tax, setTax] = useState('0')
  const [lines, setLines] = useState<{ productCode: string; quantity: string; unitPrice: string }[]>([])
  const [errors, setErrors] = useState<Partial<Record<'order' | 'number' | 'lines', string>>>({})

  const { data: orders } = useQuery({ queryKey: ['po', 'orders', 'invoiceable'], queryFn: () => purchaseOrdersApi.list({ pageNumber: 1, pageSize: 100 }) })
  const invoiceable = (orders?.items ?? []).filter((o) => /approved|ordered|received/i.test(String(o.status).replace(/_/g, '')))
  const { data: proc } = useQuery({ queryKey: ['po', 'procurement', orderId], queryFn: () => procurementApi.orderProcurement(orderId), enabled: !!orderId })

  const prefill = () => {
    if (!proc) return
    setLines(proc.lines.filter((l) => l.billable > 0).map((l) => ({ productCode: l.productCode, quantity: String(l.billable), unitPrice: String(l.unitPrice) })))
  }

  const record = useMutation({
    mutationFn: () =>
      procurementApi.recordInvoice({
        purchaseOrderId: orderId, invoiceNumber: number, invoiceDate, dueDate: dueDate || null, currencyCode: null, taxAmount: Number(tax) || 0,
        lines: lines.filter((l) => Number(l.quantity) > 0).map((l) => ({ productCode: l.productCode, quantity: Number(l.quantity), unitPrice: Number(l.unitPrice) })),
      }),
    onSuccess: (inv) => {
      queryClient.invalidateQueries({ queryKey: ['po', 'invoices'] })
      toast.success(t('common.saved'))
      navigate(`/purchasing/supplier-invoices/${inv.id}`)
    },
  })

  const net = lines.reduce((s, l) => s + (Number(l.quantity) || 0) * (Number(l.unitPrice) || 0), 0)
  const set = (i: number, key: string, value: string) => setLines(lines.map((l, j) => (j === i ? { ...l, [key]: value } : l)))

  const submit = () => {
    const found: typeof errors = {}
    if (!orderId) found.order = t('common.required')
    if (!number.trim()) found.number = t('common.required')
    if (!lines.some((l) => Number(l.quantity) > 0)) found.lines = t('procurement.invoice.linesRequired')
    setErrors(found)
    if (Object.keys(found).length === 0) record.mutate()
  }

  return (
    <Modal
      open
      size="lg"
      title={t('procurement.invoice.recordTitle')}
      onClose={onClose}
      closeOnEsc={!record.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="supplier-invoice-form" loading={record.isPending}>{t('procurement.invoice.recordAndMatch')}</Button>
        </>
      }
    >
      <form id="supplier-invoice-form" noValidate onSubmit={(e) => { e.preventDefault(); submit() }} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <FormField className="col-span-2" label={t('procurement.invoice.order')} required error={errors.order} hint={invoiceable.length === 0 ? t('procurement.invoice.noOrders') : undefined}>
            <Select value={orderId} onChange={(e) => { setOrderId(e.target.value); setLines([]) }}>
              <option value="">{t('common.select')}</option>
              {invoiceable.map((o) => <option key={o.id} value={o.id}>{o.poNumber} — {o.supplier?.name ?? ''} ({o.status})</option>)}
            </Select>
          </FormField>
          <FormField label={t('procurement.invoice.number')} required error={errors.number}>
            <Input value={number} onChange={(e) => setNumber(e.target.value)} />
          </FormField>
          <FormField label={t('procurement.invoice.date')}>
            <Input type="date" value={invoiceDate} onChange={(e) => setInvoiceDate(e.target.value)} />
          </FormField>
          <FormField label={t('procurement.invoice.dueDate')} hint={t('procurement.invoice.dueHint')}>
            <Input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
          </FormField>
          <FormField label={t('procurement.invoice.tax')}>
            <Input type="number" min={0} value={tax} onChange={(e) => setTax(e.target.value)} />
          </FormField>
        </div>

        {proc && (
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold text-ink-secondary">{t('procurement.invoice.lines')}</p>
              <Button variant="link" className="text-xs" onClick={prefill}>{t('procurement.invoice.prefill')}</Button>
            </div>
            <table className="min-w-full text-xs text-ink-secondary">
              <thead>
                <tr className="text-left uppercase">
                  <th>{t('procurement.productCode')}</th>
                  <th className="text-right">{t('procurement.ordered')}</th>
                  <th className="text-right">{t('procurement.received')}</th>
                  <th className="text-right">{t('procurement.returned')}</th>
                  <th className="text-right">{t('procurement.invoiced')}</th>
                  <th className="text-right">{t('procurement.invoice.billable')}</th>
                  <th className="text-right">{t('procurement.orderPrice')}</th>
                </tr>
              </thead>
              <tbody>
                {proc.lines.map((l) => (
                  <tr key={l.productCode}>
                    <td>{l.productCode}</td>
                    <td className="text-right tabular-nums">{l.ordered}</td>
                    <td className="text-right tabular-nums">{l.received}</td>
                    <td className="text-right tabular-nums">{l.returned}</td>
                    <td className="text-right tabular-nums">{l.invoiced}</td>
                    <td className="text-right font-semibold tabular-nums">{l.billable}</td>
                    <td className="text-right tabular-nums">{money(l.unitPrice)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {lines.map((l, i) => (
              <div key={i} className="grid grid-cols-12 items-center gap-2 text-sm">
                <span className="col-span-4 font-mono text-xs">{l.productCode}</span>
                <Input type="number" min={0} className="col-span-4" aria-label={t('procurement.invoice.lineQty', { code: l.productCode })} value={l.quantity} onChange={(e) => set(i, 'quantity', e.target.value)} />
                <Input type="number" min={0} step="0.01" className="col-span-4" aria-label={t('procurement.invoice.linePrice', { code: l.productCode })} value={l.unitPrice} onChange={(e) => set(i, 'unitPrice', e.target.value)} />
              </div>
            ))}
            {errors.lines && <p role="alert" className="text-xs text-danger-soft-text">{errors.lines}</p>}
            {lines.length > 0 && (
              <p className="text-right text-sm text-ink-secondary" aria-live="polite">
                {t('procurement.invoice.net')}: <b>{money(net)}</b> + {t('procurement.invoice.tax')} {money(Number(tax) || 0)} = <b>{money(net + (Number(tax) || 0))}</b>
              </p>
            )}
          </div>
        )}
        {!proc && errors.lines && <p role="alert" className="text-xs text-danger-soft-text">{errors.lines}</p>}

        {record.isError && <Alert variant="danger">{serverMessage(record.error, t('common.error'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── List ───────────────────────────────────────────────────────────────────

export function SupplierInvoiceListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const [params] = useSearchParams()
  const [status, setStatus] = useState('')
  const [recording, setRecording] = useState(false)
  const purchaseOrderId = params.get('po') ?? undefined
  const canManage = hasPermission('supplier_invoice:manage')
  const { data = [], isLoading } = useQuery({ queryKey: ['po', 'invoices', status, purchaseOrderId], queryFn: () => procurementApi.invoices({ status: status || undefined, purchaseOrderId }) })

  const columns = useMemo(
    () => [
      { key: 'invoiceNumber', label: t('procurement.invoice.number'), className: 'font-mono text-xs', render: (i: SupplierInvoiceSummary) => <Link className="text-brand-action hover:underline" to={`/purchasing/supplier-invoices/${i.id}`}>{i.invoiceNumber}</Link> },
      { key: 'supplierName', label: t('procurement.supplier') },
      { key: 'poNumber', label: t('procurement.invoice.order') },
      { key: 'invoiceDate', label: t('procurement.invoice.date'), render: (i: SupplierInvoiceSummary) => day(i.invoiceDate) },
      { key: 'dueDate', label: t('procurement.invoice.dueDate'), render: (i: SupplierInvoiceSummary) => day(i.dueDate) },
      { key: 'totalAmount', label: t('procurement.invoice.total'), align: 'right' as const, render: (i: SupplierInvoiceSummary) => `${money(i.totalAmount)} ${i.currencyCode}` },
      { key: 'status', label: t('common.status'), render: (i: SupplierInvoiceSummary) => <StatusBadge status={i.status} /> },
      { key: 'holdReasons', label: t('procurement.invoice.holdReasons'), render: (i: SupplierInvoiceSummary) => (i.holdReasons ? <span className="text-xs text-warning-soft-text">{i.holdReasons}</span> : null) },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t('procurement.invoice.listTitle')}</h1>
          <p className="text-sm text-ink-secondary">{t('procurement.invoice.hint')}</p>
        </div>
        {canManage && <Button onClick={() => setRecording(true)}>{t('procurement.invoice.record')}</Button>}
      </div>
      <Select aria-label={t('common.status')} className="max-w-xs" value={status} onChange={(e) => setStatus(e.target.value)}>
        <option value="">{t('procurement.allStatuses')}</option>
        {['Recorded', 'Matched', 'OnHold', 'Approved', 'Rejected'].map((s) => <option key={s} value={s}>{s}</option>)}
      </Select>
      <DataTable
        columns={columns}
        data={data}
        isLoading={isLoading}
        emptyState={
          status ? (
            <EmptyState variant="no-results" title={t('common.noResults')} action={<Button variant="outline" size="sm" onClick={() => setStatus('')}>{t('common.clear')}</Button>} />
          ) : (
            <EmptyState title={t('procurement.invoice.empty')} action={canManage ? <Button onClick={() => setRecording(true)}>{t('procurement.invoice.record')}</Button> : undefined} />
          )
        }
      />
      {recording && <RecordInvoiceModal presetOrderId={purchaseOrderId} onClose={() => setRecording(false)} />}
    </div>
  )
}

// ─── Detail ─────────────────────────────────────────────────────────────────

const VERDICT_VARIANT: Record<string, BadgeVariant> = {
  Matched: 'success',
  QuantityVariance: 'warning',
  PriceVariance: 'warning',
  QuantityAndPriceVariance: 'danger',
  NotOnOrder: 'danger',
}

type Step = 'approve' | 'reject'

export function SupplierInvoiceDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [reason, setReason] = useState('')
  const [step, setStep] = useState<Step | null>(null)
  const { data: inv } = useQuery({ queryKey: ['po', 'invoice', id], queryFn: () => procurementApi.invoice(id!), enabled: !!id })

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['po', 'invoice', id] })
    queryClient.invalidateQueries({ queryKey: ['po', 'invoices'] })
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
  const match = useMutation({ mutationFn: () => procurementApi.matchInvoice(id!), ...after })
  const approve = useMutation({ mutationFn: () => procurementApi.approveInvoice(id!, reason.trim() || null), ...after })
  const reject = useMutation({ mutationFn: () => procurementApi.rejectInvoice(id!, reason.trim()), ...after })

  if (!inv) return <Spinner label={t('common.loading')} />
  const onHold = inv.status === 'OnHold'
  const open = inv.status === 'Matched' || onHold || inv.status === 'Recorded'
  const canApprove = hasPermission('supplier_invoice:approve')
  const canVariance = hasPermission('supplier_invoice:approve_variance')
  const canManage = hasPermission('supplier_invoice:manage')

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-3">
        <Link to="/purchasing/supplier-invoices" className="text-sm text-ink-secondary hover:text-ink">{t('procurement.invoice.back')}</Link>
        <h1 className="text-xl font-semibold">{inv.invoiceNumber}</h1>
        <StatusBadge status={inv.status} />
        <span className="text-ink-secondary">{inv.supplierName} · <Link className="text-brand-action hover:underline" to={`/orders/purchase/${inv.purchaseOrderId}`}>{inv.poNumber}</Link></span>
      </div>

      <div className="grid gap-5 md:grid-cols-2">
        <div className="card p-5">
          <dl className="space-y-1 text-sm">
            <div className="flex justify-between"><dt className="text-ink-secondary">{t('procurement.invoice.date')}</dt><dd>{day(inv.invoiceDate)}</dd></div>
            <div className="flex justify-between"><dt className="text-ink-secondary">{t('procurement.invoice.dueDate')}</dt><dd>{day(inv.dueDate)}</dd></div>
            <div className="flex justify-between"><dt className="text-ink-secondary">{t('procurement.invoice.net')}</dt><dd>{money(inv.netAmount)} {inv.currencyCode}</dd></div>
            <div className="flex justify-between"><dt className="text-ink-secondary">{t('procurement.invoice.tax')}</dt><dd>{money(inv.taxAmount)}</dd></div>
            <div className="flex justify-between font-semibold"><dt>{t('procurement.invoice.total')}</dt><dd>{money(inv.totalAmount)}</dd></div>
            <div className="flex justify-between text-ink-secondary"><dt>{t('procurement.invoice.receivedValue')}</dt><dd>{money(inv.receivedValue)}</dd></div>
          </dl>
        </div>
        <div className="card space-y-3 p-5 text-sm">
          {inv.holdReasons && <Alert variant="warning" title={t('procurement.invoice.onHold')}>{inv.holdReasons}</Alert>}
          {inv.approvalReason && <p className="text-ink-secondary">{t('procurement.invoice.approvedWith')}: {inv.approvalReason}</p>}
          {inv.status === 'Approved' && <p className="text-success-soft-text">{t('procurement.invoice.approvedBy', { who: inv.approvedBy ?? '', when: day(inv.approvedAt) })}</p>}
          {inv.rejectionReason && <p className="text-danger-soft-text">{t('procurement.invoice.rejectedFor')}: {inv.rejectionReason}</p>}
          {open && (
            <div className="space-y-3">
              <FormField label={t('procurement.invoice.reasonLabel')} hint={onHold ? t('procurement.invoice.reasonRequired') : t('procurement.invoice.reasonOptional')}>
                <Textarea rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
              </FormField>
              <div className="flex flex-wrap gap-2">
                {canManage && <Button variant="outline" loading={match.isPending} onClick={() => match.mutate()}>{t('procurement.invoice.matchAgain')}</Button>}
                {canApprove && inv.status === 'Matched' && <Button onClick={() => setStep('approve')}>{t('procurement.invoice.approve')}</Button>}
                {canApprove && onHold && canVariance && <Button disabled={!reason.trim()} onClick={() => setStep('approve')}>{t('procurement.invoice.approveVariance')}</Button>}
                {canManage && <Button variant="danger" disabled={!reason.trim()} onClick={() => setStep('reject')}>{t('procurement.invoice.reject')}</Button>}
              </div>
              {onHold && !canVariance && <p className="text-xs text-ink-secondary">{t('procurement.invoice.needVariancePermission')}</p>}
            </div>
          )}
        </div>
      </div>

      <div className="card overflow-x-auto p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-ink-secondary">{t('procurement.invoice.matchTitle')}</h2>
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-line text-left text-xs uppercase text-ink-secondary">
              <th className="pb-2">{t('procurement.productCode')}</th>
              <th className="pb-2 text-right">{t('procurement.ordered')}</th>
              <th className="pb-2 text-right">{t('procurement.received')}</th>
              <th className="pb-2 text-right">{t('procurement.returned')}</th>
              <th className="pb-2 text-right">{t('procurement.invoice.before')}</th>
              <th className="pb-2 text-right">{t('procurement.invoice.thisInvoice')}</th>
              <th className="pb-2 text-right">{t('procurement.orderPrice')}</th>
              <th className="pb-2 text-right">{t('procurement.invoice.invoicedPrice')}</th>
              <th className="pb-2 text-right">{t('procurement.invoice.priceVar')}</th>
              <th className="pb-2">{t('procurement.invoice.verdict')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {inv.matchLines.map((m) => (
              <tr key={m.productCode}>
                <td className="py-2 font-mono text-xs">{m.productCode}</td>
                <td className="py-2 text-right tabular-nums">{m.ordered}</td>
                <td className="py-2 text-right tabular-nums">{m.received}</td>
                <td className="py-2 text-right tabular-nums">{m.returned}</td>
                <td className="py-2 text-right tabular-nums">{m.previouslyInvoiced}</td>
                <td className="py-2 text-right font-semibold tabular-nums">{m.invoiced}</td>
                <td className="py-2 text-right tabular-nums">{money(m.orderPrice)}</td>
                <td className="py-2 text-right tabular-nums">{money(m.invoicedPrice)}</td>
                <td className="py-2 text-right tabular-nums">{m.priceVariancePercent > 0 ? '+' : ''}{m.priceVariancePercent}%</td>
                <td className="py-2"><Badge variant={VERDICT_VARIANT[m.verdict] ?? 'muted'}>{t(`procurement.verdict.${m.verdict}`, { defaultValue: m.verdict })}</Badge></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ConfirmDialog
        open={step !== null}
        title={step === 'reject' ? t('procurement.invoice.rejectTitle') : t('procurement.invoice.approveTitle')}
        message={step === 'reject' ? t('procurement.invoice.rejectConfirm', { number: inv.invoiceNumber }) : t('procurement.invoice.approveConfirm', { number: inv.invoiceNumber })}
        confirmLabel={step === 'reject' ? t('procurement.invoice.reject') : onHold ? t('procurement.invoice.approveVariance') : t('procurement.invoice.approve')}
        danger={step === 'reject'}
        onConfirm={() => (step === 'reject' ? reject.mutate() : approve.mutate())}
        onCancel={() => setStep(null)}
      />
    </div>
  )
}

// ─── Settings ───────────────────────────────────────────────────────────────

export function PurchasingSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { data } = useQuery({ queryKey: ['po', 'settings'], queryFn: procurementApi.settings })
  const [form, setForm] = useState<{ q: string; p: string; auto: boolean } | null>(null)
  const [errors, setErrors] = useState<Partial<Record<'q' | 'p', string>>>({})
  const current = form ?? (data ? { q: String(data.quantityTolerancePercent), p: String(data.priceTolerancePercent), auto: data.autoApproveMatched } : null)
  const save = useMutation({
    mutationFn: () => procurementApi.saveSettings({ quantityTolerancePercent: Number(current!.q), priceTolerancePercent: Number(current!.p), autoApproveMatched: current!.auto }),
    onSuccess: () => {
      setForm(null)
      queryClient.invalidateQueries({ queryKey: ['po', 'settings'] })
      toast.success(t('common.saved'))
    },
  })
  if (!current) return <Spinner label={t('common.loading')} />
  const canManage = hasPermission('purchasing_settings:manage')

  const submit = () => {
    const found: typeof errors = {}
    const pct = (v: string) => v.trim() !== '' && Number(v) >= 0 && Number(v) <= 100
    if (!pct(current.q)) found.q = t('procurement.settings.percent')
    if (!pct(current.p)) found.p = t('procurement.settings.percent')
    setErrors(found)
    if (Object.keys(found).length === 0) save.mutate()
  }

  return (
    <div className="max-w-xl space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{t('procurement.settings.title')}</h1>
        <p className="text-sm text-ink-secondary">{t('procurement.settings.hint')}</p>
      </div>
      <form noValidate onSubmit={(e) => { e.preventDefault(); submit() }} className="card space-y-4 p-5">
        <FormField label={t('procurement.settings.quantityTolerance')} hint={t('procurement.settings.quantityHint')} error={errors.q}>
          <Input type="number" step="0.1" min={0} max={100} disabled={!canManage} value={current.q} onChange={(e) => setForm({ ...current, q: e.target.value })} />
        </FormField>
        <FormField label={t('procurement.settings.priceTolerance')} hint={t('procurement.settings.priceHint')} error={errors.p}>
          <Input type="number" step="0.1" min={0} max={100} disabled={!canManage} value={current.p} onChange={(e) => setForm({ ...current, p: e.target.value })} />
        </FormField>
        <div className="space-y-1">
          <div className="flex items-center gap-3">
            <Switch label={t('procurement.settings.autoApprove')} checked={current.auto} disabled={!canManage} onChange={(auto) => setForm({ ...current, auto })} />
            <span className="text-sm text-ink">{t('procurement.settings.autoApprove')}</span>
          </div>
          <p className="text-xs text-ink-secondary">{t('procurement.settings.autoApproveHint')}</p>
        </div>
        {save.isError && <Alert variant="danger">{serverMessage(save.error, t('common.error'))}</Alert>}
        {canManage && <div className="flex justify-end"><Button type="submit" loading={save.isPending}>{t('common.save')}</Button></div>}
      </form>
    </div>
  )
}
