import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { format } from 'date-fns'
import { shippingApi, type ReturnParcel } from '@/api/shipping.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/api/purchasing/procurement.api'

const STATUS_FILTERS = ['', 'Requested', 'PickedUp', 'Received', 'Cancelled']
const money = (n: number) => `${new Intl.NumberFormat('vi-VN').format(n)} ₫`
const KEY = ['shipping', 'returns']

/** Every return parcel of the shop: the way back of returned orders booked at the carriers (ROADMAP P1-11d / P2-1b). */
export function ReturnsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const [needsAttention, setNeedsAttention] = useState(false)
  const [cancelling, setCancelling] = useState<ReturnParcel | null>(null)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  const canUpdate = hasPermission('shipping:update')
  const params = { page, pageSize, status: status || undefined, search: search.trim() || undefined, needsAttention: needsAttention || undefined }
  const { data, isLoading } = useQuery({ queryKey: [...KEY, params], queryFn: () => shippingApi.listReturns(params), staleTime: 15_000 })

  const refresh = () => queryClient.invalidateQueries({ queryKey: KEY })
  const fail = (e: unknown) => setMessage({ ok: false, text: serverMessage(e, t('shipments.returns.failed')) })

  const rebook = useMutation({
    mutationFn: (r: ReturnParcel) => shippingApi.rebookReturn(r.id),
    onSuccess: (r) => { setMessage(r.trackingNumber ? { ok: true, text: t('shipments.returns.booked', { tracking: r.trackingNumber }) } : { ok: false, text: r.bookingError ?? t('shipments.returns.notBooked') }); refresh() },
    onError: fail,
  })
  const receive = useMutation({
    mutationFn: (r: ReturnParcel) => shippingApi.receiveReturn(r.originalShipmentId, r.id),
    onSuccess: () => { setMessage({ ok: true, text: t('shipments.returns.receivedDone') }); refresh() },
    onError: fail,
  })
  const cancel = useMutation({
    mutationFn: (r: ReturnParcel) => shippingApi.cancelReturn(r.id),
    onSuccess: () => { setMessage({ ok: true, text: t('shipments.returns.cancelledDone') }); setCancelling(null); refresh() },
    onError: (e) => { fail(e); setCancelling(null) },
  })

  const columns = [
    { key: 'order', label: t('shipments.returns.order'), render: (r: ReturnParcel) => <Link className="font-mono text-xs text-primary-600 hover:underline" to={`/orders/${r.orderId}`}>{r.orderId.slice(0, 8)}</Link> },
    { key: 'reason', label: t('shipments.returns.reason'), render: (r: ReturnParcel) => <span className="line-clamp-2 max-w-xs">{r.reason}</span> },
    {
      key: 'carrier',
      label: t('shipments.carrier'),
      render: (r: ReturnParcel) => (
        <div>
          <div>{r.carrierName ?? '—'}</div>
          <div className="font-mono text-xs text-gray-500">{r.trackingNumber ?? t('shipments.noTracking')}</div>
        </div>
      ),
    },
    { key: 'fee', label: t('shipments.carrierFee'), className: 'text-right', render: (r: ReturnParcel) => (r.carrierFee != null ? money(r.carrierFee) : '—') },
    {
      key: 'status',
      label: t('common.status'),
      render: (r: ReturnParcel) => (
        <div className="space-y-1">
          <StatusBadge status={r.status} label={t(`shipments.returns.status.${r.status}`)} />
          {r.bookingError && !r.trackingNumber && <div className="max-w-xs text-xs text-red-600" title={r.bookingError}>{r.bookingError}</div>}
        </div>
      ),
    },
    { key: 'createdAt', label: t('common.date'), render: (r: ReturnParcel) => (r.createdAt ? format(new Date(r.createdAt), 'dd MMM yyyy') : '—') },
    {
      key: 'actions',
      label: '',
      className: 'text-right',
      render: (r: ReturnParcel) =>
        canUpdate && (r.status === 'Requested' || r.status === 'PickedUp') ? (
          <div className="flex flex-wrap justify-end gap-3">
            {r.status === 'Requested' && !r.trackingNumber && (
              <button className="text-xs text-primary-600 hover:underline" disabled={rebook.isPending} onClick={() => rebook.mutate(r)}>{t('shipments.returns.bookAgain')}</button>
            )}
            <button className="text-xs text-green-700 hover:underline" disabled={receive.isPending} onClick={() => receive.mutate(r)}>{t('shipments.returns.received')}</button>
            {r.status === 'Requested' && (
              <button className="text-xs text-red-600 hover:underline" onClick={() => setCancelling(r)}>{t('common.cancel')}</button>
            )}
          </div>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{t('shipments.returns.title')}</h1>
        <p className="text-sm text-gray-500">{t('shipments.returns.hint')}</p>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <input className="input w-72" placeholder={t('shipments.returns.searchPlaceholder')} value={search} onChange={(e) => { setSearch(e.target.value); setPage(1) }} />
        <select className="input w-auto" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUS_FILTERS.map((s) => <option key={s} value={s}>{s ? t(`shipments.returns.status.${s}`) : t('common.all')}</option>)}
        </select>
        <label className="flex items-center gap-2 text-sm text-gray-600">
          <input type="checkbox" checked={needsAttention} onChange={(e) => { setNeedsAttention(e.target.checked); setPage(1) }} />
          {t('shipments.returns.needsAttention')}
        </label>
      </div>

      {message && (
        <div className={`rounded border px-3 py-2 text-sm ${message.ok ? 'border-green-200 bg-green-50 text-green-800' : 'border-red-200 bg-red-50 text-red-700'}`} role={message.ok ? 'status' : 'alert'}>
          {message.text}
        </div>
      )}

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      <ConfirmDialog
        open={!!cancelling}
        title={t('shipments.returns.cancelTitle')}
        message={t('shipments.returns.cancelConfirm', { tracking: cancelling?.trackingNumber ?? t('shipments.noTracking') })}
        confirmLabel={t('shipments.returns.cancelLabel')}
        danger
        onConfirm={() => cancelling && cancel.mutate(cancelling)}
        onCancel={() => setCancelling(null)}
      />
    </div>
  )
}
