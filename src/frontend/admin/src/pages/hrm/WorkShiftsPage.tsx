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
import { Alert, Button, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface WorkShiftFormValues {
  name: string
  startTime: string
  endTime: string
}

function WorkShiftModal({ shift, onClose }: { shift: WorkShift | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('hrm.workShifts.editTitle') : t('hrm.workShifts.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="work-shift-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="work-shift-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.workShifts.startTime')} required error={errors.startTime ? t('common.required') : undefined}>
            <Input type="time" {...register('startTime', { required: true })} />
          </FormField>
          <FormField label={t('hrm.workShifts.endTime')} required error={errors.endTime ? t('common.required') : undefined}>
            <Input type="time" {...register('endTime', { required: true })} />
          </FormField>
        </div>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.workShifts.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
    queryFn: ({ signal }) => workShiftsApi.list({}, signal),
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
              <Button variant="link" className="text-xs"
                onClick={() => setModalState({ open: true, shift: s })}>
                {t('common.edit')}
              </Button>
              {s.isActive && (
                <Button variant="link-danger" className="text-xs"
                  onClick={() => setConfirmTarget(s)}>
                  {t('common.deactivate')}
                </Button>
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
          <Button onClick={() => setModalState({ open: true, shift: null })}>
            {t('hrm.workShifts.add')}
          </Button>
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
