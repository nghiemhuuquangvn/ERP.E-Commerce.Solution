import { useState, useMemo, useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { ordersApi, OrderStatus } from '@/api/orders.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'
import type { Order } from '@/api/orders.api'

const STATUS_FILTERS: { label: string; value: OrderStatus | '' }[] = [
  { label: 'All', value: '' },
  { label: 'Draft', value: 'Draft' },
  { label: 'Confirmed', value: 'Confirmed' },
  { label: 'Processing', value: 'Processing' },
  { label: 'Shipped', value: 'Shipped' },
  { label: 'Delivered', value: 'Delivered' },
  { label: 'Cancelled', value: 'Cancelled' },
]

export function OrderListPage() {
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
        label: 'Order #',
        render: (o: Order) => (
          <Link to={`/orders/${o.id}`} className="text-primary-600 hover:underline">
            {o.orderNumber}
          </Link>
        ),
      },
      { key: 'customerName', label: 'Customer' },
      {
        key: 'totalAmount',
        label: 'Total',
        render: (o: Order) =>
          new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(
            o.totalAmount,
          ),
      },
      {
        key: 'status',
        label: 'Status',
        render: (o: Order) => <StatusBadge status={o.status} />,
      },
      {
        key: 'orderDate',
        label: 'Date',
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
                Approve
              </button>
            )}
            {hasPermission('orders:cancel') && !['Cancelled', 'Delivered'].includes(o.status) && (
              <button
                onClick={() => setCancelId(o.id)}
                className="text-xs text-red-600 hover:underline"
              >
                Cancel
              </button>
            )}
          </div>
        ),
      },
    ],
    [hasPermission, approveOrder],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Orders</h1>
        {hasPermission('orders:create') && (
          <Link to="/orders/new" className="btn-primary">
            + New Order
          </Link>
        )}
      </div>

      <div className="flex gap-3">
        <input
          className="input max-w-xs"
          placeholder="Search by order # or customer…"
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
              {f.label}
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
        title="Cancel Order"
        message="Are you sure you want to cancel this order? This action cannot be undone."
        confirmLabel="Cancel Order"
        danger
        onConfirm={handleCancelConfirm}
        onCancel={() => setCancelId(null)}
      />
    </div>
  )
}
