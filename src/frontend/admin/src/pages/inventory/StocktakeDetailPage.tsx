import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { stocktakesApi } from '@/api/stocktakes.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, Input, Spinner, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

type Confirming = 'complete' | 'approve' | 'cancel' | null

export function StocktakeDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [counts, setCounts] = useState<Record<string, string>>({})
  const [confirming, setConfirming] = useState<Confirming>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const { data: stocktake, isLoading } = useQuery({
    queryKey: ['stocktakes', id],
    queryFn: () => stocktakesApi.get(id!),
    enabled: Boolean(id),
  })

  const after = {
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stocktakes', id] })
      toast.success(t('common.saved'))
      setConfirming(null)
      setActionError(null)
    },
    onError: (e: unknown) => {
      setConfirming(null)
      setActionError(serverMessage(e, t('common.failedToSave')))
    },
  }

  const recordCountMutation = useMutation({
    mutationFn: (productId: string) => stocktakesApi.recordCount(id!, productId, Number(counts[productId])),
    ...after,
  })
  const completeMutation = useMutation({ mutationFn: () => stocktakesApi.complete(id!), ...after })
  const approveMutation = useMutation({ mutationFn: () => stocktakesApi.approve(id!), ...after })
  const cancelMutation = useMutation({ mutationFn: () => stocktakesApi.cancel(id!), ...after })

  if (isLoading) return <Spinner />
  if (!stocktake) return <EmptyState variant="error" title={t('inventory.stocktakes.notFound')} />

  const canManage = hasPermission('inventory:manage')
  const canApprove = hasPermission('inventory:stocktake:approve')
  const counting = stocktake.status === 'InProgress'
  const number = stocktake.stocktakeNumber

  const columns = [
    { key: 'productId', label: t('orders.product'), className: 'font-mono text-xs' },
    { key: 'systemQuantity', label: t('inventory.stocktakes.systemQuantity'), align: 'right' as const },
    {
      key: 'countedQuantity',
      label: t('inventory.stocktakes.countedQuantity'),
      align: 'right' as const,
      render: (line: (typeof stocktake.lines)[number]) =>
        counting ? (
          <Input
            type="number"
            className="ml-auto w-24 text-right"
            step="any"
            aria-label={`${t('inventory.stocktakes.countedQuantity')} ${line.productId}`}
            placeholder={String(line.countedQuantity ?? '')}
            value={counts[line.productId] ?? ''}
            onChange={(e) => setCounts({ ...counts, [line.productId]: e.target.value })}
          />
        ) : (
          (line.countedQuantity ?? '—')
        ),
    },
    {
      key: 'variance',
      label: t('inventory.stocktakes.variance'),
      align: 'right' as const,
      render: (line: (typeof stocktake.lines)[number]) => (
        <span className={`tabular-nums ${line.variance > 0 ? 'font-medium text-info-soft-text' : line.variance < 0 ? 'font-medium text-danger-soft-text' : ''}`}>
          {line.variance > 0 ? `+${line.variance}` : line.variance}
        </span>
      ),
    },
    ...(counting
      ? [
          {
            key: 'record',
            label: '',
            render: (line: (typeof stocktake.lines)[number]) => (
              <Button
                variant="link"
                className="text-xs"
                disabled={!counts[line.productId]?.trim()}
                loading={recordCountMutation.isPending && recordCountMutation.variables === line.productId}
                onClick={() => recordCountMutation.mutate(line.productId)}
              >
                {t('inventory.stocktakes.recordCount')}
              </Button>
            ),
          },
        ]
      : []),
  ]

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/inventory/stocktakes" className="text-sm text-ink-secondary hover:text-ink">
          {t('inventory.stocktakes.backToStocktakes')}
        </Link>
        <h1 className="text-xl font-semibold">{number}</h1>
        <StatusBadge status={stocktake.status} />
      </div>

      {actionError && <Alert variant="danger" onClose={() => setActionError(null)}>{actionError}</Alert>}

      <div className="flex gap-2">
        {canManage && counting && (
          <Button onClick={() => setConfirming('complete')}>{t('inventory.stocktakes.complete')}</Button>
        )}
        {canApprove && stocktake.status === 'PendingApproval' && (
          <Button onClick={() => setConfirming('approve')}>{t('inventory.stocktakes.approve')}</Button>
        )}
        {canManage && stocktake.status !== 'Completed' && stocktake.status !== 'Cancelled' && (
          <Button variant="outline" onClick={() => setConfirming('cancel')}>{t('inventory.stocktakes.cancel')}</Button>
        )}
      </div>

      <DataTable columns={columns} data={stocktake.lines.map((l) => ({ ...l, id: l.productId }))} isLoading={false} />

      <ConfirmDialog
        open={confirming === 'complete'}
        title={t('inventory.stocktakes.completeTitle')}
        message={t('inventory.stocktakes.completeConfirm', { number })}
        confirmLabel={t('inventory.stocktakes.complete')}
        onConfirm={() => completeMutation.mutate()}
        onCancel={() => setConfirming(null)}
      />
      <ConfirmDialog
        open={confirming === 'approve'}
        title={t('inventory.stocktakes.approveTitle')}
        message={t('inventory.stocktakes.approveConfirm', { number })}
        confirmLabel={t('inventory.stocktakes.approve')}
        danger
        onConfirm={() => approveMutation.mutate()}
        onCancel={() => setConfirming(null)}
      />
      <ConfirmDialog
        open={confirming === 'cancel'}
        title={t('inventory.stocktakes.cancelTitle')}
        message={t('inventory.stocktakes.cancelConfirm', { number })}
        confirmLabel={t('inventory.stocktakes.cancel')}
        danger
        onConfirm={() => cancelMutation.mutate()}
        onCancel={() => setConfirming(null)}
      />
    </div>
  )
}
