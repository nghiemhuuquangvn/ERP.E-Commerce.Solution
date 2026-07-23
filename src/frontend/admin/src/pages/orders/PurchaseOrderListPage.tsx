import { useState, useMemo, useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { purchaseOrdersApi, PurchaseOrderStatus } from '@/api/purchaseOrders.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'
import type { PurchaseOrder } from '@/api/purchaseOrders.api'

const STATUS_FILTERS: { labelKey: string; value: PurchaseOrderStatus | '' }[] = [
  { labelKey: 'common.all', value: '' },
  { labelKey: 'purchaseOrders.statusFilters.draft', value: 'DRAFT' },
  { labelKey: 'purchaseOrders.statusFilters.submitted', value: 'SUBMITTED' },
  { labelKey: 'purchaseOrders.statusFilters.approved', value: 'APPROVED' },
  { labelKey: 'purchaseOrders.statusFilters.ordered', value: 'ORDERED' },
  { labelKey: 'purchaseOrders.statusFilters.partiallyReceived', value: 'PARTIALLY_RECEIVED' },
  { labelKey: 'purchaseOrders.statusFilters.received', value: 'RECEIVED' },
  { labelKey: 'purchaseOrders.statusFilters.cancelled', value: 'CANCELLED' },
]

export function PurchaseOrderListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState<PurchaseOrderStatus | ''>('')
  const [cancelId, setCancelId] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['purchaseOrders', { page, pageSize, status }],
    queryFn: () =>
      purchaseOrdersApi.list({ pageNumber: page, pageSize, status: status || undefined }),
    staleTime: 30_000,
  })

  const approveMutation = useMutation({
    mutationFn: purchaseOrdersApi.approve,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchaseOrders'] }),
  })

  const cancelMutation = useMutation({
    mutationFn: (id: string) => purchaseOrdersApi.cancel(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['purchaseOrders'] })
      setCancelId(null)
    },
  })

  const handleStatusChange = useCallback(
    (value: PurchaseOrderStatus | '') => {
      setStatus(value)
      setPage(1)
    },
    [setPage],
  )

  const handleCancelConfirm = useCallback(() => {
    if (cancelId) cancelMutation.mutate(cancelId)
  }, [cancelId, cancelMutation])

  const { mutate: approvePo } = approveMutation
  const columns = useMemo(
    () => [
      {
        key: 'poNumber',
        label: t('purchaseOrders.poNumber'),
        render: (po: PurchaseOrder) => (
          <Link to={`/orders/purchase/${po.id}`} className="text-primary-600 hover:underline">
            {po.poNumber}
          </Link>
        ),
      },
      {
        key: 'supplier',
        label: t('purchaseOrders.supplier'),
        render: (po: PurchaseOrder) => po.supplier?.name ?? po.supplierId,
      },
      {
        key: 'totalAmount',
        label: t('orders.total'),
        render: (po: PurchaseOrder) =>
          new Intl.NumberFormat('en-US', { style: 'currency', currency: po.currency || 'USD' }).format(
            po.totalAmount,
          ),
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (po: PurchaseOrder) => <StatusBadge status={po.status} />,
      },
      {
        key: 'orderDate',
        label: t('common.date'),
        render: (po: PurchaseOrder) => format(new Date(po.orderDate), 'dd MMM yyyy'),
      },
      {
        key: 'actions',
        label: '',
        render: (po: PurchaseOrder) => (
          <div className="flex gap-2">
            {hasPermission('purchase_order:approve') && po.status === 'SUBMITTED' && (
              <button
                onClick={() => approvePo(po.id)}
                className="text-xs text-primary-600 hover:underline"
              >
                {t('orders.approve')}
              </button>
            )}
            {hasPermission('purchase_order:cancel') && !['CANCELLED', 'RECEIVED'].includes(po.status) && (
              <button
                onClick={() => setCancelId(po.id)}
                className="text-xs text-red-600 hover:underline"
              >
                {t('orders.cancel')}
              </button>
            )}
          </div>
        ),
      },
    ],
    [hasPermission, approvePo, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('purchaseOrders.title')}</h1>
        {hasPermission('purchase_order:create') && (
          <Link to="/orders/purchase/new" className="btn-primary">
            {t('purchaseOrders.newPurchaseOrder')}
          </Link>
        )}
      </div>

      <div className="flex gap-1">
        {STATUS_FILTERS.map((f) => (
          <button
            key={f.value}
            onClick={() => handleStatusChange(f.value as PurchaseOrderStatus | '')}
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

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />

      <ConfirmDialog
        open={cancelId !== null}
        title={t('purchaseOrders.cancelTitle')}
        message={t('purchaseOrders.cancelMessage')}
        confirmLabel={t('orders.cancelOrderConfirm')}
        danger
        onConfirm={handleCancelConfirm}
        onCancel={() => setCancelId(null)}
      />
    </div>
  )
}
