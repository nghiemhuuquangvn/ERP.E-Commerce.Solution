import { useState, useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import axios from 'axios'
import { ordersApi, OrderStatus } from '@/api/orders.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'
import type { SalesOrderSummary } from '@/api/orders.api'

const STATUS_OPTIONS: OrderStatus[] = [
  'Draft',
  'Submitted',
  'Approved',
  'Picked',
  'Shipped',
  'PartiallyShipped',
  'Delivered',
  'Rejected',
  'Cancelled',
  'ReturnRequested',
  'Returned',
]

export function OrderListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState<OrderStatus | ''>('')
  const [cancelId, setCancelId] = useState<string | null>(null)
  const [approveError, setApproveError] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['orders', { page, pageSize, status }],
    queryFn: () => ordersApi.list({ page, pageSize, status: status || undefined }),
    staleTime: 30_000,
  })

  const approveMutation = useMutation({
    mutationFn: ordersApi.approve,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['orders'] }),
    onError: (err) => {
      const message = axios.isAxiosError(err) ? (err.response?.data as { message?: string })?.message : undefined
      setApproveError(message ?? t('common.failedToSave'))
    },
  })

  const cancelMutation = useMutation({
    mutationFn: (id: string) => ordersApi.cancel(id, 'Cancelled by admin'),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['orders'] })
      setCancelId(null)
    },
  })

  const handleStatusChange = useCallback(
    (value: OrderStatus | '') => {
      setStatus(value)
      setPage(1)
    },
    [setPage],
  )

  const columns = [
    {
      key: 'orderNo',
      label: t('orders.orderNumber'),
      render: (o: SalesOrderSummary) => (
        <Link to={`/orders/${o.orderId}`} className="text-primary-600 hover:underline">
          {o.orderNo || o.orderId.slice(0, 8)}
        </Link>
      ),
    },
    { key: 'customerName', label: t('orders.customer') },
    {
      key: 'totalAmount',
      label: t('orders.total'),
      render: (o: SalesOrderSummary) =>
        new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(o.totalAmount),
    },
    {
      key: 'status',
      label: t('common.status'),
      render: (o: SalesOrderSummary) => <StatusBadge status={o.status} />,
    },
    {
      key: 'orderDate',
      label: t('common.date'),
      render: (o: SalesOrderSummary) => format(new Date(o.orderDate), 'dd MMM yyyy'),
    },
    {
      key: 'actions',
      label: '',
      render: (o: SalesOrderSummary) => (
        <div className="flex gap-2">
          {hasPermission('sales_order:approve') && o.status === 'Submitted' && (
            <button
              onClick={() => {
                setApproveError(null)
                approveMutation.mutate(o.orderId)
              }}
              className="text-xs text-primary-600 hover:underline"
            >
              {t('orders.approve')}
            </button>
          )}
          {hasPermission('sales_order:cancel') && !['Cancelled', 'Delivered', 'Shipped', 'PartiallyShipped', 'Returned'].includes(o.status) && (
            <button onClick={() => setCancelId(o.orderId)} className="text-xs text-red-600 hover:underline">
              {t('orders.cancel')}
            </button>
          )}
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('orders.title')}</h1>
        {hasPermission('sales_order:create') && (
          <Link to="/orders/new" className="btn-primary">
            {t('orders.newOrder')}
          </Link>
        )}
      </div>

      {approveError && <p className="text-sm text-red-600">{approveError}</p>}

      <div className="flex gap-3">
        <select
          className="input max-w-xs"
          value={status}
          onChange={(e) => handleStatusChange(e.target.value as OrderStatus | '')}
        >
          <option value="">{t('common.all')}</option>
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {t(`orders.statusFilters.${s}`)}
            </option>
          ))}
        </select>
      </div>

      <DataTable
        columns={columns}
        data={(data?.items ?? []).map((o) => ({ ...o, id: o.orderId }))}
        isLoading={isLoading}
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />

      <ConfirmDialog
        open={cancelId !== null}
        title={t('orders.cancelOrderTitle')}
        message={t('orders.cancelOrderMessage')}
        confirmLabel={t('orders.cancelOrderConfirm')}
        danger
        onConfirm={() => cancelId && cancelMutation.mutate(cancelId)}
        onCancel={() => setCancelId(null)}
      />
    </div>
  )
}
