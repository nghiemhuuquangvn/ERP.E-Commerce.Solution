import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { procurementApi, serverMessage } from '@/api/purchasing/procurement.api'
import type { SupplierInvoiceSummary } from '@/api/purchasing/procurement.api'
import { purchaseOrdersApi } from '@/api/purchaseOrders.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

const money = (v: number) => new Intl.NumberFormat().format(v)
const day = (v: string | null | undefined) => (v ? new Date(v).toLocaleDateString() : '—')
const inputDate = (offsetDays = 0) => new Date(Date.now() + offsetDays * 86_400_000).toISOString().slice(0, 10)

// ─── Record an invoice ──────────────────────────────────────────────────────

function RecordInvoiceModal({ presetOrderId, onClose }: { presetOrderId?: string; onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [orderId, setOrderId] = useState(presetOrderId ?? '')
  const [number, setNumber] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(inputDate())
  const [dueDate, setDueDate] = useState('')
  const [tax, setTax] = useState('0')
  const [lines, setLines] = useState<{ productCode: string; quantity: string; unitPrice: string }[]>([])

  const { data: orders } = useQuery({ queryKey: ['po', 'orders', 'invoiceable'], queryFn: () => purchaseOrdersApi.list({ pageNumber: 1, pageSize: 100 }) })
  const invoiceable = (orders?.items ?? []).filter((o) => /approved|ordered|received/i.test(String(o.status).replace(/_/g, '')))
  const { data: proc } = useQuery({ queryKey: ['po', 'procurement', orderId], queryFn: () => procurementApi.orderProcurement(orderId), enabled: !!orderId })

  // pick an order: prefill the lines with what can still be billed, at the order price
  const choose = async (id: string) => {
    setOrderId(id)
    setLines([])
  }
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
      navigate(`/purchasing/supplier-invoices/${inv.id}`)
    },
  })

  const net = lines.reduce((s, l) => s + (Number(l.quantity) || 0) * (Number(l.unitPrice) || 0), 0)
  const set = (i: number, key: string, value: string) => setLines(lines.map((l, j) => (j === i ? { ...l, [key]: value } : l)))

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[92vh] w-full max-w-3xl overflow-y-auto p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('procurement.invoice.recordTitle')}</h3>
        <div className="grid grid-cols-2 gap-3">
          <div className="col-span-2">
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.invoice.order')}</label>
            <select className="input" value={orderId} onChange={(e) => choose(e.target.value)}>
              <option value="">{t('common.select')}</option>
              {invoiceable.map((o) => <option key={o.id} value={o.id}>{o.poNumber} — {o.supplier?.name ?? ''} ({o.status})</option>)}
            </select>
          </div>
          <div><label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.invoice.number')}</label><input className="input" value={number} onChange={(e) => setNumber(e.target.value)} /></div>
          <div><label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.invoice.date')}</label><input type="date" className="input" value={invoiceDate} onChange={(e) => setInvoiceDate(e.target.value)} /></div>
          <div><label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.invoice.dueDate')}</label><input type="date" className="input" value={dueDate} onChange={(e) => setDueDate(e.target.value)} /><p className="mt-1 text-xs text-gray-400">{t('procurement.invoice.dueHint')}</p></div>
          <div><label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.invoice.tax')}</label><input type="number" className="input" value={tax} onChange={(e) => setTax(e.target.value)} /></div>
        </div>

        {proc && (
          <div className="mt-4">
            <div className="mb-1 flex items-center justify-between">
              <p className="text-sm font-semibold text-gray-700">{t('procurement.invoice.lines')}</p>
              <button type="button" className="text-xs text-blue-600 hover:underline" onClick={prefill}>{t('procurement.invoice.prefill')}</button>
            </div>
            <table className="mb-2 min-w-full text-xs text-gray-500">
              <thead><tr className="text-left uppercase"><th>{t('procurement.productCode')}</th><th className="text-right">{t('procurement.ordered')}</th><th className="text-right">{t('procurement.received')}</th><th className="text-right">{t('procurement.returned')}</th><th className="text-right">{t('procurement.invoiced')}</th><th className="text-right">{t('procurement.invoice.billable')}</th><th className="text-right">{t('procurement.orderPrice')}</th></tr></thead>
              <tbody>{proc.lines.map((l) => <tr key={l.productCode}><td>{l.productCode}</td><td className="text-right">{l.ordered}</td><td className="text-right">{l.received}</td><td className="text-right">{l.returned}</td><td className="text-right">{l.invoiced}</td><td className="text-right font-semibold">{l.billable}</td><td className="text-right">{money(l.unitPrice)}</td></tr>)}</tbody>
            </table>
            {lines.map((l, i) => (
              <div key={i} className="mb-2 grid grid-cols-12 items-center gap-2 text-sm">
                <span className="col-span-4 font-mono text-xs">{l.productCode}</span>
                <input type="number" className="input col-span-4" value={l.quantity} onChange={(e) => set(i, 'quantity', e.target.value)} />
                <input type="number" step="0.01" className="input col-span-4" value={l.unitPrice} onChange={(e) => set(i, 'unitPrice', e.target.value)} />
              </div>
            ))}
            {lines.length > 0 && <p className="text-right text-sm text-gray-600">{t('procurement.invoice.net')}: <b>{money(net)}</b> + {t('procurement.invoice.tax')} {money(Number(tax) || 0)} = <b>{money(net + (Number(tax) || 0))}</b></p>}
          </div>
        )}

        {record.isError && <p className="mt-3 text-xs text-red-600">{serverMessage(record.error, t('common.error'))}</p>}
        <div className="mt-4 flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={!orderId || !number.trim() || lines.length === 0 || record.isPending} onClick={() => record.mutate()}>{t('procurement.invoice.recordAndMatch')}</button>
        </div>
      </div>
    </div>
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
  const { data = [], isLoading } = useQuery({ queryKey: ['po', 'invoices', status, purchaseOrderId], queryFn: () => procurementApi.invoices({ status: status || undefined, purchaseOrderId }) })

  const columns = useMemo(
    () => [
      { key: 'invoiceNumber', label: t('procurement.invoice.number'), className: 'font-mono text-xs', render: (i: SupplierInvoiceSummary) => <Link className="text-blue-600 hover:underline" to={`/purchasing/supplier-invoices/${i.id}`}>{i.invoiceNumber}</Link> },
      { key: 'supplierName', label: t('procurement.supplier') },
      { key: 'poNumber', label: t('procurement.invoice.order') },
      { key: 'invoiceDate', label: t('procurement.invoice.date'), render: (i: SupplierInvoiceSummary) => day(i.invoiceDate) },
      { key: 'dueDate', label: t('procurement.invoice.dueDate'), render: (i: SupplierInvoiceSummary) => day(i.dueDate) },
      { key: 'totalAmount', label: t('procurement.invoice.total'), render: (i: SupplierInvoiceSummary) => `${money(i.totalAmount)} ${i.currencyCode}` },
      { key: 'status', label: t('common.status'), render: (i: SupplierInvoiceSummary) => <StatusBadge status={i.status} /> },
      { key: 'holdReasons', label: t('procurement.invoice.holdReasons'), render: (i: SupplierInvoiceSummary) => (i.holdReasons ? <span className="text-xs text-amber-700">{i.holdReasons}</span> : null) },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t('procurement.invoice.listTitle')}</h1>
          <p className="text-sm text-gray-500">{t('procurement.invoice.hint')}</p>
        </div>
        {hasPermission('supplier_invoice:manage') && <button className="btn-primary" onClick={() => setRecording(true)}>{t('procurement.invoice.record')}</button>}
      </div>
      <select className="input max-w-xs" value={status} onChange={(e) => setStatus(e.target.value)}>
        <option value="">{t('procurement.allStatuses')}</option>
        {['Recorded', 'Matched', 'OnHold', 'Approved', 'Rejected'].map((s) => <option key={s} value={s}>{s}</option>)}
      </select>
      <DataTable columns={columns} data={data} isLoading={isLoading} />
      {recording && <RecordInvoiceModal presetOrderId={purchaseOrderId} onClose={() => setRecording(false)} />}
    </div>
  )
}

