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
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()

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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.employeeTeamAssignments.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="create-assignment-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="create-assignment-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('masterdata.employeeTeamAssignments.employee')} required error={errors.employeeId ? t('common.required') : undefined}>
          <Select {...register('employeeId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {employees.map((e) => (
              <option key={e.id} value={e.id}>
                {e.fullName}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('masterdata.employeeTeamAssignments.team')} required error={errors.teamId ? t('common.required') : undefined}>
          <Select {...register('teamId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {teams.map((tm) => (
              <option key={tm.id} value={tm.id}>
                {tm.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('masterdata.employeeTeamAssignments.positionIdOptional')}>
          <Input {...register('positionId')} />
        </FormField>
        <FormField label={t('masterdata.employeeTeamAssignments.effectiveFrom')} required error={errors.effectiveFrom ? t('common.required') : undefined}>
          <Input type="date" {...register('effectiveFrom', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.employeeTeamAssignments.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const toast = useToast()

  const { register, handleSubmit, formState: { errors } } = useForm<EndAssignmentFormValues>({
    defaultValues: { effectiveTo: new Date().toISOString().slice(0, 10) },
  })

  const mutation = useMutation({
    mutationFn: (values: EndAssignmentFormValues) =>
      employeeTeamAssignmentsApi.end(assignment.id, { effectiveTo: values.effectiveTo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'employeeTeamAssignments'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.employeeTeamAssignments.endTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="end-assignment-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('masterdata.employeeTeamAssignments.end')}</Button>
        </>
      }
    >
      <form id="end-assignment-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('masterdata.employeeTeamAssignments.effectiveTo')} required error={errors.effectiveTo ? t('common.required') : undefined}>
          <Input type="date" {...register('effectiveTo', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.employeeTeamAssignments.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
            <Button variant="link-danger" className="text-xs" onClick={() => setEndTarget(a)}>
              {t('masterdata.employeeTeamAssignments.end')}
            </Button>
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
          <Button onClick={() => setModalOpen(true)}>
            {t('masterdata.employeeTeamAssignments.add')}
          </Button>
        )}
      </div>

      <Select
        className="max-w-xs"
        aria-label={t('masterdata.employeeTeamAssignments.selectEmployee')}
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
      </Select>

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
