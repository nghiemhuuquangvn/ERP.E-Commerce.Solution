import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { payrollApi, serverMessage } from '@/api/hrm/payroll.api'
import type { PaySlip, ProcessPayrollResult } from '@/api/hrm/payroll.api'
import { employeeSnapshotsApi } from '@/api/hrm/employeeSnapshots.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

const CURRENT_YEAR = new Date().getFullYear()
const CURRENT_MONTH = new Date().getMonth() + 1

function useEmployeePicker() {
  return useQuery({
    queryKey: ['hrm', 'employees', 'picker'],
    queryFn: () => employeeSnapshotsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })
}

// ─── Process Payroll modal ──────────────────────────────────────────────────

interface ProcessFormValues {
  year: number
  month: number
}

function ProcessPayrollModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [result, setResult] = useState<ProcessPayrollResult | null>(null)
  const { register, handleSubmit } = useForm<ProcessFormValues>({
    defaultValues: { year: CURRENT_YEAR, month: CURRENT_MONTH },
  })

  const mutation = useMutation({
    mutationFn: (values: ProcessFormValues) =>
      payrollApi.processPayroll({ year: Number(values.year), month: Number(values.month) }),
    onSuccess: (r) => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'paySlips'] })
      if (r.skipped.length === 0) onClose()
      else setResult(r)
    },
  })

  if (result) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
        <div className="card max-h-[90vh] w-full max-w-lg overflow-y-auto p-6">
          <h3 className="mb-2 text-base font-semibold text-gray-900">{t('hrm.payroll.processResultTitle')}</h3>
          <p className="mb-3 text-sm text-gray-700">{t('hrm.payroll.createdCount', { count: result.created.length })}</p>
          <p className="mb-2 text-sm font-medium text-amber-700">{t('hrm.payroll.skippedCount', { count: result.skipped.length })}</p>
          <ul className="mb-4 space-y-1 text-sm text-gray-700">
            {result.skipped.map((s) => (
              <li key={s.employeeId}>
                {s.fullName} ({s.employeeNumber}) — {t('hrm.payroll.noSalaryProfile')}
              </li>
            ))}
          </ul>
          <p className="mb-4 text-xs text-gray-500">{t('hrm.payroll.skippedHint')}</p>
          <div className="flex justify-end">
            <button className="btn-primary" onClick={onClose}>{t('common.close')}</button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('hrm.payroll.processTitle')}</h3>
        <p className="mb-4 text-sm text-gray-600">{t('hrm.payroll.processHint')}</p>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.payroll.year')}</label>
              <input type="number" className="input" {...register('year', { required: true, valueAsNumber: true })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.payroll.month')}</label>
              <input
                type="number"
                min={1}
                max={12}
                className="input"
                {...register('month', { required: true, valueAsNumber: true })}
              />
            </div>
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{serverMessage(mutation.error, t('hrm.payroll.failedToSave'))}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('hrm.payroll.process')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Generate Pay Slip modal ────────────────────────────────────────────────

interface GenerateFormValues {
  employeeId: string
  year: number
  month: number
  grossPay: number
  deductions: number
  overtimeHours: number
  overtimePay: number
}

function GeneratePaySlipModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: employeesPage } = useEmployeePicker()
  const { register, handleSubmit, formState: { errors } } = useForm<GenerateFormValues>({
    defaultValues: {
      employeeId: '',
      year: CURRENT_YEAR,
      month: CURRENT_MONTH,
      grossPay: 0,
      deductions: 0,
      overtimeHours: 0,
      overtimePay: 0,
    },
  })

  const mutation = useMutation({
    mutationFn: (values: GenerateFormValues) =>
      payrollApi.generatePaySlip({
        employeeId: values.employeeId,
        year: Number(values.year),
        month: Number(values.month),
        grossPay: Number(values.grossPay),
        deductions: Number(values.deductions),
        overtimeHours: Number(values.overtimeHours),
        overtimePay: Number(values.overtimePay),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'paySlips'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('hrm.payroll.generateTitle')}</h3>
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
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.payroll.year')}</label>
              <input type="number" className="input" {...register('year', { required: true, valueAsNumber: true })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.payroll.month')}</label>
              <input
                type="number"
                min={1}
                max={12}
                className="input"
                {...register('month', { required: true, valueAsNumber: true })}
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.payroll.grossPay')}</label>
              <input
                type="number"
                step="0.01"
                className="input"
                {...register('grossPay', { required: true, valueAsNumber: true })}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.payroll.deductions')}</label>
              <input
                type="number"
                step="0.01"
                className="input"
                {...register('deductions', { valueAsNumber: true })}
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.payroll.overtimeHours')}</label>
              <input
                type="number"
                step="0.01"
                className="input"
                {...register('overtimeHours', { valueAsNumber: true })}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('hrm.payroll.overtimePay')}</label>
              <input
                type="number"
                step="0.01"
                className="input"
                {...register('overtimePay', { valueAsNumber: true })}
              />
            </div>
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('hrm.payroll.failedToSave')}</p>}
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

// ─── Pay slip detail modal ──────────────────────────────────────────────────

const money = (v: number) => new Intl.NumberFormat().format(v)

function Row({ label, value, strong, muted }: { label: string; value: string; strong?: boolean; muted?: boolean }) {
  return (
    <div className={`flex justify-between py-1 ${strong ? 'border-t border-gray-300 pt-2 font-semibold' : ''} ${muted ? 'text-gray-500' : ''}`}>
      <span>{label}</span>
      <span className="tabular-nums">{value}</span>
    </div>
  )
}

function PaySlipDetailModal({ slip, employeeName, canManage, onClose }: { slip: PaySlip; employeeName: string; canManage: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [reason, setReason] = useState('')
  const [cancelling, setCancelling] = useState(false)
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['hrm', 'paySlips'] })
    onClose()
  }
  const pay = useMutation({ mutationFn: () => payrollApi.payPaySlip(slip.id, { paymentMethod: 'BankTransfer' }), onSuccess: refresh })
  const cancel = useMutation({ mutationFn: () => payrollApi.cancelPaySlip(slip.id, reason), onSuccess: refresh })

  const calculated = slip.standardDays > 0
  const employeeInsurance = slip.employeeSocial + slip.employeeHealth + slip.employeeUnemployment
  const employerInsurance = slip.employerSocial + slip.employerHealth + slip.employerUnemployment
  const error = pay.isError ? serverMessage(pay.error, '') : cancel.isError ? serverMessage(cancel.error, '') : ''

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[92vh] w-full max-w-lg overflow-y-auto p-6 text-sm">
        <div className="mb-3 flex items-start justify-between">
          <div>
            <h3 className="text-base font-semibold text-gray-900">{slip.paySlipNumber}</h3>
            <p className="text-gray-500">{employeeName} · {slip.payPeriod}</p>
          </div>
          <StatusBadge status={slip.paymentStatus} />
        </div>

        {calculated ? (
          <>
            <Row label={t('hrm.payroll.detail.baseSalary')} value={money(slip.baseSalary)} />
            <Row label={t('hrm.payroll.detail.workedDays', { worked: slip.workedDays, standard: slip.standardDays })} value={money(slip.grossSalary - slip.allowances)} />
            <Row label={t('hrm.payroll.detail.allowances')} value={money(slip.allowances)} />
            <Row label={t('hrm.payroll.detail.overtime', { hours: slip.overtimeHours })} value={money(slip.overtimePay)} />
            <Row label={t('hrm.payroll.grossSalary')} value={money(slip.grossSalary + slip.overtimePay)} strong />
            <div className="mt-3 text-xs font-semibold uppercase tracking-wide text-gray-500">{t('hrm.payroll.detail.withheld')}</div>
            <Row label={t('hrm.payroll.detail.social')} value={`− ${money(slip.employeeSocial)}`} />
            <Row label={t('hrm.payroll.detail.health')} value={`− ${money(slip.employeeHealth)}`} />
            <Row label={t('hrm.payroll.detail.unemployment')} value={`− ${money(slip.employeeUnemployment)}`} />
            <Row label={t('hrm.payroll.detail.taxable', { dependents: slip.dependentCount })} value={money(slip.taxableIncome)} muted />
            <Row label={t('hrm.payroll.detail.pit')} value={`− ${money(slip.personalIncomeTax)}`} />
            <Row label={t('hrm.payroll.netSalary')} value={money(slip.netSalary)} strong />
            <div className="mt-3 rounded-md bg-gray-50 p-3 text-xs text-gray-600">
              <Row label={t('hrm.payroll.detail.employerCost')} value={money(employerInsurance)} muted />
              <p className="mt-1">{t('hrm.payroll.detail.employerHint', { insurance: money(employeeInsurance) })}</p>
            </div>
          </>
        ) : (
          <>
            <Row label={t('hrm.payroll.grossSalary')} value={money(slip.grossSalary)} />
            <Row label={t('hrm.payroll.overtimePay')} value={money(slip.overtimePay)} />
            <Row label={t('hrm.payroll.deductions')} value={`− ${money(slip.deductions)}`} />
            <Row label={t('hrm.payroll.netSalary')} value={money(slip.netSalary)} strong />
            <p className="mt-2 text-xs text-gray-500">{t('hrm.payroll.detail.manualSlip')}</p>
          </>
        )}

        {error && <p className="mt-3 text-xs text-red-600">{error}</p>}

        {cancelling && (
          <div className="mt-4 space-y-2">
            <label className="block text-sm font-medium text-gray-700">{t('hrm.payroll.detail.cancelReason')}</label>
            <input className="input" value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
        )}

        <div className="mt-5 flex justify-end gap-3">
          {canManage && slip.paymentStatus === 'Pending' && !cancelling && (
            <>
              <button className="btn-outline" onClick={() => setCancelling(true)}>{t('hrm.payroll.detail.cancel')}</button>
              <button className="btn-primary" disabled={pay.isPending} onClick={() => pay.mutate()}>{t('hrm.payroll.detail.pay')}</button>
            </>
          )}
          {cancelling && (
            <button className="btn-danger" disabled={!reason.trim() || cancel.isPending} onClick={() => cancel.mutate()}>
              {t('hrm.payroll.detail.confirmCancel')}
            </button>
          )}
          <button className="btn-outline" onClick={onClose}>{t('common.close')}</button>
        </div>
      </div>
    </div>
  )
}

