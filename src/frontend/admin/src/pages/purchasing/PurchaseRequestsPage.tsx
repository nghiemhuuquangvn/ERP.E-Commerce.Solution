import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { purchaseRequestsApi } from '@/api/purchaseRequests.api'
import type { PurchaseRequestStatus } from '@/api/purchaseRequests.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'

const STATUS_TABS: (PurchaseRequestStatus | 'All')[] = ['All', 'Suggested', 'Dismissed', 'Converted']

export function PurchaseRequestsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [statusFilter, setStatusFilter] = useState<PurchaseRequestStatus | 'All'>('Suggested')

  const { data: requests, isLoading } = useQuery({
    queryKey: ['purchaseRequests', statusFilter],
    queryFn: () => purchaseRequestsApi.list(statusFilter === 'All' ? undefined : statusFilter),
  })

  const dismissMutation = useMutation({
    mutationFn: (id: string) => purchaseRequestsApi.dismiss(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchaseRequests'] }),
  })

  const convertMutation = useMutation({
    mutationFn: (id: string) => purchaseRequestsApi.convert(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchaseRequests'] }),
  })

  const canManage = hasPermission('purchase_request:manage')

  const rows = (requests ?? []).map((r) => ({ ...r, id: r.id }))

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold">{t('purchasing.requests.title')}</h1>
        <p className="mt-1 max-w-2xl text-sm text-gray-500">{t('purchasing.requests.description')}</p>
      </div>

      <div className="flex gap-1 border-b border-gray-200">
        {STATUS_TABS.map((s) => (
          <button
            key={s}
            onClick={() => setStatusFilter(s)}
            className={`px-4 py-2 text-sm font-medium ${
              statusFilter === s ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {t(`purchasing.requests.status.${s}`)}
          </button>
        ))}
      </div>

      <DataTable
        data={rows}
        isLoading={isLoading}
        columns={[
          { key: 'productName', label: t('orders.product') },
          { key: 'currentQuantity', label: t('inventory.costHistory.quantity'), className: 'text-right' },
          { key: 'reorderPoint', label: t('inventory.reorderPoint'), className: 'text-right' },
          { key: 'suggestedQuantity', label: t('purchasing.requests.suggestedQuantity'), className: 'text-right' },
          { key: 'status', label: t('common.status'), render: (r) => <StatusBadge status={r.status} /> },
          { key: 'createdAt', label: t('common.date'), render: (r) => format(new Date(r.createdAt), 'dd MMM yyyy') },
          {
            key: 'actions',
            label: '',
            className: 'text-right',
            render: (r) =>
              canManage && r.status === 'Suggested' ? (
                <div className="flex justify-end gap-2">
                  <button
                    className="btn-outline text-xs"
                    disabled={dismissMutation.isPending}
                    onClick={() => dismissMutation.mutate(r.id)}
                  >
                    {t('purchasing.requests.dismiss')}
                  </button>
                  <button
                    className="btn-primary text-xs"
                    disabled={convertMutation.isPending}
                    onClick={() => convertMutation.mutate(r.id)}
                  >
                    {t('purchasing.requests.convert')}
                  </button>
                </div>
              ) : null,
          },
        ]}
      />
    </div>
  )
}
