import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { stockAdjustmentsApi } from '@/api/stockAdjustments.api'
import type { StockAdjustment, StockAdjustmentStatus } from '@/api/stockAdjustments.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'
import { Alert, Button, EmptyState, FormField, Modal, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const STATUS_OPTIONS: StockAdjustmentStatus[] = ['Pending', 'Approved', 'Rejected']

function RejectModal({ adjustment, onClose }: { adjustment: StockAdjustment; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<{ reason: string }>({ defaultValues: { reason: '' } })

  const mutation = useMutation({
    mutationFn: (values: { reason: string }) => stockAdjustmentsApi.reject(adjustment.id, values.reason.trim()),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stockAdjustments'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('inventory.adjustment.rejectTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="reject-adjustment-form" variant="danger" loading={mutation.isPending}>{t('inventory.adjustment.reject')}</Button>
        </>
      }
    >
      <form id="reject-adjustment-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('inventory.adjustment.reason')} required error={errors.reason ? t('common.required') : undefined}>
          <Textarea rows={3} placeholder={t('inventory.adjustment.rejectReasonPlaceholder')} {...register('reason', { required: true, validate: (v) => v.trim().length > 0 })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function StockAdjustmentsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [status, setStatus] = useState<StockAdjustmentStatus | ''>('Pending')
  const [rejectTarget, setRejectTarget] = useState<StockAdjustment | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['stockAdjustments', status],
    queryFn: () => stockAdjustmentsApi.list(status || undefined),
    staleTime: 15_000,
  })

  const approveMutation = useMutation({
    mutationFn: (id: string) => stockAdjustmentsApi.approve(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stockAdjustments'] })
      toast.success(t('common.saved'))
    },
    onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))),
  })

  const canApprove = hasPermission('inventory:adjustment:approve')

  const columns = useMemo(
    () => [
      { key: 'productId', label: t('orders.product'), className: 'font-mono text-xs' },
      {
        key: 'quantityDelta',
        label: t('inventory.adjustment.quantityDelta'),
        align: 'right' as const,
        render: (a: StockAdjustment) => (
          <span className={`tabular-nums font-medium ${a.quantityDelta > 0 ? 'text-success-soft-text' : 'text-danger-soft-text'}`}>
            {a.quantityDelta > 0 ? `+${a.quantityDelta}` : a.quantityDelta}
          </span>
        ),
      },
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
              <Button variant="link" className="text-xs" onClick={() => approveMutation.mutate(a.id)}>
                {t('inventory.adjustment.approve')}
              </Button>
              <Button variant="link-danger" className="text-xs" onClick={() => setRejectTarget(a)}>
                {t('inventory.adjustment.reject')}
              </Button>
            </div>
          ) : null,
      },
    ],
    [canApprove, approveMutation, t],
  )

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('inventory.adjustment.title')}</h1>

      <div className="flex gap-1" role="group" aria-label={t('common.status')}>
        <Button size="sm" variant={status === '' ? 'primary' : 'outline'} aria-pressed={status === ''} onClick={() => setStatus('')}>
          {t('common.all')}
        </Button>
        {STATUS_OPTIONS.map((s) => (
          <Button key={s} size="sm" variant={status === s ? 'primary' : 'outline'} aria-pressed={status === s} onClick={() => setStatus(s)}>
            {t(`inventory.adjustment.statuses.${s}`)}
          </Button>
        ))}
      </div>

      <DataTable
        columns={columns}
        data={(data ?? []).map((a) => ({ ...a, id: a.id }))}
        isLoading={isLoading}
        emptyState={
          status ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => setStatus('')}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />

      {rejectTarget && <RejectModal adjustment={rejectTarget} onClose={() => setRejectTarget(null)} />}
    </div>
  )
}
