import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { procurementApi, serverMessage } from '@/api/purchasing/procurement.api'
import type { VendorReturnSummary } from '@/api/purchasing/procurement.api'
import { purchaseOrdersApi } from '@/api/purchaseOrders.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

const money = (v: number) => new Intl.NumberFormat().format(v)

function CreateReturnModal({ presetOrderId, onClose }: { presetOrderId?: string; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [orderId, setOrderId] = useState(presetOrderId ?? '')
  const [reason, setReason] = useState('')
  const [qty, setQty] = useState<Record<string, string>>({})
  const { data: orders } = useQuery({ queryKey: ['po', 'orders', 'returnable'], queryFn: () => purchaseOrdersApi.list({ pageNumber: 1, pageSize: 100 }) })
  const received = (orders?.items ?? []).filter((o) => /received/i.test(String(o.status).replace(/_/g, '')))
  const { data: proc } = useQuery({ queryKey: ['po', 'procurement', orderId], queryFn: () => procurementApi.orderProcurement(orderId), enabled: !!orderId })

  const create = useMutation({
    mutationFn: () => procurementApi.createReturn({
      purchaseOrderId: orderId, reason,
      lines: Object.entries(qty).filter(([, v]) => Number(v) > 0).map(([productCode, v]) => ({ productCode, quantity: Number(v) })),
    }),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['po', 'returns'] }); onClose() },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[92vh] w-full max-w-2xl overflow-y-auto p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('procurement.return.newTitle')}</h3>
        <label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.invoice.order')}</label>
        <select className="input" value={orderId} onChange={(e) => { setOrderId(e.target.value); setQty({}) }}>
          <option value="">{t('common.select')}</option>
          {received.map((o) => <option key={o.id} value={o.id}>{o.poNumber} — {o.supplier?.name ?? ''}</option>)}
        </select>

        {proc && (
          <table className="mt-4 min-w-full text-sm">
            <thead><tr className="border-b border-gray-100 text-left text-xs uppercase text-gray-400"><th className="pb-2">{t('procurement.productName')}</th><th className="pb-2 text-right">{t('procurement.received')}</th><th className="pb-2 text-right">{t('procurement.returned')}</th><th className="pb-2 text-right">{t('procurement.return.returnable')}</th><th className="pb-2 text-right">{t('procurement.return.quantity')}</th></tr></thead>
            <tbody className="divide-y divide-gray-50">
              {proc.lines.map((l) => (
                <tr key={l.productCode}>
                  <td className="py-2">{l.productName}</td>
                  <td className="py-2 text-right">{l.received}</td>
                  <td className="py-2 text-right">{l.returned}</td>
                  <td className="py-2 text-right font-semibold">{l.returnable}</td>
                  <td className="py-2 text-right"><input type="number" min={0} max={l.returnable} disabled={l.returnable === 0} className="input w-24 text-right" value={qty[l.productCode] ?? ''} onChange={(e) => setQty({ ...qty, [l.productCode]: e.target.value })} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <label className="mb-1 mt-4 block text-sm font-medium text-gray-700">{t('procurement.return.reason')}</label>
        <input className="input" value={reason} onChange={(e) => setReason(e.target.value)} />
        {create.isError && <p className="mt-3 text-xs text-red-600">{serverMessage(create.error, t('common.error'))}</p>}
        <div className="mt-4 flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={!orderId || !reason.trim() || create.isPending || !Object.values(qty).some((v) => Number(v) > 0)} onClick={() => create.mutate()}>{t('common.save')}</button>
        </div>
      </div>
    </div>
  )
}

function CreditModal({ ret, onClose }: { ret: VendorReturnSummary; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [number, setNumber] = useState('')
  const [amount, setAmount] = useState(String(ret.value))
  const credit = useMutation({
    mutationFn: () => procurementApi.creditReturn(ret.id, number, Number(amount)),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['po', 'returns'] }); onClose() },
  })
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-1 text-base font-semibold text-gray-900">{t('procurement.return.creditTitle')}</h3>
        <p className="mb-4 text-sm text-gray-500">{ret.returnNumber} · {money(ret.value)} {ret.currencyCode}</p>
        <label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.return.creditNote')}</label>
        <input className="input mb-3" value={number} onChange={(e) => setNumber(e.target.value)} />
        <label className="mb-1 block text-sm font-medium text-gray-700">{t('procurement.return.creditAmount')}</label>
        <input type="number" className="input" value={amount} onChange={(e) => setAmount(e.target.value)} />
        {credit.isError && <p className="mt-3 text-xs text-red-600">{serverMessage(credit.error, t('common.error'))}</p>}
        <div className="mt-4 flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={!number.trim() || !(Number(amount) > 0) || credit.isPending} onClick={() => credit.mutate()}>{t('common.save')}</button>
        </div>
      </div>
    </div>
  )
}

