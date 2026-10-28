import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { format } from 'date-fns'
import { shippingApi, type Shipment, type SplitPart } from '@/api/shipping.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/api/purchasing/procurement.api'

const STATUS_FILTERS = ['', 'Pending', 'ReadyForPickup', 'PickedUp', 'InTransit', 'OutForDelivery', 'Delivered', 'DeliveryFailed', 'Returned', 'Cancelled']

const money = (n: number) => `${new Intl.NumberFormat('vi-VN').format(n)} ₫`

/** Splits a parcel the carrier has not collected into several: a grid of product quantities per new parcel (every item must go to exactly one parcel). */
function SplitDialog({ shipment, onClose, onDone }: { shipment: Shipment; onClose: () => void; onDone: () => void }) {
  const { t } = useTranslation()
  const items = shipment.items ?? []
  const [count, setCount] = useState(2)
  // qty[parcel][item]; the first parcel starts with everything so the person moves items out.
  const [qty, setQty] = useState<number[][]>(() => [items.map((i) => i.quantity), items.map(() => 0)])
  const [cod, setCod] = useState<number[]>([])

  const resize = (n: number) => {
    setCount(n)
    setQty((q) => Array.from({ length: n }, (_, p) => q[p] ?? items.map(() => 0)))
    setCod([])
  }
  const setCell = (p: number, i: number, v: number) => {
    setQty((q) => q.map((row, rp) => (rp === p ? row.map((x, ri) => (ri === i ? Math.max(0, v) : x)) : row)))
    setCod([])
  }

  const units = qty.map((row) => row.reduce((a, b) => a + b, 0))
  const totalUnits = units.reduce((a, b) => a + b, 0)
  const unassigned = items.map((it, i) => it.quantity - qty.reduce((a, row) => a + row[i], 0))
  const exact = unassigned.every((u) => u === 0) && units.every((u) => u > 0)
  // The proposed cash follows the units; the last parcel takes the remainder (the server does the same when none is typed).
  const proposal = qty.map((_, p) => (p === count - 1 ? shipment.codAmount - qty.slice(0, p).reduce((a, _r, rp) => a + Math.round((shipment.codAmount * units[rp]) / Math.max(1, totalUnits)), 0) : Math.round((shipment.codAmount * units[p]) / Math.max(1, totalUnits))))
  const codValues = shipment.codAmount > 0 ? (cod.length === count ? cod : proposal) : []
  const codSum = codValues.reduce((a, b) => a + b, 0)
  const codOk = shipment.codAmount === 0 || codSum === shipment.codAmount

  const split = useMutation({
    mutationFn: () => {
      const parts: SplitPart[] = qty.map((row, p) => ({
        items: items.map((it, i) => ({ productId: it.productId, quantity: row[i] })).filter((l) => l.quantity > 0),
        codAmount: shipment.codAmount > 0 ? codValues[p] : undefined,
      }))
      return shippingApi.split(shipment.id, parts)
    },
    onSuccess: onDone,
  })

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-3xl space-y-4 overflow-y-auto p-6">
        <h3 className="text-base font-semibold text-gray-900">{t('shipments.split.title')}</h3>
        <p className="text-xs text-gray-500">{t('shipments.split.hint')}</p>
        <label className="flex items-center gap-2 text-sm">
          {t('shipments.split.parcels')}
          <input type="number" min={2} max={10} className="input w-20" value={count} onChange={(e) => resize(Math.min(10, Math.max(2, Number(e.target.value) || 2)))} />
        </label>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs text-gray-500">
              <th className="py-1">{t('shipments.split.item')}</th>
              {qty.map((_, p) => <th key={p} className="w-24 py-1">{t('shipments.split.parcel', { n: p + 1 })}</th>)}
              <th className="w-24 py-1">{t('shipments.split.left')}</th>
            </tr>
          </thead>
          <tbody>
            {items.map((it, i) => (
              <tr key={it.productId} className="border-t border-gray-100">
                <td className="py-1">{it.productName} <span className="text-xs text-gray-400">× {it.quantity}</span></td>
                {qty.map((row, p) => (
                  <td key={p}><input type="number" min={0} className="input w-20" value={row[i]} onChange={(e) => setCell(p, i, Number(e.target.value) || 0)} /></td>
                ))}
                <td className={unassigned[i] === 0 ? 'text-gray-400' : 'font-medium text-red-600'}>{unassigned[i]}</td>
              </tr>
            ))}
            {shipment.codAmount > 0 && (
              <tr className="border-t border-gray-200">
                <td className="py-1 font-medium">{t('shipments.cod')}</td>
                {codValues.map((v, p) => (
                  <td key={p}><input type="number" min={0} className="input w-24" value={v} onChange={(e) => setCod(codValues.map((x, xp) => (xp === p ? Number(e.target.value) || 0 : x)))} /></td>
                ))}
                <td className={codOk ? 'text-gray-400' : 'font-medium text-red-600'}>{money(shipment.codAmount - codSum)}</td>
              </tr>
            )}
          </tbody>
        </table>
        {split.isError && <p className="text-xs text-red-600">{serverMessage(split.error, t('common.failedToSave'))}</p>}
        <div className="flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={!exact || !codOk || split.isPending} onClick={() => split.mutate()}>{t('shipments.split.confirm')}</button>
        </div>
      </div>
    </div>
  )
}

