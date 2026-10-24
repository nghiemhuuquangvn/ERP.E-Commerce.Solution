import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { fiscalPeriodsApi } from '@/api/accounting/fiscal-periods.api'
import type { FiscalPeriod } from '@/api/accounting/fiscal-periods.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface FiscalPeriodFormValues {
  periodName: string
  startDate: string
  endDate: string
}

function OpenFiscalPeriodModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FiscalPeriodFormValues>({
    defaultValues: { periodName: '', startDate: '', endDate: '' },
  })

  const mutation = useMutation({
    mutationFn: (v: FiscalPeriodFormValues) =>
      fiscalPeriodsApi.open({
        periodName: v.periodName,
        startDate: v.startDate,
        endDate: v.endDate,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'fiscalPeriods'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('accounting.fiscalPeriods.openTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.fiscalPeriods.periodName')}
            </label>
            <input className="input" {...register('periodName', { required: true })} />
            {errors.periodName && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.fiscalPeriods.periodName')} is required.
              </p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.fiscalPeriods.startDate')}
            </label>
            <input type="date" className="input" {...register('startDate', { required: true })} />
            {errors.startDate && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.fiscalPeriods.startDate')} is required.
              </p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.fiscalPeriods.endDate')}
            </label>
            <input type="date" className="input" {...register('endDate', { required: true })} />
            {errors.endDate && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.fiscalPeriods.endDate')} is required.
              </p>
            )}
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('accounting.fiscalPeriods.failedToSave')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function FiscalPeriodsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:periods:manage')
  const queryClient = useQueryClient()
  const [modalOpen, setModalOpen] = useState(false)
  const [confirmTarget, setConfirmTarget] = useState<FiscalPeriod | null>(null)
  const [reopenTarget, setReopenTarget] = useState<FiscalPeriod | null>(null)

  // No pagination — FiscalPeriodsController.List (ListFiscalPeriodsQuery) returns the full List<FiscalPeriodDto>.
  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'fiscalPeriods'],
    queryFn: () => fiscalPeriodsApi.list(),
    staleTime: 30_000,
  })

  const closeMutation = useMutation({
    mutationFn: (p: FiscalPeriod) => fiscalPeriodsApi.close(p.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setConfirmTarget(null)
    },
  })

  const reopenMutation = useMutation({
    mutationFn: ({ period, reason }: { period: FiscalPeriod; reason: string }) => fiscalPeriodsApi.reopen(period.id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setReopenTarget(null)
    },
  })

  // The API explains refusals (an earlier open period, draft entries, an unbalanced trial balance ...).
  const detail = (e: unknown) =>
    (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? t('accounting.fiscalPeriods.failedToSave')

  const columns = useMemo(
    () => [
      { key: 'periodName', label: t('accounting.fiscalPeriods.periodName') },
      { key: 'startDate', label: t('accounting.fiscalPeriods.startDate') },
      { key: 'endDate', label: t('accounting.fiscalPeriods.endDate') },
      {
        key: 'status',
        label: t('common.status'),
        render: (p: FiscalPeriod) => <StatusBadge status={p.status} />,
      },
      {
        key: 'actions',
        label: '',
        render: (p: FiscalPeriod) =>
          !canManage ? null : p.status === 'Open' ? (
            <button
              className="text-xs text-red-600 hover:underline"
              onClick={() => setConfirmTarget(p)}
            >
              {t('accounting.fiscalPeriods.close')}
            </button>
          ) : (
            <button className="text-xs text-primary-600 hover:underline" onClick={() => setReopenTarget(p)}>
              {t('accounting.fiscalPeriods.reopen')}
            </button>
          ),
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.fiscalPeriods.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalOpen(true)}>
            {t('accounting.fiscalPeriods.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modalOpen && <OpenFiscalPeriodModal onClose={() => setModalOpen(false)} />}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('accounting.fiscalPeriods.closeTitle')}
        message={t('accounting.fiscalPeriods.closeConfirm', {
          periodName: confirmTarget?.periodName,
        })}
        confirmLabel={t('accounting.fiscalPeriods.close')}
        danger
        onConfirm={() => confirmTarget && closeMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
      {closeMutation.isError && <p className="text-sm text-red-600">{detail(closeMutation.error)}</p>}

      <ReasonDialog
        open={!!reopenTarget}
        title={t('accounting.fiscalPeriods.reopenTitle')}
        message={t('accounting.fiscalPeriods.reopenHint', { periodName: reopenTarget?.periodName })}
        confirmLabel={t('accounting.fiscalPeriods.reopen')}
        busy={reopenMutation.isPending}
        error={reopenMutation.isError ? detail(reopenMutation.error) : null}
        onConfirm={(reason) => reopenTarget && reopenMutation.mutate({ period: reopenTarget, reason })}
        onCancel={() => {
          setReopenTarget(null)
          reopenMutation.reset()
        }}
      />
    </div>
  )
}
