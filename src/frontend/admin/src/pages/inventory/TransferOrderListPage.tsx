import { useMemo } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { transferOrdersApi } from '@/api/transferOrders.api'
import type { TransferOrder } from '@/api/transferOrders.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'

export function TransferOrderListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['transferOrders'],
    queryFn: transferOrdersApi.list,
    staleTime: 15_000,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['transferOrders'] })
  const shipMutation = useMutation({ mutationFn: transferOrdersApi.ship, onSuccess: invalidate })
  const completeMutation = useMutation({ mutationFn: transferOrdersApi.complete, onSuccess: invalidate })
  const cancelMutation = useMutation({ mutationFn: transferOrdersApi.cancel, onSuccess: invalidate })

  const canManage = hasPermission('inventory:manage')

  const columns = useMemo(
    () => [
      { key: 'transferNumber', label: t('inventory.transfers.number'), className: 'font-mono text-xs' },
      { key: 'fromLocationId', label: t('inventory.transfers.from'), className: 'font-mono text-xs' },
      { key: 'toLocationId', label: t('inventory.transfers.to'), className: 'font-mono text-xs' },
      { key: 'lines', label: t('inventory.stocktakes.lineCount'), render: (o: TransferOrder) => o.lines.length },
      { key: 'status', label: t('common.status'), render: (o: TransferOrder) => <StatusBadge status={o.status} /> },
      { key: 'createdAt', label: t('common.date'), render: (o: TransferOrder) => format(new Date(o.createdAt), 'dd MMM yyyy') },
      {
        key: 'actions',
        label: '',
        render: (o: TransferOrder) =>
          canManage ? (
            <div className="flex gap-2">
              {o.status === 'Draft' && (
                <>
                  <button className="text-xs text-primary-600 hover:underline" onClick={() => shipMutation.mutate(o.transferOrderId)}>
                    {t('inventory.transfers.ship')}
                  </button>
                  <button className="text-xs text-red-600 hover:underline" onClick={() => cancelMutation.mutate(o.transferOrderId)}>
                    {t('common.cancel')}
                  </button>
                </>
              )}
              {o.status === 'InTransit' && (
                <button className="text-xs text-primary-600 hover:underline" onClick={() => completeMutation.mutate(o.transferOrderId)}>
                  {t('inventory.transfers.complete')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, shipMutation, completeMutation, cancelMutation, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('inventory.transfers.title')}</h1>
        {canManage && (
          <Link to="/inventory/transfers/new" className="btn-primary">
            {t('inventory.transfers.new')}
          </Link>
        )}
      </div>

      <DataTable
        columns={columns}
        data={(data ?? []).map((o) => ({ ...o, id: o.transferOrderId }))}
        isLoading={isLoading}
      />
    </div>
  )
}
