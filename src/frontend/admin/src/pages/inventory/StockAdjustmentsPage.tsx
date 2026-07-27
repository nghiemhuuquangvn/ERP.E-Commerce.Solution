import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { stockAdjustmentsApi } from '@/api/stockAdjustments.api'
import type { StockAdjustment, StockAdjustmentStatus } from '@/api/stockAdjustments.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'

const STATUS_OPTIONS: StockAdjustmentStatus[] = ['Pending', 'Approved', 'Rejected']

export function StockAdjustmentsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<StockAdjustmentStatus | ''>('Pending')
  const [rejectTarget, setRejectTarget] = useState<StockAdjustment | null>(null)
  const [rejectReason, setRejectReason] = useState('')
  const [actionError, setActionError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['stockAdjustments', status],
    queryFn: () => stockAdjustmentsApi.list(status || undefined),
    staleTime: 15_000,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['stockAdjustments'] })

  const approveMutation = useMutation({
    mutationFn: (id: string) => stockAdjustmentsApi.approve(id),
    onSuccess: invalidate,
    onError: () => setActionError(t('common.failedToSave')),
  })

  const rejectMutation = useMutation({
    mutationFn: () => stockAdjustmentsApi.reject(rejectTarget!.id, rejectReason),
    onSuccess: () => {
      invalidate()
      setRejectTarget(null)
      setRejectReason('')
    },
  })

  const canApprove = hasPermission('inventory:adjustment:approve')

  const columns = useMemo(
    () => [
      { key: 'productId', label: t('orders.product'), className: 'font-mono text-xs' },
      { key: 'quantityDelta', label: t('inventory.adjustment.quantityDelta'), className: 'text-right', render: (a: StockAdjustment) => (a.quantityDelta > 0 ? `+${a.quantityDelta}` : a.quantityDelta) },
      { key: 'reason', label: t('inventory.adjustment.reason') },
      { key: 'requestedBy', label: t('inventory.adjustment.requestedBy') },
      { key: 'requestedAt', label: t('common.date'), render: (a: StockAdjustment) => format(new Date(a.requestedAt), 'dd MMM yyyy HH:mm') },
      { key: 'status', label: t('common.status'), render: (a: StockAdjustment) => <StatusBadge status={a.status} /> },
      {
        key: 'actions',
        label: '',
        render: (a: StockAdjustment) =>
          canApprove && a.status === 'Pending' ? (
            <div className="flex gap-2">
              <button className="text-xs text-primary-600 hover:underline" onClick={() => approveMutation.mutate(a.id)}>
                {t('inventory.adjustment.approve')}
              </button>
              <button className="text-xs text-red-600 hover:underline" onClick={() => setRejectTarget(a)}>
                {t('inventory.adjustment.reject')}
              </button>
            </div>
          ) : null,
      },
    ],
    [canApprove, approveMutation, t],
  )

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('inventory.adjustment.title')}</h1>

      {actionError && <p className="text-sm text-red-600">{actionError}</p>}

      <div className="flex gap-1">
        <button
          onClick={() => setStatus('')}
          className={`rounded px-3 py-1.5 text-sm ${status === '' ? 'bg-primary-600 text-white' : 'bg-white border border-gray-300 text-gray-600 hover:bg-gray-50'}`}
        >
          {t('common.all')}
        </button>
        {STATUS_OPTIONS.map((s) => (
          <button
            key={s}
            onClick={() => setStatus(s)}
            className={`rounded px-3 py-1.5 text-sm ${status === s ? 'bg-primary-600 text-white' : 'bg-white border border-gray-300 text-gray-600 hover:bg-gray-50'}`}
          >
            {t(`inventory.adjustment.statuses.${s}`)}
          </button>
        ))}
      </div>

      <DataTable columns={columns} data={(data ?? []).map((a) => ({ ...a, id: a.id }))} isLoading={isLoading} />

      {rejectTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="card w-full max-w-md p-6">
            <h3 className="mb-4 text-base font-semibold text-gray-900">{t('inventory.adjustment.rejectTitle')}</h3>
            <textarea
              className="input mb-4"
              rows={3}
              placeholder={t('inventory.adjustment.rejectReasonPlaceholder')}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
            />
            <div className="flex justify-end gap-3">
              <button className="btn-outline" onClick={() => setRejectTarget(null)}>
                {t('common.cancel')}
              </button>
              <button
                className="btn-primary"
                disabled={!rejectReason.trim() || rejectMutation.isPending}
                onClick={() => rejectMutation.mutate()}
              >
                {t('inventory.adjustment.reject')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
