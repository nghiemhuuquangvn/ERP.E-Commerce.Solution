import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { commissionPlansApi } from '@/api/commissions.api'
import type { CommissionPlan } from '@/api/commissions.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface PlanFormValues {
  name: string
  ratePercent: number
}

function CommissionPlanModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<PlanFormValues>({
    defaultValues: { name: '', ratePercent: 5 },
  })

  const mutation = useMutation({
    // Backend stores rateValue as a fraction (0.05 = 5%) — convert from the % the user types.
    mutationFn: (values: PlanFormValues) =>
      commissionPlansApi.create({ name: values.name, rateValue: Number(values.ratePercent) / 100 }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['commissionPlans'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('commission.plans.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('commission.plans.ratePercent')}</label>
            <input
              type="number"
              step="0.01"
              min={0}
              className="input"
              {...register('ratePercent', { required: true, valueAsNumber: true, min: 0.01 })}
            />
            {errors.ratePercent && (
              <p className="mt-1 text-xs text-red-600">{t('commission.plans.ratePercent')} must be greater than 0.</p>
            )}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('common.failedToSave')}</p>}
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

export function CommissionPlansPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [modalOpen, setModalOpen] = useState(false)
  const [confirmTarget, setConfirmTarget] = useState<CommissionPlan | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['commissionPlans'],
    queryFn: commissionPlansApi.list,
    staleTime: 30_000,
  })

  const canManage = hasPermission('sales_order:approve')

  const deactivateMutation = useMutation({
    mutationFn: (plan: CommissionPlan) => commissionPlansApi.deactivate(plan.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['commissionPlans'] })
      setConfirmTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'name', label: t('common.name') },
      {
        key: 'rateValue',
        label: t('commission.plans.ratePercent'),
        render: (p: CommissionPlan) => `${(p.rateValue * 100).toFixed(2)}%`,
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (p: CommissionPlan) => (
          <StatusBadge status={p.isActive ? 'Active' : 'Inactive'} label={p.isActive ? t('common.active') : t('common.inactive')} />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (p: CommissionPlan) =>
          canManage && p.isActive ? (
            <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(p)}>
              {t('common.deactivate')}
            </button>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('commission.plans.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalOpen(true)}>
            {t('commission.plans.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modalOpen && <CommissionPlanModal onClose={() => setModalOpen(false)} />}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('commission.plans.deactivateTitle')}
        message={t('commission.plans.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && deactivateMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
