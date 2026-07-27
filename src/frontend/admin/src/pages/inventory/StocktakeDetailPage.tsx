import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import axios from 'axios'
import { stocktakesApi } from '@/api/stocktakes.api'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

export function StocktakeDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [counts, setCounts] = useState<Record<string, string>>({})
  const [actionError, setActionError] = useState<string | null>(null)

  const { data: stocktake, isLoading } = useQuery({
    queryKey: ['stocktakes', id],
    queryFn: () => stocktakesApi.get(id!),
    enabled: Boolean(id),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['stocktakes', id] })

  const runAction = (fn: () => Promise<unknown>) => {
    setActionError(null)
    fn()
      .then(invalidate)
      .catch((err) => {
        const message = axios.isAxiosError(err) ? (err.response?.data as { message?: string })?.message : undefined
        setActionError(message ?? t('common.failedToSave'))
      })
  }

  const recordCountMutation = useMutation({
    mutationFn: (productId: string) =>
      stocktakesApi.recordCount(id!, productId, Number(counts[productId])),
  })

  const completeMutation = useMutation({ mutationFn: () => stocktakesApi.complete(id!) })
  const approveMutation = useMutation({ mutationFn: () => stocktakesApi.approve(id!) })
  const cancelMutation = useMutation({ mutationFn: () => stocktakesApi.cancel(id!) })

  if (isLoading) return <div className="text-gray-400">{t('common.loading')}</div>
  if (!stocktake) return <div className="text-gray-400">{t('inventory.stocktakes.notFound')}</div>

  const canManage = hasPermission('inventory:manage')
  const canApprove = hasPermission('inventory:stocktake:approve')

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/inventory/stocktakes" className="text-sm text-gray-500 hover:text-gray-700">
          {t('inventory.stocktakes.backToStocktakes')}
        </Link>
        <h1 className="text-xl font-semibold">{stocktake.stocktakeNumber}</h1>
        <StatusBadge status={stocktake.status} />
      </div>

      {actionError && <p className="text-sm text-red-600">{actionError}</p>}

      <div className="flex gap-2">
        {canManage && stocktake.status === 'InProgress' && (
          <button className="btn-primary" onClick={() => runAction(() => completeMutation.mutateAsync())}>
            {t('inventory.stocktakes.complete')}
          </button>
        )}
        {canApprove && stocktake.status === 'PendingApproval' && (
          <button className="btn-primary" onClick={() => runAction(() => approveMutation.mutateAsync())}>
            {t('inventory.stocktakes.approve')}
          </button>
        )}
        {canManage && stocktake.status !== 'Completed' && stocktake.status !== 'Cancelled' && (
          <button className="btn-outline text-red-600" onClick={() => runAction(() => cancelMutation.mutateAsync())}>
            {t('inventory.stocktakes.cancel')}
          </button>
        )}
      </div>

      <div className="card p-5">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
              <th className="pb-2">{t('orders.product')}</th>
              <th className="pb-2 text-right">{t('inventory.stocktakes.systemQuantity')}</th>
              <th className="pb-2 text-right">{t('inventory.stocktakes.countedQuantity')}</th>
              <th className="pb-2 text-right">{t('inventory.stocktakes.variance')}</th>
              {stocktake.status === 'InProgress' && <th className="pb-2" />}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {stocktake.lines.map((line) => (
              <tr key={line.productId}>
                <td className="py-2 font-mono text-xs">{line.productId}</td>
                <td className="py-2 text-right">{line.systemQuantity}</td>
                <td className="py-2 text-right">
                  {stocktake.status === 'InProgress' ? (
                    <input
                      type="number"
                      className="input w-24 text-right"
                      placeholder={String(line.countedQuantity ?? '')}
                      value={counts[line.productId] ?? ''}
                      onChange={(e) => setCounts({ ...counts, [line.productId]: e.target.value })}
                    />
                  ) : (
                    line.countedQuantity ?? '—'
                  )}
                </td>
                <td className={`py-2 text-right ${line.variance !== 0 ? 'font-medium text-yellow-700' : ''}`}>{line.variance}</td>
                {stocktake.status === 'InProgress' && (
                  <td className="py-2 text-right">
                    <button
                      disabled={!counts[line.productId]?.trim()}
                      className="text-xs text-primary-600 hover:underline"
                      onClick={() => runAction(() => recordCountMutation.mutateAsync(line.productId))}
                    >
                      {t('inventory.stocktakes.recordCount')}
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
