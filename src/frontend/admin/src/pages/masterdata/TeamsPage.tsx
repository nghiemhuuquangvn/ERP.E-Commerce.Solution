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
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.teams.editTitle') : t('masterdata.teams.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="team-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="team-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField label={t('masterdata.teams.department')} required error={errors.departmentId ? t('common.required') : undefined}>
            <Select {...register('departmentId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.departmentName}
                </option>
              ))}
            </Select>
          </FormField>
        )}
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input {...register('code', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.teams.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
              <Button variant="link" className="text-xs"
                onClick={() => setModalState({ open: true, team })}>
                {t('common.edit')}
              </Button>
              {team.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(team)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs"
                  onClick={() => toggleMutation.mutate(team)}>
                  {t('common.activate')}
                </Button>
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
          <Button onClick={() => setModalState({ open: true, team: null })}>
            {t('masterdata.teams.add')}
          </Button>
        )}
      </div>

      <Select
        className="max-w-xs"
        aria-label={t('masterdata.teams.allDepartments')}
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
      </Select>

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