export function VendorReturnListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [params] = useSearchParams()
  const purchaseOrderId = params.get('po') ?? undefined
  const [status, setStatus] = useState('')
  const [creating, setCreating] = useState(false)
  const [crediting, setCrediting] = useState<VendorReturnSummary | null>(null)
  const canManage = hasPermission('vendor_return:manage')

  const { data = [], isLoading } = useQuery({ queryKey: ['po', 'returns', status, purchaseOrderId], queryFn: () => procurementApi.returns({ status: status || undefined, purchaseOrderId }) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['po', 'returns'] })
  const ship = useMutation({ mutationFn: (id: string) => procurementApi.shipReturn(id), onSuccess: refresh })
  const cancel = useMutation({ mutationFn: (id: string) => procurementApi.cancelReturn(id), onSuccess: refresh })

  const columns = useMemo(
    () => [
      { key: 'returnNumber', label: t('procurement.return.number'), className: 'font-mono text-xs' },
      { key: 'poNumber', label: t('procurement.invoice.order') },
      { key: 'supplierName', label: t('procurement.supplier') },
      { key: 'value', label: t('procurement.return.value'), render: (r: VendorReturnSummary) => `${money(r.value)} ${r.currencyCode}` },
      { key: 'status', label: t('common.status'), render: (r: VendorReturnSummary) => <StatusBadge status={r.status} /> },
      {
        key: 'actions', label: '',
        render: (r: VendorReturnSummary) => canManage ? (
          <div className="flex gap-3">
            {r.status === 'Draft' && <button className="text-xs text-blue-600 hover:underline" onClick={() => ship.mutate(r.id)}>{t('procurement.return.ship')}</button>}
            {r.status === 'Draft' && <button className="text-xs text-gray-500 hover:underline" onClick={() => cancel.mutate(r.id)}>{t('common.cancel')}</button>}
            {r.status === 'Shipped' && <button className="text-xs text-blue-600 hover:underline" onClick={() => setCrediting(r)}>{t('procurement.return.recordCredit')}</button>}
          </div>
        ) : null,
      },
    ],
    [canManage, t, ship, cancel],
  )
  const error = [ship, cancel].map((m) => (m.isError ? serverMessage(m.error, '') : '')).find(Boolean)

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t('procurement.return.listTitle')}</h1>
          <p className="text-sm text-gray-500">{t('procurement.return.hint')}</p>
        </div>
        {canManage && <button className="btn-primary" onClick={() => setCreating(true)}>{t('procurement.return.add')}</button>}
      </div>
      <select className="input max-w-xs" value={status} onChange={(e) => setStatus(e.target.value)}>
        <option value="">{t('procurement.allStatuses')}</option>
        {['Draft', 'Shipped', 'Credited', 'Cancelled'].map((s) => <option key={s} value={s}>{s}</option>)}
      </select>
      {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}
      <DataTable columns={columns} data={data} isLoading={isLoading} />
      {creating && <CreateReturnModal presetOrderId={purchaseOrderId} onClose={() => setCreating(false)} />}
      {crediting && <CreditModal ret={crediting} onClose={() => setCrediting(null)} />}
    </div>
  )
}
