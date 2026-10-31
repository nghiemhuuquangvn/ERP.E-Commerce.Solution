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
import { Button, Checkbox, EmptyState, Input, Select, useToast } from '@/components/ui'
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
  const toast = useToast()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const [needsAttention, setNeedsAttention] = useState(false)
  const [cancelling, setCancelling] = useState<ReturnParcel | null>(null)

  const canUpdate = hasPermission('shipping:update')
  const params = { page, pageSize, status: status || undefined, search: search.trim() || undefined, needsAttention: needsAttention || undefined }
  const { data, isLoading } = useQuery({ queryKey: [...KEY, params], queryFn: () => shippingApi.listReturns(params), staleTime: 15_000 })

  const refresh = () => queryClient.invalidateQueries({ queryKey: KEY })
  const fail = (e: unknown) => toast.error(serverMessage(e, t('shipments.returns.failed')))
  const clearFilters = () => { setStatus(''); setSearch(''); setNeedsAttention(false); setPage(1) }
  const filtered = !!(status || search.trim() || needsAttention)

  const rebook = useMutation({
    mutationFn: (r: ReturnParcel) => shippingApi.rebookReturn(r.id),
    onSuccess: (r) => { if (r.trackingNumber) toast.success(t('shipments.returns.booked', { tracking: r.trackingNumber })); else toast.error(r.bookingError ?? t('shipments.returns.notBooked')); refresh() },
    onError: fail,
  })
  const receive = useMutation({
    mutationFn: (r: ReturnParcel) => shippingApi.receiveReturn(r.originalShipmentId, r.id),
    onSuccess: () => { toast.success(t('shipments.returns.receivedDone')); refresh() },
    onError: fail,
  })
  const cancel = useMutation({
    mutationFn: (r: ReturnParcel) => shippingApi.cancelReturn(r.id),
    onSuccess: () => { toast.success(t('shipments.returns.cancelledDone')); setCancelling(null); refresh() },
    onError: (e) => { fail(e); setCancelling(null) },
  })

  const columns = [
    { key: 'order', label: t('shipments.returns.order'), render: (r: ReturnParcel) => <Link className="font-mono text-xs text-brand-action hover:underline" to={`/orders/${r.orderId}`}>{r.orderId.slice(0, 8)}</Link> },
    { key: 'reason', label: t('shipments.returns.reason'), render: (r: ReturnParcel) => <span className="line-clamp-2 max-w-xs">{r.reason}</span> },
    {
      key: 'carrier',
      label: t('shipments.carrier'),
      render: (r: ReturnParcel) => (
        <div>
          <div>{r.carrierName ?? '—'}</div>
          <div className="font-mono text-xs text-ink-secondary">{r.trackingNumber ?? t('shipments.noTracking')}</div>
        </div>
      ),
    },
    { key: 'fee', label: t('shipments.carrierFee'), align: 'right' as const, render: (r: ReturnParcel) => (r.carrierFee != null ? money(r.carrierFee) : '—') },
    {
      key: 'status',
      label: t('common.status'),
      render: (r: ReturnParcel) => (
        <div className="space-y-1">
          <StatusBadge status={r.status} label={t(`shipments.returns.status.${r.status}`)} />
          {r.bookingError && !r.trackingNumber && <div className="max-w-xs text-xs text-danger-soft-text" title={r.bookingError}>{r.bookingError}</div>}
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
              <Button variant="link" className="text-xs" loading={rebook.isPending && rebook.variables?.id === r.id} onClick={() => rebook.mutate(r)}>{t('shipments.returns.bookAgain')}</Button>
            )}
            <Button variant="link" className="text-xs" loading={receive.isPending && receive.variables?.id === r.id} onClick={() => receive.mutate(r)}>{t('shipments.returns.received')}</Button>
            {r.status === 'Requested' && (
              <Button variant="link-danger" className="text-xs" onClick={() => setCancelling(r)}>{t('shipments.returns.cancelLabel')}</Button>
            )}
          </div>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{t('shipments.returns.title')}</h1>
        <p className="text-sm text-ink-secondary">{t('shipments.returns.hint')}</p>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Input className="w-72" aria-label={t('common.search')} placeholder={t('shipments.returns.searchPlaceholder')} value={search} onChange={(e) => { setSearch(e.target.value); setPage(1) }} />
        <Select className="w-auto" aria-label={t('common.status')} value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUS_FILTERS.map((s) => <option key={s} value={s}>{s ? t(`shipments.returns.status.${s}`) : t('common.all')}</option>)}
        </Select>
        <Checkbox label={t('shipments.returns.needsAttention')} checked={needsAttention} onChange={(e) => { setNeedsAttention(e.target.checked); setPage(1) }} />
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={filtered
          ? <EmptyState variant="no-results" title={t('common.noResults')} description={t('common.noResultsHint')} action={<Button variant="outline" onClick={clearFilters}>{t('common.clear')}</Button>} />
          : <EmptyState title={t('shipments.returns.empty')} description={t('shipments.returns.emptyHint')} />}
      />
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
