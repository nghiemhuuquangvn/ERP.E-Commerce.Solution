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
import { Alert, Button, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.employees.hireTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="hire-form" loading={mutation.isPending}>{mutation.isPending ? t('masterdata.employees.hiring') : t('masterdata.employees.hireButton')}</Button>
        </>
      }
    >
      <form id="hire-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('masterdata.employees.firstName')} required error={errors.firstName ? t('common.required') : undefined}>
            <Input {...register('firstName', { required: true })} />
          </FormField>
          <FormField label={t('masterdata.employees.lastName')} required error={errors.lastName ? t('common.required') : undefined}>
            <Input {...register('lastName', { required: true })} />
          </FormField>
        </div>
        <FormField label={t('masterdata.employees.email')}>
          <Input type="email" {...register('email')} />
        </FormField>
        <FormField label={t('masterdata.employees.phone')}>
          <Input {...register('phone')} />
        </FormField>
        <FormField label={t('masterdata.employees.department')} required error={errors.departmentId ? t('common.required') : undefined}>
          <Select {...register('departmentId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {(departmentsPage?.items ?? []).map((d) => (
              <option key={d.id} value={d.id}>
                {d.departmentName}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('masterdata.employees.positionIdOptional')}>
          <Input {...register('positionId')} />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('masterdata.employees.dateOfHire')} required error={errors.dateOfHire ? t('common.required') : undefined}>
            <Input type="date" {...register('dateOfHire', { required: true })} />
          </FormField>
          <FormField label={t('masterdata.employees.employmentType')}>
            <Select {...register('employmentType')}>
              <option value={EMPLOYMENT_TYPE.FullTime}>{t('masterdata.employees.employmentTypes.fullTime')}</option>
              <option value={EMPLOYMENT_TYPE.PartTime}>{t('masterdata.employees.employmentTypes.partTime')}</option>
              <option value={EMPLOYMENT_TYPE.Contract}>{t('masterdata.employees.employmentTypes.contract')}</option>
            </Select>
          </FormField>
        </div>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.employees.failedToHire'))}</Alert>}
      </form>
    </Modal>
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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.employees.updateProfileTitle', { name: employee.fullName })}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="update-profile-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="update-profile-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('masterdata.employees.email')}>
          <Input type="email" {...register('email')} />
        </FormField>
        <FormField label={t('masterdata.employees.phone')}>
          <Input {...register('phone')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.employees.failedToUpdateProfile'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Transfer modal ─────────────────────────────────────────────────────────

interface TransferFormValues {
  newDepartmentId: string
}

function TransferModal({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.employees.transferTitle', { name: employee.fullName })}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="transfer-form" loading={mutation.isPending}>{mutation.isPending ? t('masterdata.employees.transferring') : t('masterdata.employees.transfer')}</Button>
        </>
      }
    >
      <form id="transfer-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('masterdata.employees.newDepartment')} required error={errors.newDepartmentId ? t('common.required') : undefined}>
          <Select {...register('newDepartmentId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {(departmentsPage?.items ?? [])
              .filter((d) => d.id !== employee.departmentId)
              .map((d) => (
                <option key={d.id} value={d.id}>
                  {d.departmentName}
                </option>
              ))}
          </Select>
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.employees.failedToTransfer'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Terminate modal ────────────────────────────────────────────────────────

interface TerminateFormValues {
  reason: string
}

function TerminateModal({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [confirming, setConfirming] = useState(false)
  const { register, handleSubmit, getValues, formState: { errors } } = useForm<TerminateFormValues>({
    defaultValues: { reason: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: TerminateFormValues) =>
      employeesApi.terminate(employee.id, { reason: values.reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'employees'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <>
      <Modal
        open
        size="sm"
        title={t('masterdata.employees.terminateTitle', { name: employee.fullName })}
        onClose={onClose}
        closeOnEsc={!mutation.isPending && !confirming}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button variant="danger" type="submit" form="terminate-form" loading={mutation.isPending}>{t('masterdata.employees.terminate')}</Button>
          </>
        }
      >
        <form id="terminate-form" onSubmit={handleSubmit(() => setConfirming(true))} className="space-y-4">
          <FormField label={t('masterdata.employees.reason')} required error={errors.reason ? t('common.required') : undefined}>
            <Textarea rows={3} {...register('reason', { required: true })} />
          </FormField>
          {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.employees.failedToTerminate'))}</Alert>}
        </form>
      </Modal>

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
    </>
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
              <Button variant="link" className="text-xs"
                onClick={() => openModal('profile', e)}>
                {t('masterdata.employees.updateProfile')}
              </Button>
              <Button variant="link" className="text-xs"
                onClick={() => openModal('transfer', e)}>
                {t('masterdata.employees.transfer')}
              </Button>
              <Button variant="link-danger" className="text-xs"
                onClick={() => openModal('terminate', e)}>
                {t('masterdata.employees.terminate')}
              </Button>
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
          <Button onClick={() => setModal('hire')}>
            {t('masterdata.employees.hire')}
          </Button>
        )}
      </div>

      <div className="flex gap-3">
        <Select
          className="max-w-xs"
        aria-label={t('masterdata.employees.allDepartments')}
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
        </Select>
        <Select
          className="max-w-xs"
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
        </Select>
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
