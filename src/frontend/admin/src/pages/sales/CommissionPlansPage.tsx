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
import { Alert, Button, EmptyState, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface PlanFormValues {
  name: string
  ratePercent: number
}

function CommissionPlanModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<PlanFormValues>({
    defaultValues: { name: '', ratePercent: 5 },
  })

  const mutation = useMutation({
    // Backend stores rateValue as a fraction (0.05 = 5%) — convert from the % the user types.
    mutationFn: (values: PlanFormValues) =>
      commissionPlansApi.create({ name: values.name, rateValue: Number(values.ratePercent) / 100 }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['commissionPlans'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('commission.plans.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="commission-plan-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="commission-plan-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField
          label={t('commission.plans.ratePercent')}
          required
          hint={t('commission.plans.rateHint')}
          error={errors.ratePercent ? t('commission.plans.rateRange') : undefined}
        >
          <Input
            type="number"
            step="0.01"
            min={0}
            max={100}
            {...register('ratePercent', { required: true, valueAsNumber: true, min: 0.01, max: 100 })}
          />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function CommissionPlansPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
      setConfirmTarget(null)
    },
    onError: (e) => {
      setConfirmTarget(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
    },
  })

  const columns = useMemo(
    () => [
      { key: 'name', label: t('common.name') },
      {
        key: 'rateValue',
        label: t('commission.plans.ratePercent'),
        align: 'right' as const,
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
            <Button variant="link-danger" className="text-xs" aria-label={`${t('common.deactivate')} ${p.name}`} onClick={() => setConfirmTarget(p)}>
              {t('common.deactivate')}
            </Button>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('commission.plans.title')}</h1>
        {canManage && <Button onClick={() => setModalOpen(true)}>{t('commission.plans.add')}</Button>}
      </div>

      <DataTable
        columns={columns}
        data={data ?? []}
        isLoading={isLoading}
        emptyState={
          <EmptyState
            title={t('commission.plans.empty')}
            action={canManage ? <Button onClick={() => setModalOpen(true)}>{t('commission.plans.add')}</Button> : undefined}
          />
        }
      />

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
