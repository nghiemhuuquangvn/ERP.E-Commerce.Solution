import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { attendanceApi } from '@/api/hrm/attendance.api'
import type { Attendance } from '@/api/hrm/attendance.api'
import { employeeSnapshotsApi } from '@/api/hrm/employeeSnapshots.api'
import { workShiftsApi } from '@/api/hrm/workShifts.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

function useEmployeePicker() {
  return useQuery({
    queryKey: ['hrm', 'employees', 'picker'],
    queryFn: () => employeeSnapshotsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })
}

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
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('hrm.attendance.checkInTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.employee')}</label>
            <select className="input" {...register('employeeId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {(employeesPage?.items ?? []).map((e) => (
                <option key={e.id} value={e.id}>
                  {e.fullName} ({e.employeeNumber})
                </option>
              ))}
            </select>
            {errors.employeeId && <p className="mt-1 text-xs text-red-600">{t('hrm.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.attendance.checkInTime')}</label>
            <input type="datetime-local" className="input" {...register('checkInTime', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.attendance.workShiftOptional')}</label>
            <select className="input" {...register('workShiftId')}>
              <option value="">{t('common.none')}</option>
              {(shifts ?? []).map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.startTime}–{s.endTime})
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.notes')}</label>
            <textarea className="input" rows={2} {...register('notes')} />
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('hrm.attendance.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('hrm.attendance.checkIn')}
            </button>
          </div>
        </form>
      </div>
    </div>
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
  const { register, handleSubmit } = useForm<CheckOutFormValues>({
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
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('hrm.attendance.checkOutTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.attendance.checkOutTime')}</label>
            <input type="datetime-local" className="input" {...register('checkOutTime', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.notes')}</label>
            <textarea className="input" rows={2} {...register('notes')} />
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('hrm.attendance.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('hrm.attendance.checkOut')}
            </button>
          </div>
        </form>
      </div>
    </div>
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
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('hrm.attendance.markAbsentTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.employee')}</label>
            <select className="input" {...register('employeeId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {(employeesPage?.items ?? []).map((e) => (
                <option key={e.id} value={e.id}>
                  {e.fullName} ({e.employeeNumber})
                </option>
              ))}
            </select>
            {errors.employeeId && <p className="mt-1 text-xs text-red-600">{t('hrm.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.date')}</label>
            <input type="date" className="input" {...register('date', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.notes')}</label>
            <textarea className="input" rows={2} {...register('notes')} />
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('hrm.attendance.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('hrm.attendance.markAbsent')}
            </button>
          </div>
        </form>
      </div>
    </div>
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
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => {
                setActiveRecord(a)
                setModal('checkOut')
              }}
            >
              {t('hrm.attendance.checkOut')}
            </button>
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
            <button className="btn-outline" onClick={() => setModal('markAbsent')}>
              {t('hrm.attendance.markAbsent')}
            </button>
            <button className="btn-primary" onClick={() => setModal('checkIn')}>
              {t('hrm.attendance.checkIn')}
            </button>
          </div>
        )}
      </div>

      <div className="flex gap-3">
        <select
          className="input max-w-xs"
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
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modal === 'checkIn' && <CheckInModal onClose={closeModal} />}
      {modal === 'checkOut' && activeRecord && <CheckOutModal record={activeRecord} onClose={closeModal} />}
      {modal === 'markAbsent' && <MarkAbsentModal onClose={closeModal} />}
    </div>
  )
}
