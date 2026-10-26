import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { procurementApi, serverMessage } from '@/api/purchasing/procurement.api'
import type { Rfq, RfqSummary } from '@/api/purchasing/procurement.api'
import { suppliersApi } from '@/api/suppliers.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

const money = (v: number) => new Intl.NumberFormat().format(v)
const day = (v: string | null | undefined) => (v ? new Date(v).toLocaleDateString() : '—')
const inputDate = (offsetDays = 0) => new Date(Date.now() + offsetDays * 86_400_000).toISOString().slice(0, 10)

// ─── Create ─────────────────────────────────────────────────────────────────

function CreateRfqModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: suppliers = [] } = useQuery({ queryKey: ['po', 'suppliers', 'active'], queryFn: () => suppliersApi.list({ isActive: true, pageSize: 200 }) })
  const [title, setTitle] = useState('')
  const [neededBy, setNeededBy] = useState('')
  const [lines, setLines] = useState([{ productCode: '', productName: '', quantity: '1', unit: 'pcs' }])
  const [invited, setInvited] = useState<string[]>([])

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
      navigate(`/purchasing/rfqs/${rfq.id}`)
    },
  })

  const set = (i: number, key: string, value: string) => setLines(lines.map((l, j) => (j === i ? { ...l, [key]: value } : l)))

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[92vh] w-full max-w-3xl overflow-y-auto p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('procurement.rfq.newTitle')}</h3>
        <div className="grid grid-cols-3 gap-3">
          <div className="col-span-2">
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.rfq.title')}</label>
            <input className="input" value={title} onChange={(e) => setTitle(e.target.value)} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.rfq.neededBy')}</label>
            <input type="date" className="input" value={neededBy} onChange={(e) => setNeededBy(e.target.value)} />
          </div>
        </div>

        <p className="mb-1 mt-4 text-sm font-semibold text-gray-700">{t('procurement.rfq.lines')}</p>
        {lines.map((l, i) => (
          <div key={i} className="mb-2 grid grid-cols-12 gap-2">
            <input className="input col-span-3" placeholder={t('procurement.productCode')} value={l.productCode} onChange={(e) => set(i, 'productCode', e.target.value)} />
            <input className="input col-span-5" placeholder={t('procurement.productName')} value={l.productName} onChange={(e) => set(i, 'productName', e.target.value)} />
            <input type="number" className="input col-span-2" value={l.quantity} onChange={(e) => set(i, 'quantity', e.target.value)} />
            <input className="input col-span-1" value={l.unit} onChange={(e) => set(i, 'unit', e.target.value)} />
            <button type="button" className="col-span-1 text-xs text-red-600 hover:underline" onClick={() => setLines(lines.filter((_, j) => j !== i))}>{t('common.delete')}</button>
          </div>
        ))}
        <button type="button" className="btn-outline" onClick={() => setLines([...lines, { productCode: '', productName: '', quantity: '1', unit: 'pcs' }])}>{t('procurement.rfq.addLine')}</button>

        <p className="mb-1 mt-4 text-sm font-semibold text-gray-700">{t('procurement.rfq.invite')}</p>
        <div className="grid max-h-40 grid-cols-2 gap-1 overflow-y-auto rounded-md border border-gray-200 p-2 text-sm">
          {suppliers.map((s) => (
            <label key={s.id} className="flex items-center gap-2">
              <input type="checkbox" checked={invited.includes(s.id)} onChange={(e) => setInvited(e.target.checked ? [...invited, s.id] : invited.filter((x) => x !== s.id))} />
              {s.name}
            </label>
          ))}
          {suppliers.length === 0 && <span className="text-gray-400">{t('procurement.rfq.noSuppliers')}</span>}
        </div>

        {create.isError && <p className="mt-3 text-xs text-red-600">{serverMessage(create.error, t('common.error'))}</p>}
        <div className="mt-4 flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={!title.trim() || create.isPending} onClick={() => create.mutate()}>{t('common.save')}</button>
        </div>
      </div>
    </div>
  )
}

// ─── List ───────────────────────────────────────────────────────────────────

export function RfqListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const [status, setStatus] = useState('')
  const [creating, setCreating] = useState(false)
  const { data = [], isLoading } = useQuery({ queryKey: ['po', 'rfqs', status], queryFn: () => procurementApi.rfqs(status || undefined) })

  const columns = useMemo(
    () => [
      { key: 'rfqNumber', label: t('procurement.rfq.number'), className: 'font-mono text-xs', render: (r: RfqSummary) => <Link className="text-blue-600 hover:underline" to={`/purchasing/rfqs/${r.id}`}>{r.rfqNumber}</Link> },
      { key: 'title', label: t('procurement.rfq.title') },
      { key: 'lines', label: t('procurement.rfq.lines') },
      { key: 'quotes', label: t('procurement.rfq.quotes'), render: (r: RfqSummary) => `${r.quoted} / ${r.invited}` },
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
          <p className="text-sm text-gray-500">{t('procurement.rfq.hint')}</p>
        </div>
        {hasPermission('rfq:manage') && <button className="btn-primary" onClick={() => setCreating(true)}>{t('procurement.rfq.add')}</button>}
      </div>
      <select className="input max-w-xs" value={status} onChange={(e) => setStatus(e.target.value)}>
        <option value="">{t('procurement.allStatuses')}</option>
        {['Draft', 'Sent', 'Awarded', 'Cancelled'].map((s) => <option key={s} value={s}>{s}</option>)}
      </select>
      <DataTable columns={columns} data={data} isLoading={isLoading} />
      {creating && <CreateRfqModal onClose={() => setCreating(false)} />}
    </div>
  )
}

