import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { positionsApi } from '@/api/masterdata/positions.api'
import type { Position } from '@/api/masterdata/positions.api'
import { departmentsApi } from '@/api/masterdata/departments.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface PositionFormValues {
  title: string
  code: string
  description: string
  departmentId: string
}

function PositionModal({
  position,
  onClose,
}: {
  position: Position | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isEdit = !!position
  const { data: departmentsPage } = useQuery({
    queryKey: ['masterdata', 'departments', 'picker'],
    queryFn: () => departmentsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })
  const { register, handleSubmit, formState: { errors } } = useForm<PositionFormValues>({
    defaultValues: {
      title: position?.title ?? '',
      code: position?.code ?? '',
      description: position?.description ?? '',
      departmentId: position?.departmentId ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: PositionFormValues) => {
      if (isEdit) {
        return positionsApi.update(position!.id, {
          title: values.title,
          description: values.description.trim() || null,
        })
      }
      return positionsApi.create({
        title: values.title,
        code: values.code,
        description: values.description.trim() || null,
        departmentId: values.departmentId.trim() || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'positions'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.positions.editTitle') : t('masterdata.positions.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="position-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="position-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('masterdata.positions.title_field')} required error={errors.title ? t('common.required') : undefined}>
          <Input {...register('title', { required: true })} />
        </FormField>
        {!isEdit && (
          <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
            <Input {...register('code', { required: true })} />
          </FormField>
        )}
        <FormField label={t('masterdata.positions.description')}>
          <Textarea rows={3} {...register('description')} />
        </FormField>
        {!isEdit && (
          <FormField label={t('masterdata.positions.departmentIdOptional')}>
            <Select {...register('departmentId')}>
              <option value="">{t('common.none')}</option>
              {(departmentsPage?.items ?? []).map((d) => (
                <option key={d.id} value={d.id}>
                  {d.departmentName}
                </option>
              ))}
            </Select>
          </FormField>
        )}
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.positions.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function PositionsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [modalState, setModalState] = useState<{ open: boolean; position: Position | null }>({
    open: false,
    position: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<Position | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'positions', { page, pageSize }],
    queryFn: () => positionsApi.list({ page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:positions:manage')

  const toggleMutation = useMutation({
    mutationFn: (p: Position) =>
      p.isActive ? positionsApi.deactivate(p.id) : positionsApi.activate(p.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'positions'] })
      setConfirmTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'title', label: t('masterdata.positions.title_field') },
      { key: 'description', label: t('masterdata.positions.description') },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (p: Position) => (
          <StatusBadge
            status={p.isActive ? 'Active' : 'Inactive'}
            label={p.isActive ? t('masterdata.positions.active') : t('masterdata.positions.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (p: Position) =>
          canManage ? (
            <div className="flex gap-3">
              <Button variant="link" className="text-xs"
                onClick={() => setModalState({ open: true, position: p })}>
                {t('common.edit')}
              </Button>
              {p.isActive ? (
                <Button variant="link-danger" className="text-xs"
                  onClick={() => setConfirmTarget(p)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs"
                  onClick={() => toggleMutation.mutate(p)}>
                  {t('common.activate')}
                </Button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.positions.title')}</h1>
        {canManage && (
          <Button
            onClick={() => setModalState({ open: true, position: null })}>
            {t('masterdata.positions.add')}
          </Button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />

      {modalState.open && (
        <PositionModal
          position={modalState.position}
          onClose={() => setModalState({ open: false, position: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.positions.deactivateTitle')}
        message={t('masterdata.positions.deactivateConfirm', { title: confirmTarget?.title })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
