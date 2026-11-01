import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { transferOrdersApi } from '@/api/transferOrders.api'
import type { TransferOrder } from '@/api/transferOrders.api'
import { locationsApi } from '@/api/locations.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'
import { Button, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

type Step = { kind: 'ship' | 'complete' | 'cancel'; order: TransferOrder }

export function TransferOrderListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [step, setStep] = useState<Step | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['transferOrders'],
    queryFn: transferOrdersApi.list,
    staleTime: 15_000,
  })
  const { data: locations } = useQuery({ queryKey: ['locations'], queryFn: () => locationsApi.list(), staleTime: 60_000 })
  const locationCode = (id: string) => (locations ?? []).find((l) => l.locationId === id)?.locationCode ?? id

  const after = {
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['transferOrders'] })
      toast.success(t('common.saved'))
      setStep(null)
    },
    onError: (e: unknown) => {
      setStep(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
    },
  }
  const shipMutation = useMutation({ mutationFn: transferOrdersApi.ship, ...after })
  const completeMutation = useMutation({ mutationFn: transferOrdersApi.complete, ...after })
  const cancelMutation = useMutation({ mutationFn: transferOrdersApi.cancel, ...after })

  const canManage = hasPermission('inventory:manage')

  const columns = useMemo(
    () => [
      { key: 'transferNumber', label: t('inventory.transfers.number'), className: 'font-mono text-xs' },
      { key: 'fromLocationId', label: t('inventory.transfers.from'), className: 'font-mono text-xs', render: (o: TransferOrder) => locationCode(o.fromLocationId) },
      { key: 'toLocationId', label: t('inventory.transfers.to'), className: 'font-mono text-xs', render: (o: TransferOrder) => locationCode(o.toLocationId) },
      { key: 'lines', label: t('inventory.stocktakes.lineCount'), align: 'right' as const, render: (o: TransferOrder) => o.lines.length },
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
                  <Button variant="link" className="text-xs" onClick={() => setStep({ kind: 'ship', order: o })}>
                    {t('inventory.transfers.ship')}
                  </Button>
                  <Button variant="link-danger" className="text-xs" onClick={() => setStep({ kind: 'cancel', order: o })}>
                    {t('common.cancel')}
                  </Button>
                </>
              )}
              {o.status === 'InTransit' && (
                <Button variant="link" className="text-xs" onClick={() => setStep({ kind: 'complete', order: o })}>
                  {t('inventory.transfers.complete')}
                </Button>
              )}
            </div>
          ) : null,
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [canManage, locations, t],
  )

  const run = () => {
    if (!step) return
    const id = step.order.transferOrderId
    if (step.kind === 'ship') shipMutation.mutate(id)
    else if (step.kind === 'complete') completeMutation.mutate(id)
    else cancelMutation.mutate(id)
  }

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

      <ConfirmDialog
        open={!!step}
        title={step ? t(`inventory.transfers.${step.kind}Title`) : ''}
        message={step ? t(`inventory.transfers.${step.kind}Confirm`, { number: step.order.transferNumber }) : ''}
        confirmLabel={step ? (step.kind === 'cancel' ? t('inventory.transfers.cancelTitle') : t(`inventory.transfers.${step.kind}Title`)) : undefined}
        danger={step?.kind === 'cancel'}
        onConfirm={run}
        onCancel={() => setStep(null)}
      />
    </div>
  )
}
