import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { leaveRequestsApi, LEAVE_TYPE } from '@/api/hrm/leaveRequests.api'
import type { LeaveRequest } from '@/api/hrm/leaveRequests.api'
import { employeeSnapshotsApi } from '@/api/hrm/employeeSnapshots.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
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

// ─── Submit modal ───────────────────────────────────────────────────────────

interface SubmitFormValues {
  employeeId: string
  leaveType: string
  startDate: string
  endDate: string
  reason: string
}

function SubmitLeaveModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: employeesPage } = useEmployeePicker()
  const { register, handleSubmit, formState: { errors } } = useForm<SubmitFormValues>({
    defaultValues: {
      employeeId: '',
      leaveType: String(LEAVE_TYPE.Annual),
      startDate: new Date().toISOString().slice(0, 10),
      endDate: new Date().toISOString().slice(0, 10),
      reason: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: SubmitFormValues) =>
      leaveRequestsApi.submit({
        employeeId: values.employeeId,
        leaveType: Number(values.leaveType),
        startDate: values.startDate,
        endDate: values.endDate,
        reason: values.reason.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'leaveRequests'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('hrm.leaveRequests.addTitle')}</h3>
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
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.leaveRequests.leaveType')}</label>
            <select className="input" {...register('leaveType')}>
              <option value={LEAVE_TYPE.Annual}>{t('hrm.leaveRequests.leaveTypes.annual')}</option>
              <option value={LEAVE_TYPE.Sick}>{t('hrm.leaveRequests.leaveTypes.sick')}</option>
              <option value={LEAVE_TYPE.Unpaid}>{t('hrm.leaveRequests.leaveTypes.unpaid')}</option>
              <option value={LEAVE_TYPE.Maternity}>{t('hrm.leaveRequests.leaveTypes.maternity')}</option>
              <option value={LEAVE_TYPE.Paternity}>{t('hrm.leaveRequests.leaveTypes.paternity')}</option>
            </select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.leaveRequests.startDate')}</label>
              <input type="date" className="input" {...register('startDate', { required: true })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.leaveRequests.endDate')}</label>
              <input type="date" className="input" {...register('endDate', { required: true })} />
            </div>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.leaveRequests.reason')}</label>
            <textarea className="input" rows={3} {...register('reason')} />
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('hrm.leaveRequests.failedToSave')}</p>}
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

// ─── Reject modal ───────────────────────────────────────────────────────────

interface RejectFormValues {
  reason: string
}

function RejectLeaveModal({ request, onClose }: { request: LeaveRequest; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<RejectFormValues>({
    defaultValues: { reason: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: RejectFormValues) => leaveRequestsApi.reject(request.id, { reason: values.reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'leaveRequests'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('hrm.leaveRequests.rejectTitle', { number: request.leaveRequestNumber })}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.leaveRequests.reason')}</label>
            <textarea className="input" rows={3} {...register('reason', { required: true })} />
            {errors.reason && <p className="mt-1 text-xs text-red-600">{t('hrm.required')}</p>}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('hrm.leaveRequests.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-danger">
              {t('hrm.leaveRequests.reject')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Page ───────────────────────────────────────────────────────────────────

export function LeaveRequestsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [employeeFilter, setEmployeeFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [showSubmitModal, setShowSubmitModal] = useState(false)
  const [rejectTarget, setRejectTarget] = useState<LeaveRequest | null>(null)
  const [cancelTarget, setCancelTarget] = useState<LeaveRequest | null>(null)

  const STATUS_OPTIONS = [
    { value: '', label: t('common.all') },
    { value: '0', label: t('hrm.leaveRequests.statuses.pending') },
    { value: '1', label: t('hrm.leaveRequests.statuses.approved') },
    { value: '2', label: t('hrm.leaveRequests.statuses.rejected') },
    { value: '3', label: t('hrm.leaveRequests.statuses.cancelled') },
  ]

  const { data: employeesPage } = useEmployeePicker()

  const { data, isLoading } = useQuery({
    queryKey: ['hrm', 'leaveRequests', { page, pageSize, employeeFilter, statusFilter }],
    queryFn: () =>
      leaveRequestsApi.list({
        page,
        pageSize,
        employeeId: employeeFilter || undefined,
        status: statusFilter ? Number(statusFilter) : undefined,
      }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('hrm:leave:manage')

  const employeeNameById = useMemo(() => {
    const map = new Map<string, string>()
    for (const e of employeesPage?.items ?? []) map.set(e.id, `${e.fullName} (${e.employeeNumber})`)
    return map
  }, [employeesPage])

  const approveMutation = useMutation({
    mutationFn: (r: LeaveRequest) => leaveRequestsApi.approve(r.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hrm', 'leaveRequests'] }),
  })

  const cancelMutation = useMutation({
    mutationFn: (r: LeaveRequest) => leaveRequestsApi.cancel(r.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'leaveRequests'] })
      setCancelTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'leaveRequestNumber', label: t('hrm.leaveRequests.number'), className: 'font-mono text-xs' },
      {
        key: 'employeeId',
        label: t('hrm.employee'),
        render: (r: LeaveRequest) => employeeNameById.get(r.employeeId) ?? r.employeeId,
      },
      { key: 'leaveType', label: t('hrm.leaveRequests.leaveType') },
      {
        key: 'startDate',
        label: t('hrm.leaveRequests.startDate'),
        render: (r: LeaveRequest) => new Date(r.startDate).toLocaleDateString(),
      },
      {
        key: 'endDate',
        label: t('hrm.leaveRequests.endDate'),
        render: (r: LeaveRequest) => new Date(r.endDate).toLocaleDateString(),
      },
      { key: 'daysRequested', label: t('hrm.leaveRequests.days') },
      {
        key: 'leaveStatus',
        label: t('common.status'),
        render: (r: LeaveRequest) => <StatusBadge status={r.leaveStatus} />,
      },
      {
        key: 'actions',
        label: '',
        render: (r: LeaveRequest) =>
          canManage && r.leaveStatus === 'Pending' ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-green-600 hover:underline"
                onClick={() => approveMutation.mutate(r)}
              >
                {t('hrm.leaveRequests.approve')}
              </button>
              <button className="text-xs text-red-600 hover:underline" onClick={() => setRejectTarget(r)}>
                {t('hrm.leaveRequests.reject')}
              </button>
              <button className="text-xs text-gray-500 hover:underline" onClick={() => setCancelTarget(r)}>
                {t('hrm.leaveRequests.cancel')}
              </button>
            </div>
          ) : null,
      },
    ],
    [approveMutation, canManage, employeeNameById, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('hrm.leaveRequests.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setShowSubmitModal(true)}>
            {t('hrm.leaveRequests.add')}
          </button>
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

      {showSubmitModal && <SubmitLeaveModal onClose={() => setShowSubmitModal(false)} />}
      {rejectTarget && <RejectLeaveModal request={rejectTarget} onClose={() => setRejectTarget(null)} />}

      <ConfirmDialog
        open={!!cancelTarget}
        title={t('hrm.leaveRequests.cancelTitle')}
        message={t('hrm.leaveRequests.cancelConfirm', { number: cancelTarget?.leaveRequestNumber })}
        confirmLabel={t('hrm.leaveRequests.cancel')}
        danger
        onConfirm={() => cancelTarget && cancelMutation.mutate(cancelTarget)}
        onCancel={() => setCancelTarget(null)}
      />
    </div>
  )
}
