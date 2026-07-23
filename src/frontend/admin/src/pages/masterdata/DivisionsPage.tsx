import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { divisionsApi } from '@/api/masterdata/divisions.api'
import type { Division } from '@/api/masterdata/divisions.api'
import { branchesApi } from '@/api/masterdata/branches.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

interface DivisionFormValues {
  branchId: string
  name: string
  code: string
}

function DivisionModal({
  division,
  branches,
  defaultBranchId,
  onClose,
}: {
  division: Division | null
  branches: { id: string; name: string }[]
  defaultBranchId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!division

  const { register, handleSubmit, formState: { errors } } = useForm<DivisionFormValues>({
    defaultValues: {
      branchId: division?.branchId ?? defaultBranchId,
      name: division?.name ?? '',
      code: division?.code ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: DivisionFormValues) => {
      if (isEdit) {
        return divisionsApi.update(division!.id, {
          name: values.name,
          code: values.code,
          parentDivisionId: division!.parentDivisionId ?? null,
        })
      }
      return divisionsApi.create({
        branchId: values.branchId,
        name: values.name,
        code: values.code,
        parentDivisionId: null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'divisions'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.divisions.editTitle') : t('masterdata.divisions.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.divisions.branch')}</label>
              <select className="input" {...register('branchId', { required: true })}>
                <option value="">{t('common.select')}</option>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
              {errors.branchId && <p className="mt-1 text-xs text-red-600">{t('masterdata.divisions.branch')} is required.</p>}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" {...register('code', { required: true })} />
            {errors.code && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.divisions.failedToSave')}</p>
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

export function DivisionsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [branchId, setBranchId] = useState('')
  const [modalState, setModalState] = useState<{ open: boolean; division: Division | null }>({
    open: false,
    division: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<Division | null>(null)

  const { data: branchesPage } = useQuery({
    queryKey: ['masterdata', 'branches', 'picker'],
    queryFn: () => branchesApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'divisions', { branchId, page, pageSize }],
    queryFn: () => divisionsApi.list({ branchId: branchId || undefined, page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:divisions:manage')

  const toggleMutation = useMutation({
    mutationFn: (d: Division) => (d.isActive ? divisionsApi.deactivate(d.id) : divisionsApi.activate(d.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'divisions'] })
      setConfirmTarget(null)
    },
  })

  const branches = branchesPage?.items ?? []

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      {
        key: 'branchId',
        label: t('masterdata.divisions.branch'),
        render: (d: Division) => branches.find((b) => b.id === d.branchId)?.name ?? d.branchId,
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (d: Division) => (
          <StatusBadge
            status={d.isActive ? 'Active' : 'Inactive'}
            label={d.isActive ? t('masterdata.divisions.active') : t('masterdata.divisions.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (d: Division) =>
          canManage ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, division: d })}
              >
                {t('common.edit')}
              </button>
              {d.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(d)}>
                  {t('common.deactivate')}
                </button>
              ) : (
                <button
                  className="text-xs text-green-600 hover:underline"
                  onClick={() => toggleMutation.mutate(d)}
                >
                  {t('common.activate')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, branches, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.divisions.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, division: null })}>
            {t('masterdata.divisions.add')}
          </button>
        )}
      </div>

      <select
        className="input max-w-xs"
        value={branchId}
        onChange={(e) => {
          setBranchId(e.target.value)
          setPage(1)
        }}
      >
        <option value="">{t('masterdata.divisions.allBranches')}</option>
        {branches.map((b) => (
          <option key={b.id} value={b.id}>
            {b.name}
          </option>
        ))}
      </select>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modalState.open && (
        <DivisionModal
          division={modalState.division}
          branches={branches}
          defaultBranchId={branchId}
          onClose={() => setModalState({ open: false, division: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.divisions.deactivateTitle')}
        message={t('masterdata.divisions.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
