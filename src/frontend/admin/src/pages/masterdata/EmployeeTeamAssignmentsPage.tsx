import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  employeeTeamAssignmentsApi,
  type EmployeeTeamAssignment,
} from '@/api/masterdata/employeeTeamAssignments.api'
import { employeesApi } from '@/api/masterdata/employees.api'
import { teamsApi } from '@/api/masterdata/teams.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

interface CreateAssignmentFormValues {
  employeeId: string
  teamId: string
  positionId: string
  effectiveFrom: string
}

function CreateAssignmentModal({
  defaultEmployeeId,
  employees,
  teams,
  onClose,
}: {
  defaultEmployeeId: string
  employees: { id: string; fullName: string }[]
  teams: { id: string; name: string }[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { register, handleSubmit, formState: { errors } } = useForm<CreateAssignmentFormValues>({
    defaultValues: {
      employeeId: defaultEmployeeId,
      teamId: '',
      positionId: '',
      effectiveFrom: new Date().toISOString().slice(0, 10),
    },
  })

  const mutation = useMutation({
    mutationFn: (values: CreateAssignmentFormValues) =>
      employeeTeamAssignmentsApi.create({
        employeeId: values.employeeId,
        teamId: values.teamId,
        effectiveFrom: values.effectiveFrom,
        positionId: values.positionId.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'employeeTeamAssignments'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('masterdata.employeeTeamAssignments.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.employeeTeamAssignments.employee')}
            </label>
            <select className="input" {...register('employeeId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {employees.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.fullName}
                </option>
              ))}
            </select>
            {errors.employeeId && (
              <p className="mt-1 text-xs text-red-600">
                {t('masterdata.employeeTeamAssignments.employee')} is required.
              </p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.employeeTeamAssignments.team')}
            </label>
            <select className="input" {...register('teamId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {teams.map((tm) => (
                <option key={tm.id} value={tm.id}>
                  {tm.name}
                </option>
              ))}
            </select>
            {errors.teamId && (
              <p className="mt-1 text-xs text-red-600">
                {t('masterdata.employeeTeamAssignments.team')} is required.
              </p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.employeeTeamAssignments.positionIdOptional')}
            </label>
            <input className="input" {...register('positionId')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.employeeTeamAssignments.effectiveFrom')}
            </label>
            <input type="date" className="input" {...register('effectiveFrom', { required: true })} />
            {errors.effectiveFrom && (
              <p className="mt-1 text-xs text-red-600">
                {t('masterdata.employeeTeamAssignments.effectiveFrom')} is required.
              </p>
            )}
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.employeeTeamAssignments.failedToSave')}</p>
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

interface EndAssignmentFormValues {
  effectiveTo: string
}

function EndAssignmentModal({
  assignment,
  onClose,
}: {
  assignment: EmployeeTeamAssignment
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { register, handleSubmit, formState: { errors } } = useForm<EndAssignmentFormValues>({
    defaultValues: { effectiveTo: new Date().toISOString().slice(0, 10) },
  })

  const mutation = useMutation({
    mutationFn: (values: EndAssignmentFormValues) =>
      employeeTeamAssignmentsApi.end(assignment.id, { effectiveTo: values.effectiveTo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'employeeTeamAssignments'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('masterdata.employeeTeamAssignments.endTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.employeeTeamAssignments.effectiveTo')}
            </label>
            <input type="date" className="input" {...register('effectiveTo', { required: true })} />
            {errors.effectiveTo && (
              <p className="mt-1 text-xs text-red-600">
                {t('masterdata.employeeTeamAssignments.effectiveTo')} is required.
              </p>
            )}
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.employeeTeamAssignments.failedToSave')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('masterdata.employeeTeamAssignments.end')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function EmployeeTeamAssignmentsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [employeeId, setEmployeeId] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [endTarget, setEndTarget] = useState<EmployeeTeamAssignment | null>(null)

  const canManage = hasPermission('masterdata:employee-team-assignments:manage')

  const { data: employeesPage } = useQuery({
    queryKey: ['masterdata', 'employees', 'picker'],
    queryFn: () => employeesApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const { data: teamsPage } = useQuery({
    queryKey: ['masterdata', 'teams', 'picker'],
    queryFn: () => teamsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const employees = employeesPage?.items ?? []
  const teams = teamsPage?.items ?? []

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'employeeTeamAssignments', employeeId, { page, pageSize }],
    queryFn: () => employeeTeamAssignmentsApi.list({ employeeId, page, pageSize }),
    enabled: !!employeeId,
    staleTime: 30_000,
  })

  const columns = useMemo(
    () => [
      {
        key: 'teamId',
        label: t('masterdata.employeeTeamAssignments.team'),
        render: (a: EmployeeTeamAssignment) => teams.find((tm) => tm.id === a.teamId)?.name ?? a.teamId,
      },
      {
        key: 'effectiveFrom',
        label: t('masterdata.employeeTeamAssignments.effectiveFrom'),
        render: (a: EmployeeTeamAssignment) => a.effectiveFrom.slice(0, 10),
      },
      {
        key: 'effectiveTo',
        label: t('masterdata.employeeTeamAssignments.effectiveTo'),
        render: (a: EmployeeTeamAssignment) => a.effectiveTo?.slice(0, 10) ?? '—',
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (a: EmployeeTeamAssignment) =>
          a.effectiveTo ? (
            <StatusBadge status="Inactive" label={t('masterdata.employeeTeamAssignments.ended')} />
          ) : (
            <StatusBadge status="Active" label={t('masterdata.employeeTeamAssignments.current')} />
          ),
      },
      {
        key: 'actions',
        label: '',
        render: (a: EmployeeTeamAssignment) =>
          canManage && !a.effectiveTo ? (
            <button className="text-xs text-red-600 hover:underline" onClick={() => setEndTarget(a)}>
              {t('masterdata.employeeTeamAssignments.end')}
            </button>
          ) : null,
      },
    ],
    [canManage, teams, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.employeeTeamAssignments.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalOpen(true)}>
            {t('masterdata.employeeTeamAssignments.add')}
          </button>
        )}
      </div>

      <select
        className="input max-w-xs"
        value={employeeId}
        onChange={(e) => {
          setEmployeeId(e.target.value)
          setPage(1)
        }}
      >
        <option value="">{t('masterdata.employeeTeamAssignments.selectEmployee')}</option>
        {employees.map((e) => (
          <option key={e.id} value={e.id}>
            {e.fullName}
          </option>
        ))}
      </select>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyMessage={
          employeeId
            ? t('common.noRecordsFound')
            : t('masterdata.employeeTeamAssignments.pickEmployeeFirst')
        }
      />
      {employeeId && (
        <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
      )}

      {modalOpen && (
        <CreateAssignmentModal
          defaultEmployeeId={employeeId}
          employees={employees}
          teams={teams}
          onClose={() => setModalOpen(false)}
        />
      )}

      {endTarget && (
        <EndAssignmentModal assignment={endTarget} onClose={() => setEndTarget(null)} />
      )}
    </div>
  )
}