function ShipmentDetail({ shipment, onClose }: { shipment: Shipment; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canUpdate = hasPermission('shipping:update')

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['shipments'] })
  const cancel = useMutation({ mutationFn: () => shippingApi.cancelAtCarrier(shipment.id), onSuccess: () => { refresh(); onClose() } })
  const rebook = useMutation({ mutationFn: () => shippingApi.rebook(shipment.id), onSuccess: () => { refresh(); onClose() } })

  const [splitting, setSplitting] = useState(false)
  const splittable = canUpdate && ['Pending', 'ReadyForPickup'].includes(shipment.status) && (shipment.items ?? []).reduce((a, i) => a + i.quantity, 0) > 1
  const cancellable = canUpdate && ['Pending', 'ReadyForPickup'].includes(shipment.status)
  const row = (label: string, value?: string | number | null) =>
    value === undefined || value === null || value === '' ? null : (
      <div className="flex justify-between gap-4 py-1 text-sm">
        <span className="text-gray-500">{label}</span>
        <span className="text-right text-gray-900">{value}</span>
      </div>
    )

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-2xl space-y-4 overflow-y-auto p-6">
        <div className="flex items-start justify-between">
          <div>
            <h3 className="text-base font-semibold text-gray-900">{shipment.carrierName}</h3>
            <p className="font-mono text-xs text-gray-500">{shipment.trackingNumber ?? t('shipments.noTracking')}</p>
            {(shipment.parcelCount ?? 1) > 1 && <p className="text-xs font-medium text-indigo-700">{t('shipments.split.badge', { n: shipment.parcelNo, count: shipment.parcelCount })}</p>}
          </div>
          <StatusBadge status={shipment.status} />
        </div>

        {shipment.reviewNote && <p className="rounded bg-amber-50 p-2 text-sm text-amber-800">{shipment.reviewNote}</p>}
        {shipment.bookingError && <p className="rounded bg-red-50 p-2 text-sm text-red-700">{shipment.bookingError}</p>}

        <div className="divide-y divide-gray-100">
          {row(t('shipments.recipient'), `${shipment.recipientName} · ${shipment.recipientPhone ?? ''}`)}
          {row(t('shipments.address'), [shipment.addressStreet, shipment.addressWard, shipment.addressDistrict, shipment.addressProvince].filter(Boolean).join(', '))}
          {row(t('shipments.weight'), `${shipment.totalWeight} kg`)}
          {row(t('shipments.cod'), shipment.codAmount > 0 ? money(shipment.codAmount) : t('shipments.prepaid'))}
          {row(t('shipments.declaredValue'), money(shipment.declaredValue))}
          {row(t('shipments.carrierFee'), shipment.carrierFee != null ? money(shipment.carrierFee) : null)}
          {row(t('shipments.carrierOrderCode'), shipment.carrierOrderCode)}
          {row(t('shipments.whyThisCarrier'), shipment.selectionNote)}
          {row(t('shipments.eta'), shipment.estimatedDelivery ? format(new Date(shipment.estimatedDelivery), 'dd MMM yyyy') : null)}
          {row(t('shipments.lastSync'), shipment.lastCarrierSyncAt ? format(new Date(shipment.lastCarrierSyncAt), 'dd MMM yyyy HH:mm') : null)}
        </div>

        <div>
          <h4 className="mb-2 text-sm font-semibold text-gray-900">{t('shipments.timeline')}</h4>
          <ol className="space-y-2 border-l border-gray-200 pl-4">
            {shipment.trackingEvents.map((e, i) => (
              <li key={i} className="text-sm">
                <div className="font-medium text-gray-900">{e.status}</div>
                <div className="text-xs text-gray-500">{e.description} · {format(new Date(e.occurredAt), 'dd MMM HH:mm')}</div>
              </li>
            ))}
            {shipment.trackingEvents.length === 0 && <li className="text-sm text-gray-400">{t('shipments.noEvents')}</li>}
          </ol>
        </div>

        {(cancel.isError || rebook.isError) && (
          <p className="text-xs text-red-600">{serverMessage(cancel.error ?? rebook.error, t('common.failedToSave'))}</p>
        )}

        <div className="flex justify-end gap-3">
          {canUpdate && !shipment.trackingNumber && shipment.status !== 'Cancelled' && (
            <button className="btn-outline" disabled={rebook.isPending} onClick={() => rebook.mutate()}>{t('shipments.rebook')}</button>
          )}
          {splittable && <button className="btn-outline" onClick={() => setSplitting(true)}>{t('shipments.split.open')}</button>}
          {cancellable && (
            <button className="btn-outline text-red-600" disabled={cancel.isPending} onClick={() => cancel.mutate()}>{t('shipments.cancelAtCarrier')}</button>
          )}
          <button className="btn-primary" onClick={onClose}>{t('common.close')}</button>
        </div>
      </div>
      {splitting && <SplitDialog shipment={shipment} onClose={() => setSplitting(false)} onDone={() => { refresh(); onClose() }} />}
    </div>
  )
}

