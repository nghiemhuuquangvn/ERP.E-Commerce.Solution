import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { teamsApi } from '@/api/masterdata/teams.api'
import type { Team } from '@/api/masterdata/teams.api'
import { departmentsApi } from '@/api/masterdata/departments.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

interface TeamFormValues {
  departmentId: string
  name: string
  code: string
}

function TeamModal({
  team,
  departments,
  defaultDepartmentId,
  onClose,
}: {
  team: Team | null
  departments: { id: string; departmentName: string }[]
  defaultDepartmentId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!team

  const { register, handleSubmit, formState: { errors } } = useForm<TeamFormValues>({
    defaultValues: {
      departmentId: team?.departmentId ?? defaultDepartmentId,
      name: team?.name ?? '',
      code: team?.code ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: TeamFormValues) => {
      if (isEdit) {
        return teamsApi.update(team!.id, {
          name: values.name,
          code: values.code,
          leaderEmployeeId: team!.leaderEmployeeId ?? null,
        })
      }
      return teamsApi.create({
        departmentId: values.departmentId,
        name: values.name,
        code: values.code,
        leaderEmployeeId: null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'teams'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.teams.editTitle') : t('masterdata.teams.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.teams.department')}</label>
              <select className="input" {...register('departmentId', { required: true })}>
                <option value="">{t('common.select')}</option>
                {departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.departmentName}
                  </option>
                ))}
              </select>
              {errors.departmentId && (
                <p className="mt-1 text-xs text-red-600">{t('masterdata.teams.department')} is required.</p>
              )}
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
            <p className="text-xs text-red-600">{t('masterdata.teams.failedToSave')}</p>
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

export function TeamsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [departmentId, setDepartmentId] = useState('')
  const [modalState, setModalState] = useState<{ open: boolean; team: Team | null }>({
    open: false,
    team: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<Team | null>(null)

  const { data: departmentsPage } = useQuery({
    queryKey: ['masterdata', 'departments', 'picker'],
    queryFn: () => departmentsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'teams', { departmentId, page, pageSize }],
    queryFn: () => teamsApi.list({ departmentId: departmentId || undefined, page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:teams:manage')

  const toggleMutation = useMutation({
    mutationFn: (team: Team) => (team.isActive ? teamsApi.deactivate(team.id) : teamsApi.activate(team.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'teams'] })
      setConfirmTarget(null)
    },
  })

  const departments = departmentsPage?.items ?? []

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      {
        key: 'departmentId',
        label: t('masterdata.teams.department'),
        render: (team: Team) => departments.find((d) => d.id === team.departmentId)?.departmentName ?? team.departmentId,
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (team: Team) => (
          <StatusBadge
            status={team.isActive ? 'Active' : 'Inactive'}
            label={team.isActive ? t('masterdata.teams.active') : t('masterdata.teams.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (team: Team) =>
          canManage ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, team })}
              >
                {t('common.edit')}
              </button>
              {team.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(team)}>
                  {t('common.deactivate')}
                </button>
              ) : (
                <button
                  className="text-xs text-green-600 hover:underline"
                  onClick={() => toggleMutation.mutate(team)}
                >
                  {t('common.activate')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, departments, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.teams.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, team: null })}>
            {t('masterdata.teams.add')}
          </button>
        )}
      </div>

      <select
        className="input max-w-xs"
        value={departmentId}
        onChange={(e) => {
          setDepartmentId(e.target.value)
          setPage(1)
        }}
      >
        <option value="">{t('masterdata.teams.allDepartments')}</option>
        {departments.map((d) => (
          <option key={d.id} value={d.id}>
            {d.departmentName}
          </option>
        ))}
      </select>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modalState.open && (
        <TeamModal
          team={modalState.team}
          departments={departments}
          defaultDepartmentId={departmentId}
          onClose={() => setModalState({ open: false, team: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.teams.deactivateTitle')}
        message={t('masterdata.teams.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
