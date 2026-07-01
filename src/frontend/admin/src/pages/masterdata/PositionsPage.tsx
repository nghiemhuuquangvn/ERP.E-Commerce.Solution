import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { positionsApi } from '@/api/masterdata/positions.api'
import type { Position } from '@/api/masterdata/positions.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

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
  const isEdit = !!position
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
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.positions.editTitle') : t('masterdata.positions.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.positions.title_field')}</label>
            <input className="input" {...register('title', { required: true })} />
            {errors.title && <p className="mt-1 text-xs text-red-600">{t('masterdata.positions.title_field')} is required.</p>}
          </div>
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
              <input className="input" {...register('code', { required: true })} />
              {errors.code && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.positions.description')}</label>
            <textarea className="input" rows={3} {...register('description')} />
          </div>
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('masterdata.positions.departmentIdOptional')}
              </label>
              <input className="input" {...register('departmentId')} />
            </div>
          )}
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.positions.failedToSave')}</p>
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
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, position: p })}
              >
                {t('common.edit')}
              </button>
              {p.isActive ? (
                <button
                  className="text-xs text-red-600 hover:underline"
                  onClick={() => setConfirmTarget(p)}
                >
                  {t('common.deactivate')}
                </button>
              ) : (
                <button
                  className="text-xs text-green-600 hover:underline"
                  onClick={() => toggleMutation.mutate(p)}
                >
                  {t('common.activate')}
                </button>
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
          <button
            className="btn-primary"
            onClick={() => setModalState({ open: true, position: null })}
          >
            {t('masterdata.positions.add')}
          </button>
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
