import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { leaveRequestsApi, LEAVE_TYPE } from '@/api/hrm/leaveRequests.api'
import type { LeaveRequest } from '@/api/hrm/leaveRequests.api'
import { useEmployeePicker } from './useEmployeePicker'
import { payrollSettingsApi } from '@/api/hrm/payrollSettings.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
  const { data: employeesPage } = useEmployeePicker()
  const { register, handleSubmit, watch, formState: { errors } } = useForm<SubmitFormValues>({
    defaultValues: {
      employeeId: '',
      leaveType: String(LEAVE_TYPE.Annual),
      startDate: new Date().toISOString().slice(0, 10),
      endDate: new Date().toISOString().slice(0, 10),
      reason: '',
    },
  })

  // Annual leave is limited by the year's balance: show what is left for the chosen employee.
  const employeeId = watch('employeeId')
  const leaveType = watch('leaveType')
  const year = new Date(watch('startDate') || Date.now()).getFullYear()
  const { data: balances } = useQuery({
    queryKey: ['hrm', 'leaveBalances', employeeId, year],
    queryFn: () => payrollSettingsApi.leaveBalances(employeeId, year),
    enabled: !!employeeId && Number(leaveType) === LEAVE_TYPE.Annual,
  })
  const annual = balances?.find((b) => b.leaveType === 'Annual')

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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('hrm.leaveRequests.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="submit-leave-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="submit-leave-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
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
        <FormField label={t('hrm.leaveRequests.leaveType')}>
          <Select {...register('leaveType')}>
            <option value={LEAVE_TYPE.Annual}>{t('hrm.leaveRequests.leaveTypes.annual')}</option>
            <option value={LEAVE_TYPE.Sick}>{t('hrm.leaveRequests.leaveTypes.sick')}</option>
            <option value={LEAVE_TYPE.Unpaid}>{t('hrm.leaveRequests.leaveTypes.unpaid')}</option>
            <option value={LEAVE_TYPE.Maternity}>{t('hrm.leaveRequests.leaveTypes.maternity')}</option>
            <option value={LEAVE_TYPE.Paternity}>{t('hrm.leaveRequests.leaveTypes.paternity')}</option>
          </Select>
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.leaveRequests.startDate')} required error={errors.startDate ? t('common.required') : undefined}>
            <Input type="date" {...register('startDate', { required: true })} />
          </FormField>
          <FormField label={t('hrm.leaveRequests.endDate')} required error={errors.endDate ? t('common.required') : undefined}>
            <Input type="date" {...register('endDate', { required: true })} />
          </FormField>
        </div>
        <FormField label={t('hrm.leaveRequests.reason')}>
          <Textarea rows={3} {...register('reason')} />
        </FormField>
        {annual && Number(leaveType) === LEAVE_TYPE.Annual && (
          <Alert variant="info">
            {t('hrm.leaveRequests.balance', { remaining: annual.remainingDays, total: annual.totalDays, year })}
          </Alert>
        )}
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.leaveRequests.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Reject modal ───────────────────────────────────────────────────────────

interface RejectFormValues {
  reason: string
}

function RejectLeaveModal({ request, onClose }: { request: LeaveRequest; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<RejectFormValues>({
    defaultValues: { reason: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: RejectFormValues) => leaveRequestsApi.reject(request.id, { reason: values.reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'leaveRequests'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('hrm.leaveRequests.rejectTitle', { number: request.leaveRequestNumber })}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="reject-leave-form" variant="danger" loading={mutation.isPending}>{t('hrm.leaveRequests.reject')}</Button>
        </>
      }
    >
      <form id="reject-leave-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('hrm.leaveRequests.reason')} required error={errors.reason ? t('common.required') : undefined}>
          <Textarea rows={3} {...register('reason', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.leaveRequests.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'leaveRequests'] })
      queryClient.invalidateQueries({ queryKey: ['hrm', 'leaveBalances'] })
    },
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
              <Button variant="link" className="text-xs"
                onClick={() => approveMutation.mutate(r)}>
                {t('hrm.leaveRequests.approve')}
              </Button>
              <Button variant="link-danger" className="text-xs" onClick={() => setRejectTarget(r)}>
                {t('hrm.leaveRequests.reject')}
              </Button>
              <button className="text-xs text-ink-secondary hover:underline" onClick={() => setCancelTarget(r)}>
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
          <Button onClick={() => setShowSubmitModal(true)}>
            {t('hrm.leaveRequests.add')}
          </Button>
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

      {approveMutation.isError && (
        <Alert variant="danger">{serverMessage(approveMutation.error, t('hrm.leaveRequests.failedToSave'))}</Alert>
      )}
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
