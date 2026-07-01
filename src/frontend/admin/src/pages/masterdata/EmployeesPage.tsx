import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  employeesApi,
  EMPLOYMENT_TYPE,
  type Employee,
} from '@/api/masterdata/employees.api'
import { departmentsApi } from '@/api/masterdata/departments.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

// ─── Hire modal ─────────────────────────────────────────────────────────────

interface HireFormValues {
  firstName: string
  lastName: string
  email: string
  phone: string
  departmentId: string
  dateOfHire: string
  employmentType: string
  positionId: string
}

function HireModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: departmentsPage } = useQuery({
    queryKey: ['masterdata', 'departments', 'picker'],
    queryFn: () => departmentsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })

  const { register, handleSubmit, formState: { errors } } = useForm<HireFormValues>({
    defaultValues: {
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      departmentId: '',
      dateOfHire: new Date().toISOString().slice(0, 10),
      employmentType: String(EMPLOYMENT_TYPE.FullTime),
      positionId: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: HireFormValues) =>
      employeesApi.hire({
        firstName: values.firstName,
        lastName: values.lastName,
        email: values.email.trim() || null,
        phone: values.phone.trim() || null,
        departmentId: values.departmentId,
        dateOfHire: values.dateOfHire,
        employmentType: Number(values.employmentType),
        positionId: values.positionId.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'employees'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('masterdata.employees.hireTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.firstName')}</label>
              <input className="input" {...register('firstName', { required: true })} />
              {errors.firstName && <p className="mt-1 text-xs text-red-600">{t('masterdata.employees.required')}</p>}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.lastName')}</label>
              <input className="input" {...register('lastName', { required: true })} />
              {errors.lastName && <p className="mt-1 text-xs text-red-600">{t('masterdata.employees.required')}</p>}
            </div>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.email')}</label>
            <input type="email" className="input" {...register('email')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.phone')}</label>
            <input className="input" {...register('phone')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.department')}</label>
            <select className="input" {...register('departmentId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {(departmentsPage?.items ?? []).map((d) => (
                <option key={d.id} value={d.id}>
                  {d.departmentName}
                </option>
              ))}
            </select>
            {errors.departmentId && <p className="mt-1 text-xs text-red-600">{t('masterdata.employees.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.positionIdOptional')}</label>
            <input className="input" {...register('positionId')} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.dateOfHire')}</label>
              <input type="date" className="input" {...register('dateOfHire', { required: true })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.employmentType')}</label>
              <select className="input" {...register('employmentType')}>
                <option value={EMPLOYMENT_TYPE.FullTime}>{t('masterdata.employees.employmentTypes.fullTime')}</option>
                <option value={EMPLOYMENT_TYPE.PartTime}>{t('masterdata.employees.employmentTypes.partTime')}</option>
                <option value={EMPLOYMENT_TYPE.Contract}>{t('masterdata.employees.employmentTypes.contract')}</option>
              </select>
            </div>
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.employees.failedToHire')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('masterdata.employees.hiring') : t('masterdata.employees.hireButton')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Update Profile modal ───────────────────────────────────────────────────

interface ProfileFormValues {
  email: string
  phone: string
}

function UpdateProfileModal({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit } = useForm<ProfileFormValues>({
    defaultValues: { email: employee.email ?? '', phone: employee.phone ?? '' },
  })

  const mutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      employeesApi.updateProfile(employee.id, {
        email: values.email.trim() || null,
        phone: values.phone.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'employees'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('masterdata.employees.updateProfileTitle', { name: employee.fullName })}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.email')}</label>
            <input type="email" className="input" {...register('email')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.phone')}</label>
            <input className="input" {...register('phone')} />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.employees.failedToUpdateProfile')}</p>
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

// ─── Transfer modal ─────────────────────────────────────────────────────────

interface TransferFormValues {
  newDepartmentId: string
}

function TransferModal({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: departmentsPage } = useQuery({
    queryKey: ['masterdata', 'departments', 'picker'],
    queryFn: () => departmentsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })

  const { register, handleSubmit, formState: { errors } } = useForm<TransferFormValues>({
    defaultValues: { newDepartmentId: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: TransferFormValues) =>
      employeesApi.transfer(employee.id, { newDepartmentId: values.newDepartmentId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'employees'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('masterdata.employees.transferTitle', { name: employee.fullName })}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.newDepartment')}</label>
            <select className="input" {...register('newDepartmentId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {(departmentsPage?.items ?? [])
                .filter((d) => d.id !== employee.departmentId)
                .map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.departmentName}
                  </option>
                ))}
            </select>
            {errors.newDepartmentId && <p className="mt-1 text-xs text-red-600">{t('masterdata.employees.required')}</p>}
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.employees.failedToTransfer')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('masterdata.employees.transferring') : t('masterdata.employees.transfer')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Terminate modal ────────────────────────────────────────────────────────

interface TerminateFormValues {
  reason: string
}

function TerminateModal({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState(false)
  const { register, handleSubmit, getValues, formState: { errors } } = useForm<TerminateFormValues>({
    defaultValues: { reason: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: TerminateFormValues) =>
      employeesApi.terminate(employee.id, { reason: values.reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'employees'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('masterdata.employees.terminateTitle', { name: employee.fullName })}
        </h3>
        <form
          onSubmit={handleSubmit(() => setConfirming(true))}
          className="space-y-4"
        >
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.employees.reason')}</label>
            <textarea className="input" rows={3} {...register('reason', { required: true })} />
            {errors.reason && <p className="mt-1 text-xs text-red-600">{t('masterdata.employees.reason')} is required.</p>}
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.employees.failedToTerminate')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" className="btn-danger">
              {t('masterdata.employees.terminate')}
            </button>
          </div>
        </form>

        <ConfirmDialog
          open={confirming}
          title={t('masterdata.employees.confirmTerminationTitle')}
          message={t('masterdata.employees.terminateConfirm', { name: employee.fullName })}
          confirmLabel={t('masterdata.employees.terminate')}
          danger
          onConfirm={() => {
            setConfirming(false)
            mutation.mutate(getValues())
          }}
          onCancel={() => setConfirming(false)}
        />
      </div>
    </div>
  )
}

// ─── Page ───────────────────────────────────────────────────────────────────

type ModalKind = 'hire' | 'profile' | 'transfer' | 'terminate' | null

export function EmployeesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [departmentFilter, setDepartmentFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [modal, setModal] = useState<ModalKind>(null)
  const [activeEmployee, setActiveEmployee] = useState<Employee | null>(null)

  const STATUS_OPTIONS = [
    { value: '', label: t('masterdata.employees.statusOptions.all') },
    { value: '0', label: t('masterdata.employees.statusOptions.active') },
    { value: '1', label: t('masterdata.employees.statusOptions.onLeave') },
    { value: '2', label: t('masterdata.employees.statusOptions.terminated') },
  ]

  const { data: departmentsPage } = useQuery({
    queryKey: ['masterdata', 'departments', 'picker'],
    queryFn: () => departmentsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'employees', { page, pageSize, departmentFilter, statusFilter }],
    queryFn: () =>
      employeesApi.list({
        page,
        pageSize,
        departmentId: departmentFilter || undefined,
        status: statusFilter ? Number(statusFilter) : undefined,
      }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:employees:manage')

  const departmentNameById = useMemo(() => {
    const map = new Map<string, string>()
    for (const d of departmentsPage?.items ?? []) map.set(d.id, d.departmentName)
    return map
  }, [departmentsPage])

  const openModal = (kind: ModalKind, employee: Employee) => {
    setActiveEmployee(employee)
    setModal(kind)
  }

  const closeModal = () => {
    setModal(null)
    setActiveEmployee(null)
  }

  const columns = useMemo(
    () => [
      { key: 'employeeNumber', label: t('masterdata.employees.employeeNumber'), className: 'font-mono text-xs' },
      { key: 'fullName', label: t('common.name') },
      {
        key: 'departmentId',
        label: t('masterdata.employees.department'),
        render: (e: Employee) => departmentNameById.get(e.departmentId) ?? e.departmentId,
      },
      { key: 'email', label: t('masterdata.employees.email'), render: (e: Employee) => e.email ?? '—' },
      {
        key: 'employmentStatus',
        label: t('common.status'),
        render: (e: Employee) => <StatusBadge status={e.employmentStatus} />,
      },
      {
        key: 'actions',
        label: '',
        render: (e: Employee) =>
          canManage && e.employmentStatus !== 'Terminated' ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => openModal('profile', e)}
              >
                {t('masterdata.employees.updateProfile')}
              </button>
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => openModal('transfer', e)}
              >
                {t('masterdata.employees.transfer')}
              </button>
              <button
                className="text-xs text-red-600 hover:underline"
                onClick={() => openModal('terminate', e)}
              >
                {t('masterdata.employees.terminate')}
              </button>
            </div>
          ) : null,
      },
    ],
    [canManage, departmentNameById, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.employees.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModal('hire')}>
            {t('masterdata.employees.hire')}
          </button>
        )}
      </div>

      <div className="flex gap-3">
        <select
          className="input max-w-xs"
          value={departmentFilter}
          onChange={(e) => {
            setDepartmentFilter(e.target.value)
            setPage(1)
          }}
        >
          <option value="">{t('masterdata.employees.allDepartments')}</option>
          {(departmentsPage?.items ?? []).map((d) => (
            <option key={d.id} value={d.id}>
              {d.departmentName}
            </option>
          ))}
        </select>
        <select
          className="input max-w-xs"
          value={statusFilter}
          onChange={(e) => {
            setStatusFilter(e.target.value)
            setPage(1)
          }}
        >
          {STATUS_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />

      {modal === 'hire' && <HireModal onClose={closeModal} />}
      {modal === 'profile' && activeEmployee && (
        <UpdateProfileModal employee={activeEmployee} onClose={closeModal} />
      )}
      {modal === 'transfer' && activeEmployee && (
        <TransferModal employee={activeEmployee} onClose={closeModal} />
      )}
      {modal === 'terminate' && activeEmployee && (
        <TerminateModal employee={activeEmployee} onClose={closeModal} />
      )}
    </div>
  )
}