// ─── Quotation form ─────────────────────────────────────────────────────────

function QuotationModal({ rfq, supplierId, onClose }: { rfq: Rfq; supplierId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const existing = rfq.quotations.find((q) => q.supplierId === supplierId)
  const [quoteNumber, setQuoteNumber] = useState(existing?.quoteNumber ?? '')
  const [validUntil, setValidUntil] = useState(existing ? existing.validUntil.slice(0, 10) : inputDate(14))
  const [currency, setCurrency] = useState(existing?.currencyCode ?? 'VND')
  const [lead, setLead] = useState(String(existing?.leadTimeDays ?? 7))
  const [prices, setPrices] = useState<Record<string, string>>(
    Object.fromEntries(rfq.lines.map((l) => [l.id, String(existing?.lines.find((x) => x.rfqLineId === l.id)?.unitPrice ?? '')])),
  )

  const save = useMutation({
    mutationFn: () =>
      procurementApi.recordQuotation(rfq.id, {
        supplierId, quoteNumber, validUntil, currencyCode: currency, leadTimeDays: Number(lead) || 0, notes: null,
        unitPrices: Object.fromEntries(Object.entries(prices).map(([k, v]) => [k, Number(v)])),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['po', 'rfq', rfq.id] })
      queryClient.invalidateQueries({ queryKey: ['po', 'rfq-comparison', rfq.id] })
      onClose()
    },
  })

  const name = rfq.invitations.find((i) => i.supplierId === supplierId)?.supplierName ?? ''
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[92vh] w-full max-w-xl overflow-y-auto p-6">
        <h3 className="mb-1 text-base font-semibold text-gray-900">{t('procurement.rfq.quotationTitle')}</h3>
        <p className="mb-4 text-sm text-gray-500">{name}</p>
        <div className="grid grid-cols-2 gap-3">
          <div><label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.rfq.quoteNumber')}</label><input className="input" value={quoteNumber} onChange={(e) => setQuoteNumber(e.target.value)} /></div>
          <div><label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.rfq.validUntil')}</label><input type="date" className="input" value={validUntil} onChange={(e) => setValidUntil(e.target.value)} /></div>
          <div><label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.currency')}</label><input className="input" maxLength={3} value={currency} onChange={(e) => setCurrency(e.target.value.toUpperCase())} /></div>
          <div><label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.rfq.leadTime')}</label><input type="number" className="input" value={lead} onChange={(e) => setLead(e.target.value)} /></div>
        </div>
        <p className="mb-1 mt-4 text-sm font-semibold text-gray-700">{t('procurement.rfq.unitPrices')}</p>
        {rfq.lines.map((l) => (
          <div key={l.id} className="mb-2 grid grid-cols-12 items-center gap-2 text-sm">
            <span className="col-span-7">{l.productName} <span className="text-gray-400">× {l.quantity} {l.unit}</span></span>
            <input type="number" step="0.01" className="input col-span-5" value={prices[l.id]} onChange={(e) => setPrices({ ...prices, [l.id]: e.target.value })} />
          </div>
        ))}
        {save.isError && <p className="mt-3 text-xs text-red-600">{serverMessage(save.error, t('common.error'))}</p>}
        <div className="mt-4 flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>{t('common.save')}</button>
        </div>
      </div>
    </div>
  )
}

// ─── Detail ─────────────────────────────────────────────────────────────────