// ─── Detail ─────────────────────────────────────────────────────────────────

const VERDICT_STYLE: Record<string, string> = {
  Matched: 'bg-green-100 text-green-700',
  QuantityVariance: 'bg-amber-100 text-amber-700',
  PriceVariance: 'bg-amber-100 text-amber-700',
  QuantityAndPriceVariance: 'bg-red-100 text-red-700',
  NotOnOrder: 'bg-red-100 text-red-700',
}

export function SupplierInvoiceDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [reason, setReason] = useState('')
  const { data: inv } = useQuery({ queryKey: ['po', 'invoice', id], queryFn: () => procurementApi.invoice(id!), enabled: !!id })

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['po', 'invoice', id] })
    queryClient.invalidateQueries({ queryKey: ['po', 'invoices'] })
  }
  const match = useMutation({ mutationFn: () => procurementApi.matchInvoice(id!), onSuccess: refresh })
  const approve = useMutation({ mutationFn: () => procurementApi.approveInvoice(id!, reason.trim() || null), onSuccess: refresh })
  const reject = useMutation({ mutationFn: () => procurementApi.rejectInvoice(id!, reason.trim()), onSuccess: refresh })

  if (!inv) return <div className="text-gray-400">{t('common.loading')}</div>
  const onHold = inv.status === 'OnHold'
  const open = inv.status === 'Matched' || onHold || inv.status === 'Recorded'
  const canApprove = hasPermission('supplier_invoice:approve')
  const canVariance = hasPermission('supplier_invoice:approve_variance')
  const error = [match, approve, reject].map((m) => (m.isError ? serverMessage(m.error, '') : '')).find(Boolean)

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-3">
        <Link to="/purchasing/supplier-invoices" className="text-sm text-gray-500 hover:text-gray-700">{t('procurement.invoice.back')}</Link>
        <h1 className="text-xl font-semibold">{inv.invoiceNumber}</h1>
        <StatusBadge status={inv.status} />
        <span className="text-gray-500">{inv.supplierName} · <Link className="text-blue-600 hover:underline" to={`/orders/purchase/${inv.purchaseOrderId}`}>{inv.poNumber}</Link></span>
      </div>

      <div className="grid gap-5 md:grid-cols-2">
        <div className="card p-5">
          <dl className="space-y-1 text-sm">
            <div className="flex justify-between"><dt className="text-gray-500">{t('procurement.invoice.date')}</dt><dd>{day(inv.invoiceDate)}</dd></div>
            <div className="flex justify-between"><dt className="text-gray-500">{t('procurement.invoice.dueDate')}</dt><dd>{day(inv.dueDate)}</dd></div>
            <div className="flex justify-between"><dt className="text-gray-500">{t('procurement.invoice.net')}</dt><dd>{money(inv.netAmount)} {inv.currencyCode}</dd></div>
            <div className="flex justify-between"><dt className="text-gray-500">{t('procurement.invoice.tax')}</dt><dd>{money(inv.taxAmount)}</dd></div>
            <div className="flex justify-between font-semibold"><dt>{t('procurement.invoice.total')}</dt><dd>{money(inv.totalAmount)}</dd></div>
            <div className="flex justify-between text-gray-500"><dt>{t('procurement.invoice.receivedValue')}</dt><dd>{money(inv.receivedValue)}</dd></div>
          </dl>
        </div>
        <div className="card p-5 text-sm">
          {inv.holdReasons && <div className="mb-3 rounded-md bg-amber-50 p-3 text-amber-800"><b>{t('procurement.invoice.onHold')}</b><br />{inv.holdReasons}</div>}
          {inv.approvalReason && <p className="mb-2 text-gray-600">{t('procurement.invoice.approvedWith')}: {inv.approvalReason}</p>}
          {inv.status === 'Approved' && <p className="text-green-700">{t('procurement.invoice.approvedBy', { who: inv.approvedBy ?? '', when: day(inv.approvedAt) })}</p>}
          {inv.rejectionReason && <p className="text-red-700">{t('procurement.invoice.rejectedFor')}: {inv.rejectionReason}</p>}
          {open && (
            <div className="space-y-2">
              <input className="input" placeholder={onHold ? t('procurement.invoice.reasonRequired') : t('procurement.invoice.reasonOptional')} value={reason} onChange={(e) => setReason(e.target.value)} />
              <div className="flex flex-wrap gap-2">
                {hasPermission('supplier_invoice:manage') && <button className="btn-outline" disabled={match.isPending} onClick={() => match.mutate()}>{t('procurement.invoice.matchAgain')}</button>}
                {canApprove && inv.status === 'Matched' && <button className="btn-primary" disabled={approve.isPending} onClick={() => approve.mutate()}>{t('procurement.invoice.approve')}</button>}
                {canApprove && onHold && canVariance && <button className="btn-primary" disabled={!reason.trim() || approve.isPending} onClick={() => approve.mutate()}>{t('procurement.invoice.approveVariance')}</button>}
                {hasPermission('supplier_invoice:manage') && <button className="btn-danger" disabled={!reason.trim() || reject.isPending} onClick={() => reject.mutate()}>{t('procurement.invoice.reject')}</button>}
              </div>
              {onHold && !canVariance && <p className="text-xs text-gray-500">{t('procurement.invoice.needVariancePermission')}</p>}
            </div>
          )}
          {error && <p className="mt-2 text-xs text-red-600">{error}</p>}
        </div>
      </div>

      <div className="card overflow-x-auto p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">{t('procurement.invoice.matchTitle')}</h2>
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-gray-100 text-left text-xs uppercase text-gray-400">
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
          <tbody className="divide-y divide-gray-50">
            {inv.matchLines.map((m) => (
              <tr key={m.productCode}>
                <td className="py-2 font-mono text-xs">{m.productCode}</td>
                <td className="py-2 text-right">{m.ordered}</td>
                <td className="py-2 text-right">{m.received}</td>
                <td className="py-2 text-right">{m.returned}</td>
                <td className="py-2 text-right">{m.previouslyInvoiced}</td>
                <td className="py-2 text-right font-semibold">{m.invoiced}</td>
                <td className="py-2 text-right">{money(m.orderPrice)}</td>
                <td className="py-2 text-right">{money(m.invoicedPrice)}</td>
                <td className="py-2 text-right">{m.priceVariancePercent > 0 ? '+' : ''}{m.priceVariancePercent}%</td>
                <td className="py-2"><span className={`rounded-full px-2 py-0.5 text-xs font-medium ${VERDICT_STYLE[m.verdict] ?? 'bg-gray-100 text-gray-600'}`}>{t(`procurement.verdict.${m.verdict}`, { defaultValue: m.verdict })}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ─── Settings ───────────────────────────────────────────────────────────────

export function PurchasingSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const { data } = useQuery({ queryKey: ['po', 'settings'], queryFn: procurementApi.settings })
  const [form, setForm] = useState<{ q: string; p: string; auto: boolean } | null>(null)
  const current = form ?? (data ? { q: String(data.quantityTolerancePercent), p: String(data.priceTolerancePercent), auto: data.autoApproveMatched } : null)
  const save = useMutation({
    mutationFn: () => procurementApi.saveSettings({ quantityTolerancePercent: Number(current!.q), priceTolerancePercent: Number(current!.p), autoApproveMatched: current!.auto }),
    onSuccess: () => { setForm(null); queryClient.invalidateQueries({ queryKey: ['po', 'settings'] }) },
  })
  if (!current) return <div className="text-gray-400">{t('common.loading')}</div>
  const canManage = hasPermission('purchasing_settings:manage')

  return (
    <div className="max-w-xl space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{t('procurement.settings.title')}</h1>
        <p className="text-sm text-gray-500">{t('procurement.settings.hint')}</p>
      </div>
      <div className="card space-y-4 p-5">
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.settings.quantityTolerance')}</label>
          <input type="number" step="0.1" className="input" disabled={!canManage} value={current.q} onChange={(e) => setForm({ ...current, q: e.target.value })} />
          <p className="mt-1 text-xs text-gray-400">{t('procurement.settings.quantityHint')}</p>
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.settings.priceTolerance')}</label>
          <input type="number" step="0.1" className="input" disabled={!canManage} value={current.p} onChange={(e) => setForm({ ...current, p: e.target.value })} />
          <p className="mt-1 text-xs text-gray-400">{t('procurement.settings.priceHint')}</p>
        </div>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" disabled={!canManage} checked={current.auto} onChange={(e) => setForm({ ...current, auto: e.target.checked })} />
          {t('procurement.settings.autoApprove')}
        </label>
        {save.isError && <p className="text-xs text-red-600">{serverMessage(save.error, t('common.error'))}</p>}
        {canManage && <div className="flex justify-end"><button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>{t('common.save')}</button></div>}
      </div>
    </div>
  )
}