export function ShipmentsPage() {
  const { t } = useTranslation()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const [needsReview, setNeedsReview] = useState(false)
  const [selected, setSelected] = useState<Shipment | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['shipments', { page, pageSize, status, search, needsReview }],
    queryFn: () => shippingApi.listShipments({ page, pageSize, status: status || undefined, search: search || undefined, needsReview: needsReview || undefined }),
  })

  const columns = [
    { key: 'tracking', label: t('shipments.tracking'), className: 'font-mono text-xs', render: (s: Shipment) => s.trackingNumber ?? '—' },
    { key: 'carrier', label: t('shipments.carrier'), render: (s: Shipment) => s.carrierName },
    { key: 'recipient', label: t('shipments.recipient'), render: (s: Shipment) => `${s.recipientName}, ${s.addressProvince}` },
    { key: 'cod', label: t('shipments.cod'), className: 'text-right', render: (s: Shipment) => (s.codAmount > 0 ? money(s.codAmount) : '—') },
    { key: 'fee', label: t('shipments.carrierFee'), className: 'text-right', render: (s: Shipment) => (s.carrierFee != null ? money(s.carrierFee) : '—') },
    {
      key: 'status',
      label: t('common.status'),
      render: (s: Shipment) => (
        <div className="space-y-1">
          <StatusBadge status={s.status} />
          {s.reviewNote && <div className="text-xs text-amber-700" title={s.reviewNote}>{t('shipments.needsAttention')}</div>}
        </div>
      ),
    },
    { key: 'createdAt', label: t('common.date'), render: (s: Shipment) => format(new Date(s.createdAt), 'dd MMM yyyy') },
    { key: 'actions', label: '', className: 'text-right', render: (s: Shipment) => <button className="btn-outline text-xs" onClick={() => setSelected(s)}>{t('common.view')}</button> },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('shipments.title')}</h1>

      <div className="flex flex-wrap items-center gap-3">
        <input className="input w-64" placeholder={t('shipments.searchPlaceholder')} value={search} onChange={(e) => { setSearch(e.target.value); setPage(1) }} />
        <select className="input w-auto" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUS_FILTERS.map((s) => <option key={s} value={s}>{s ? t(`shipments.status.${s}`) : t('common.all')}</option>)}
        </select>
        <label className="flex items-center gap-2 text-sm text-gray-600">
          <input type="checkbox" checked={needsReview} onChange={(e) => { setNeedsReview(e.target.checked); setPage(1) }} />
          {t('shipments.needsAttention')}
        </label>
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {selected && <ShipmentDetail shipment={selected} onClose={() => setSelected(null)} />}
    </div>
  )
}
