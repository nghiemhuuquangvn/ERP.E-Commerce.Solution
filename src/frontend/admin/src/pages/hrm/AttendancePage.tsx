import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { attendanceApi } from '@/api/hrm/attendance.api'
import type { Attendance } from '@/api/hrm/attendance.api'
import { useEmployeePicker } from './useEmployeePicker'
import { workShiftsApi } from '@/api/hrm/workShifts.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

// ─── Check-In modal ─────────────────────────────────────────────────────────

interface CheckInFormValues {
  employeeId: string
  checkInTime: string
  workShiftId: string
  notes: string
}

function CheckInModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { data: employeesPage } = useEmployeePicker()
  const { data: shifts } = useQuery({
    queryKey: ['hrm', 'workShifts', 'picker'],
    queryFn: () => workShiftsApi.list({ activeOnly: true }),
    staleTime: 60_000,
  })

  const { register, handleSubmit, formState: { errors } } = useForm<CheckInFormValues>({
    defaultValues: {
      employeeId: '',
      checkInTime: new Date().toISOString().slice(0, 16),
      workShiftId: '',
      notes: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: CheckInFormValues) =>
      attendanceApi.checkIn({
        employeeId: values.employeeId,
        checkInTime: new Date(values.checkInTime).toISOString(),
        workShiftId: values.workShiftId || null,
        notes: values.notes.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'attendance'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('hrm.attendance.checkInTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="check-in-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('hrm.attendance.checkIn')}</Button>
        </>
      }
    >
      <form id="check-in-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('hrm.employee')} required error={errors.employeeId ? t('common.required') : undefined}>
          <Select {...register('employeeId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {(employeesPage?.items ?? []).map((e) => (
              <option key={e.id} value={e.id}>
                {e.fullName} ({e.employeeNumber})
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('hrm.attendance.checkInTime')} required error={errors.checkInTime ? t('common.required') : undefined}>
          <Input type="datetime-local" {...register('checkInTime', { required: true })} />
        </FormField>
        <FormField label={t('hrm.attendance.workShiftOptional')}>
          <Select {...register('workShiftId')}>
            <option value="">{t('common.none')}</option>
            {(shifts ?? []).map((s) => (
              <option key={s.id} value={s.id}>
                {s.name} ({s.startTime}–{s.endTime})
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('hrm.notes')}>
          <Textarea rows={2} {...register('notes')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.attendance.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Check-Out modal ────────────────────────────────────────────────────────

interface CheckOutFormValues {
  checkOutTime: string
  notes: string
}

function CheckOutModal({ record, onClose }: { record: Attendance; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<CheckOutFormValues>({
    defaultValues: { checkOutTime: new Date().toISOString().slice(0, 16), notes: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: CheckOutFormValues) =>
      attendanceApi.checkOut(record.id, {
        checkOutTime: new Date(values.checkOutTime).toISOString(),
        notes: values.notes.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'attendance'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('hrm.attendance.checkOutTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="check-out-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('hrm.attendance.checkOut')}</Button>
        </>
      }
    >
      <form id="check-out-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('hrm.attendance.checkOutTime')} required error={errors.checkOutTime ? t('common.required') : undefined}>
          <Input type="datetime-local" {...register('checkOutTime', { required: true })} />
        </FormField>
        <FormField label={t('hrm.notes')}>
          <Textarea rows={2} {...register('notes')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.attendance.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Mark Absent modal ──────────────────────────────────────────────────────

interface MarkAbsentFormValues {
  employeeId: string
  date: string
  notes: string
}

function MarkAbsentModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { data: employeesPage } = useEmployeePicker()
  const { register, handleSubmit, formState: { errors } } = useForm<MarkAbsentFormValues>({
    defaultValues: { employeeId: '', date: new Date().toISOString().slice(0, 10), notes: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: MarkAbsentFormValues) =>
      attendanceApi.markAbsent({
        employeeId: values.employeeId,
        date: values.date,
        notes: values.notes.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'attendance'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('hrm.attendance.markAbsentTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="mark-absent-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('hrm.attendance.markAbsent')}</Button>
        </>
      }
    >
      <form id="mark-absent-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('hrm.employee')} required error={errors.employeeId ? t('common.required') : undefined}>
          <Select {...register('employeeId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {(employeesPage?.items ?? []).map((e) => (
              <option key={e.id} value={e.id}>
                {e.fullName} ({e.employeeNumber})
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('common.date')} required error={errors.date ? t('common.required') : undefined}>
          <Input type="date" {...register('date', { required: true })} />
        </FormField>
        <FormField label={t('hrm.notes')}>
          <Textarea rows={2} {...register('notes')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.attendance.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Page ───────────────────────────────────────────────────────────────────

type ModalKind = 'checkIn' | 'checkOut' | 'markAbsent' | null

export function AttendancePage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [employeeFilter, setEmployeeFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [modal, setModal] = useState<ModalKind>(null)
  const [activeRecord, setActiveRecord] = useState<Attendance | null>(null)

  const STATUS_OPTIONS = [
    { value: '', label: t('common.all') },
    { value: '0', label: t('hrm.attendance.statuses.present') },
    { value: '1', label: t('hrm.attendance.statuses.absent') },
    { value: '2', label: t('hrm.attendance.statuses.late') },
    { value: '3', label: t('hrm.attendance.statuses.halfDay') },
    { value: '4', label: t('hrm.attendance.statuses.onLeave') },
    { value: '5', label: t('hrm.attendance.statuses.holiday') },
  ]

  const { data: employeesPage } = useEmployeePicker()

  const { data, isLoading } = useQuery({
    queryKey: ['hrm', 'attendance', { page, pageSize, employeeFilter, statusFilter }],
    queryFn: () =>
      attendanceApi.list({
        page,
        pageSize,
        employeeId: employeeFilter || undefined,
        status: statusFilter ? Number(statusFilter) : undefined,
      }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('hrm:attendance:manage')

  const employeeNameById = useMemo(() => {
    const map = new Map<string, string>()
    for (const e of employeesPage?.items ?? []) map.set(e.id, `${e.fullName} (${e.employeeNumber})`)
    return map
  }, [employeesPage])

  const closeModal = () => {
    setModal(null)
    setActiveRecord(null)
  }

  const columns = useMemo(
    () => [
      {
        key: 'employeeId',
        label: t('hrm.employee'),
        render: (a: Attendance) => employeeNameById.get(a.employeeId) ?? a.employeeId,
      },
      { key: 'date', label: t('common.date'), render: (a: Attendance) => new Date(a.date).toLocaleDateString() },
      {
        key: 'checkInTime',
        label: t('hrm.attendance.checkInTime'),
        render: (a: Attendance) => (a.checkInTime ? new Date(a.checkInTime).toLocaleTimeString() : '—'),
      },
      {
        key: 'checkOutTime',
        label: t('hrm.attendance.checkOutTime'),
        render: (a: Attendance) => (a.checkOutTime ? new Date(a.checkOutTime).toLocaleTimeString() : '—'),
      },
      { key: 'totalHours', label: t('hrm.attendance.totalHours') },
      {
        key: 'status',
        label: t('common.status'),
        render: (a: Attendance) => <StatusBadge status={a.status} />,
      },
      {
        key: 'actions',
        label: '',
        render: (a: Attendance) =>
          canManage && a.checkInTime && !a.checkOutTime ? (
            <Button variant="link" className="text-xs"
              onClick={() => {
                setActiveRecord(a)
                setModal('checkOut')
              }}>
              {t('hrm.attendance.checkOut')}
            </Button>
          ) : null,
      },
    ],
    [canManage, employeeNameById, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('hrm.attendance.title')}</h1>
        {canManage && (
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => setModal('markAbsent')}>
              {t('hrm.attendance.markAbsent')}
            </Button>
            <Button onClick={() => setModal('checkIn')}>
              {t('hrm.attendance.checkIn')}
            </Button>
          </div>
        )}
      </div>

      <div className="flex gap-3">
        <Select
          className="max-w-xs"
        aria-label={t('hrm.allEmployees')}
          value={employeeFilter}
          onChange={(e) => {
            setEmployeeFilter(e.target.value)
            setPage(1)
          }}
        >
          <option value="">{t('hrm.allEmployees')}</option>
          {(employeesPage?.items ?? []).map((e) => (
            <option key={e.id} value={e.id}>
              {e.fullName} ({e.employeeNumber})
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

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          employeeFilter || statusFilter ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => { setEmployeeFilter(''); setStatusFilter(''); setPage(1) }}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modal === 'checkIn' && <CheckInModal onClose={closeModal} />}
      {modal === 'checkOut' && activeRecord && <CheckOutModal record={activeRecord} onClose={closeModal} />}
      {modal === 'markAbsent' && <MarkAbsentModal onClose={closeModal} />}
    </div>
  )
}