// ─── Page ───────────────────────────────────────────────────────────────────

type ModalKind = 'process' | 'generate' | null

export function PayrollPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [employeeFilter, setEmployeeFilter] = useState('')
  const [yearFilter, setYearFilter] = useState('')
  const [monthFilter, setMonthFilter] = useState('')
  const [modal, setModal] = useState<ModalKind>(null)
  const [detail, setDetail] = useState<PaySlip | null>(null)

  const { data: employeesPage } = useEmployeePicker()

  const { data, isLoading } = useQuery({
    queryKey: ['hrm', 'paySlips', { page, pageSize, employeeFilter, yearFilter, monthFilter }],
    queryFn: () =>
      payrollApi.listPaySlips({
        page,
        pageSize,
        employeeId: employeeFilter || undefined,
        year: yearFilter ? Number(yearFilter) : undefined,
        month: monthFilter ? Number(monthFilter) : undefined,
      }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('hrm:payroll:manage')

  const employeeNameById = useMemo(() => {
    const map = new Map<string, string>()
    for (const e of employeesPage?.items ?? []) map.set(e.id, `${e.fullName} (${e.employeeNumber})`)
    return map
  }, [employeesPage])

  const columns = useMemo(
    () => [
      { key: 'paySlipNumber', label: t('hrm.payroll.number'), className: 'font-mono text-xs' },
      {
        key: 'employeeId',
        label: t('hrm.employee'),
        render: (p: PaySlip) => employeeNameById.get(p.employeeId) ?? p.employeeId,
      },
      { key: 'payPeriod', label: t('hrm.payroll.payPeriod') },
      { key: 'grossSalary', label: t('hrm.payroll.grossSalary'), render: (p: PaySlip) => money(p.grossSalary + p.overtimePay) },
      { key: 'deductions', label: t('hrm.payroll.deductions'), render: (p: PaySlip) => money(p.deductions) },
      { key: 'netSalary', label: t('hrm.payroll.netSalary'), render: (p: PaySlip) => money(p.netSalary) },
      {
        key: 'paymentStatus',
        label: t('common.status'),
        render: (p: PaySlip) => <StatusBadge status={p.paymentStatus} />,
      },
      {
        key: 'actions',
        label: '',
        render: (p: PaySlip) => (
          <button className="text-xs text-blue-600 hover:underline" onClick={() => setDetail(p)}>
            {t('hrm.payroll.details')}
          </button>
        ),
      },
    ],
    [employeeNameById, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('hrm.payroll.title')}</h1>
        {canManage && (
          <div className="flex gap-2">
            <button className="btn-outline" onClick={() => setModal('generate')}>
              {t('hrm.payroll.generate')}
            </button>
            <button className="btn-primary" onClick={() => setModal('process')}>
              {t('hrm.payroll.process')}
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
        <input
          type="number"
          placeholder={t('hrm.payroll.year')}
          className="input max-w-[8rem]"
          value={yearFilter}
          onChange={(e) => {
            setYearFilter(e.target.value)
            setPage(1)
          }}
        />
        <input
          type="number"
          min={1}
          max={12}
          placeholder={t('hrm.payroll.month')}
          className="input max-w-[8rem]"
          value={monthFilter}
          onChange={(e) => {
            setMonthFilter(e.target.value)
            setPage(1)
          }}
        />
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modal === 'process' && <ProcessPayrollModal onClose={() => setModal(null)} />}
      {modal === 'generate' && <GeneratePaySlipModal onClose={() => setModal(null)} />}
      {detail && (
        <PaySlipDetailModal
          slip={detail}
          employeeName={employeeNameById.get(detail.employeeId) ?? detail.employeeId}
          canManage={canManage}
          onClose={() => setDetail(null)}
        />
      )}
    </div>
  )
}
