import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { workShiftsApi } from '@/api/hrm/workShifts.api'
import type { WorkShift } from '@/api/hrm/workShifts.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface WorkShiftFormValues {
  name: string
  startTime: string
  endTime: string
}

function WorkShiftModal({ shift, onClose }: { shift: WorkShift | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!shift
  const { register, handleSubmit, formState: { errors } } = useForm<WorkShiftFormValues>({
    defaultValues: {
      name: shift?.name ?? '',
      startTime: shift?.startTime ?? '',
      endTime: shift?.endTime ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: WorkShiftFormValues) =>
      isEdit
        ? workShiftsApi.update(shift!.id, values)
        : workShiftsApi.create(values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'workShifts'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('hrm.workShifts.editTitle') : t('hrm.workShifts.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('hrm.required')}</p>}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.workShifts.startTime')}</label>
              <input type="time" className="input" {...register('startTime', { required: true })} />
              {errors.startTime && <p className="mt-1 text-xs text-red-600">{t('hrm.required')}</p>}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.workShifts.endTime')}</label>
              <input type="time" className="input" {...register('endTime', { required: true })} />
              {errors.endTime && <p className="mt-1 text-xs text-red-600">{t('hrm.required')}</p>}
            </div>
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('hrm.workShifts.failedToSave')}</p>
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

export function WorkShiftsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [modalState, setModalState] = useState<{ open: boolean; shift: WorkShift | null }>({
    open: false,
    shift: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<WorkShift | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['hrm', 'workShifts'],
    queryFn: () => workShiftsApi.list(),
    staleTime: 30_000,
  })

  const canManage = hasPermission('hrm:shifts:manage')

  const deactivateMutation = useMutation({
    mutationFn: (s: WorkShift) => workShiftsApi.deactivate(s.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'workShifts'] })
      setConfirmTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'name', label: t('common.name') },
      { key: 'startTime', label: t('hrm.workShifts.startTime') },
      { key: 'endTime', label: t('hrm.workShifts.endTime') },
      { key: 'shiftHours', label: t('hrm.workShifts.shiftHours') },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (s: WorkShift) => (
          <StatusBadge
            status={s.isActive ? 'Active' : 'Inactive'}
            label={s.isActive ? t('masterdata.positions.active') : t('masterdata.positions.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (s: WorkShift) =>
          canManage ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, shift: s })}
              >
                {t('common.edit')}
              </button>
              {s.isActive && (
                <button
                  className="text-xs text-red-600 hover:underline"
                  onClick={() => setConfirmTarget(s)}
                >
                  {t('common.deactivate')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('hrm.workShifts.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, shift: null })}>
            {t('hrm.workShifts.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modalState.open && (
        <WorkShiftModal
          shift={modalState.shift}
          onClose={() => setModalState({ open: false, shift: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('hrm.workShifts.deactivateTitle')}
        message={t('hrm.workShifts.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && deactivateMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
