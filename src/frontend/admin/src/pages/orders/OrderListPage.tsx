import { useState, useMemo, useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ordersApi, OrderStatus } from '@/api/orders.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'
import type { Order } from '@/api/orders.api'

const STATUS_FILTERS: { labelKey: string; value: OrderStatus | '' }[] = [
  { labelKey: 'common.all', value: '' },
  { labelKey: 'orders.statusFilters.draft', value: 'Draft' },
  { labelKey: 'orders.statusFilters.confirmed', value: 'Confirmed' },
  { labelKey: 'orders.statusFilters.processing', value: 'Processing' },
  { labelKey: 'orders.statusFilters.shipped', value: 'Shipped' },
  { labelKey: 'orders.statusFilters.delivered', value: 'Delivered' },
  { labelKey: 'orders.statusFilters.cancelled', value: 'Cancelled' },
]

export function OrderListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<OrderStatus | ''>('')
  const [cancelId, setCancelId] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['orders', { page, pageSize, search, status }],
    queryFn: () =>
      ordersApi.list({ page, pageSize, search: search || undefined, status: status || undefined }),
    staleTime: 30_000,
  })

  const approveMutation = useMutation({
    mutationFn: ordersApi.approve,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['orders'] }),
  })

  const cancelMutation = useMutation({
    mutationFn: (id: string) => ordersApi.cancel(id, 'Cancelled by admin'),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['orders'] })
      setCancelId(null)
    },
  })

  // Stable handlers — only recreated when setPage changes (which is never)
  const handleSearchChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      setSearch(e.target.value)
      setPage(1)
    },
    [setPage],
  )

  const handleStatusChange = useCallback(
    (value: OrderStatus | '') => {
      setStatus(value)
      setPage(1)
    },
    [setPage],
  )

  const handleCancelConfirm = useCallback(() => {
    if (cancelId) cancelMutation.mutate(cancelId)
  }, [cancelId, cancelMutation])

  // Columns only recreate when permissions or approve mutate fn change
  const { mutate: approveOrder } = approveMutation
  const columns = useMemo(
    () => [
      {
        key: 'orderNumber',
        label: t('orders.orderNumber'),
        render: (o: Order) => (
          <Link to={`/orders/${o.id}`} className="text-primary-600 hover:underline">
            {o.orderNumber}
          </Link>
        ),
      },
      { key: 'customerName', label: t('orders.customer') },
      {
        key: 'totalAmount',
        label: t('orders.total'),
        render: (o: Order) =>
          new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(
            o.totalAmount,
          ),
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (o: Order) => <StatusBadge status={o.status} />,
      },
      {
        key: 'orderDate',
        label: t('common.date'),
        render: (o: Order) => format(new Date(o.orderDate), 'dd MMM yyyy'),
      },
      {
        key: 'actions',
        label: '',
        render: (o: Order) => (
          <div className="flex gap-2">
            {hasPermission('orders:approve') && o.status === 'Draft' && (
              <button
                onClick={() => approveOrder(o.id)}
                className="text-xs text-primary-600 hover:underline"
              >
                {t('orders.approve')}
              </button>
            )}
            {hasPermission('orders:cancel') && !['Cancelled', 'Delivered'].includes(o.status) && (
              <button
                onClick={() => setCancelId(o.id)}
                className="text-xs text-red-600 hover:underline"
              >
                {t('orders.cancel')}
              </button>
            )}
          </div>
        ),
      },
    ],
    [hasPermission, approveOrder, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('orders.title')}</h1>
        {hasPermission('orders:create') && (
          <Link to="/orders/new" className="btn-primary">
            {t('orders.newOrder')}
          </Link>
        )}
      </div>

      <div className="flex gap-3">
        <input
          className="input max-w-xs"
          placeholder={t('orders.searchPlaceholder')}
          value={search}
          onChange={handleSearchChange}
        />
        <div className="flex gap-1">
          {STATUS_FILTERS.map((f) => (
            <button
              key={f.value}
              onClick={() => handleStatusChange(f.value as OrderStatus | '')}
              className={`rounded px-3 py-1.5 text-sm ${
                status === f.value
                  ? 'bg-primary-600 text-white'
                  : 'bg-white border border-gray-300 text-gray-600 hover:bg-gray-50'
              }`}
            >
              {t(f.labelKey)}
            </button>
          ))}
        </div>
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />

      <ConfirmDialog
        open={cancelId !== null}
        title={t('orders.cancelOrderTitle')}
        message={t('orders.cancelOrderMessage')}
        confirmLabel={t('orders.cancelOrderConfirm')}
        danger
        onConfirm={handleCancelConfirm}
        onCancel={() => setCancelId(null)}
      />
    </div>
  )
}