export function RfqDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [quoting, setQuoting] = useState<string | null>(null)
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
  const send = useMutation({ mutationFn: () => procurementApi.sendRfq(id!), onSuccess: refresh })
  const cancel = useMutation({ mutationFn: () => procurementApi.cancelRfq(id!), onSuccess: refresh })
  const decline = useMutation({ mutationFn: (supplierId: string) => procurementApi.declineRfq(id!, supplierId), onSuccess: refresh })
  const award = useMutation({
    mutationFn: (supplierId: string) => procurementApi.awardRfq(id!, supplierId),
    onSuccess: (r) => {
      refresh()
      if (r.awardedPurchaseOrderId) navigate(`/orders/purchase/${r.awardedPurchaseOrderId}`)
    },
  })

  if (!rfq) return <div className="text-gray-400">{t('common.loading')}</div>
  const error = [send, cancel, decline, award].map((m) => (m.isError ? serverMessage(m.error, '') : '')).find(Boolean)

  return (
    <div className="space-y-5">
      <div className="flex items-center gap-3">
        <Link to="/purchasing/rfqs" className="text-sm text-gray-500 hover:text-gray-700">{t('procurement.rfq.back')}</Link>
        <h1 className="text-xl font-semibold">{rfq.rfqNumber}</h1>
        <StatusBadge status={rfq.status} />
        <span className="text-gray-500">{rfq.title}</span>
        <div className="ml-auto flex gap-2">
          {canManage && rfq.status === 'Draft' && <button className="btn-primary" onClick={() => send.mutate()}>{t('procurement.rfq.send')}</button>}
          {canManage && (rfq.status === 'Draft' || rfq.status === 'Sent') && <button className="btn-outline" onClick={() => cancel.mutate()}>{t('procurement.rfq.cancel')}</button>}
          {rfq.awardedPurchaseOrderId && <Link className="btn-outline" to={`/orders/purchase/${rfq.awardedPurchaseOrderId}`}>{t('procurement.rfq.openOrder')}</Link>}
        </div>
      </div>
      {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      <div className="grid gap-5 md:grid-cols-2">
        <div className="card p-5">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">{t('procurement.rfq.lines')}</h2>
          <table className="min-w-full text-sm">
            <tbody>{rfq.lines.map((l) => <tr key={l.id}><td className="py-1">{l.productName} <span className="font-mono text-xs text-gray-400">{l.productCode}</span></td><td className="py-1 text-right">{l.quantity} {l.unit}</td></tr>)}</tbody>
          </table>
          {rfq.neededBy && <p className="mt-2 text-xs text-gray-500">{t('procurement.rfq.neededBy')}: {day(rfq.neededBy)}</p>}
        </div>
        <div className="card p-5">
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">{t('procurement.rfq.suppliers')}</h2>
          <table className="min-w-full text-sm">
            <tbody>
              {rfq.invitations.map((i) => (
                <tr key={i.supplierId}>
                  <td className="py-1">{i.supplierName}</td>
                  <td className="py-1"><StatusBadge status={i.state} /></td>
                  <td className="py-1 text-right">
                    {canManage && rfq.status === 'Sent' && (
                      <>
                        <button className="mr-3 text-xs text-blue-600 hover:underline" onClick={() => setQuoting(i.supplierId)}>{i.state === 'Quoted' ? t('procurement.rfq.editQuote') : t('procurement.rfq.enterQuote')}</button>
                        {i.state === 'Invited' && <button className="text-xs text-gray-500 hover:underline" onClick={() => decline.mutate(i.supplierId)}>{t('procurement.rfq.declined')}</button>}
                      </>
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
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">{t('procurement.rfq.comparison')}</h2>
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-left text-xs uppercase text-gray-400">
                <th className="pb-2">{t('procurement.productName')}</th>
                <th className="pb-2 text-right">{t('procurement.qty')}</th>
                {cmp.suppliers.map((s) => (
                  <th key={s.supplierId} className={`pb-2 text-right ${s.expired ? 'text-gray-300' : ''}`}>
                    {s.supplierName}
                    <div className="text-[10px] normal-case text-gray-400">{s.quoteNumber} · {s.currencyCode} · {s.leadTimeDays}d · {t('procurement.rfq.validUntil')} {day(s.validUntil)}{s.expired ? ` (${t('procurement.rfq.expired')})` : ''}</div>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {cmp.lines.map((l) => (
                <tr key={l.lineId}>
                  <td className="py-2">{l.productName}</td>
                  <td className="py-2 text-right">{l.quantity}</td>
                  {cmp.suppliers.map((s) => {
                    const p = l.prices.find((x) => x.supplierId === s.supplierId)
                    return (
                      <td key={s.supplierId} className={`py-2 text-right ${p?.expired ? 'text-gray-300' : ''} ${p?.isLowest ? 'bg-green-50 font-semibold text-green-700' : ''}`}>
                        {p ? money(p.unitPrice) : '—'}
                      </td>
                    )
                  })}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t border-gray-200 font-semibold">
                <td className="pt-3" colSpan={2}>{t('procurement.rfq.total')}</td>
                {cmp.suppliers.map((s) => <td key={s.supplierId} className="pt-3 text-right">{money(s.total)}{s.isWinner ? ` ★` : ''}</td>)}
              </tr>
              {canManage && rfq.status === 'Sent' && (
                <tr>
                  <td colSpan={2} />
                  {cmp.suppliers.map((s) => (
                    <td key={s.supplierId} className="pt-2 text-right">
                      <button className="btn-primary" disabled={s.expired || award.isPending} onClick={() => award.mutate(s.supplierId)}>{t('procurement.rfq.award')}</button>
                    </td>
                  ))}
                </tr>
              )}
            </tfoot>
          </table>
        </div>
      )}

      {quoting && <QuotationModal rfq={rfq} supplierId={quoting} onClose={() => setQuoting(null)} />}
    </div>
  )
}
