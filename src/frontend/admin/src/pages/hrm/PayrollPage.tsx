import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { payrollApi } from '@/api/hrm/payroll.api'
import type { PaySlip, ProcessPayrollResult } from '@/api/hrm/payroll.api'
import { useEmployeePicker } from './useEmployeePicker'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const CURRENT_YEAR = new Date().getFullYear()
const CURRENT_MONTH = new Date().getMonth() + 1

// ─── Process Payroll modal ──────────────────────────────────────────────────

interface ProcessFormValues {
  year: number
  month: number
}

function ProcessPayrollModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [result, setResult] = useState<ProcessPayrollResult | null>(null)
  const { register, handleSubmit, formState: { errors } } = useForm<ProcessFormValues>({
    defaultValues: { year: CURRENT_YEAR, month: CURRENT_MONTH },
  })

  const mutation = useMutation({
    mutationFn: (values: ProcessFormValues) =>
      payrollApi.processPayroll({ year: Number(values.year), month: Number(values.month) }),
    onSuccess: (r) => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'paySlips'] })
      toast.success(t('common.saved'))
      if (r.skipped.length === 0) onClose()
      else setResult(r)
    },
  })

  if (result) {
    return (
      <Modal
        open
        size="md"
        title={t('hrm.payroll.processResultTitle')}
        onClose={onClose}
        footer={<Button onClick={onClose}>{t('common.close')}</Button>}
      >
        <p className="mb-3 text-sm text-ink">{t('hrm.payroll.createdCount', { count: result.created.length })}</p>
        <Alert variant="warning" className="mb-3">{t('hrm.payroll.skippedCount', { count: result.skipped.length })}</Alert>
        <ul className="mb-4 space-y-1 text-sm text-ink">
          {result.skipped.map((s) => (
            <li key={s.employeeId}>
              {s.fullName} ({s.employeeNumber}) — {t('hrm.payroll.noSalaryProfile')}
            </li>
          ))}
        </ul>
        <p className="text-xs text-ink-secondary">{t('hrm.payroll.skippedHint')}</p>
      </Modal>
    )
  }

  return (
    <Modal
      open
      size="sm"
      title={t('hrm.payroll.processTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="process-payroll-form" loading={mutation.isPending}>{t('hrm.payroll.process')}</Button>
        </>
      }
    >
      <p className="mb-4 text-sm text-ink-secondary">{t('hrm.payroll.processHint')}</p>
      <form id="process-payroll-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.payroll.year')} required error={errors.year ? t('common.required') : undefined}>
            <Input type="number" {...register('year', { required: true, valueAsNumber: true })} />
          </FormField>
          <FormField label={t('hrm.payroll.month')} required error={errors.month ? t('common.required') : undefined}>
            <Input type="number" min={1} max={12} {...register('month', { required: true, valueAsNumber: true })} />
          </FormField>
        </div>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.payroll.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('hrm.payroll.generateTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="generate-pay-slip-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="generate-pay-slip-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
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
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.payroll.year')} required error={errors.year ? t('common.required') : undefined}>
            <Input type="number" {...register('year', { required: true, valueAsNumber: true })} />
          </FormField>
          <FormField label={t('hrm.payroll.month')} required error={errors.month ? t('common.required') : undefined}>
            <Input type="number" min={1} max={12} {...register('month', { required: true, valueAsNumber: true })} />
          </FormField>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.payroll.grossPay')} required error={errors.grossPay ? t('common.required') : undefined}>
            <Input type="number" step="0.01" {...register('grossPay', { required: true, valueAsNumber: true })} />
          </FormField>
          <FormField label={t('hrm.payroll.deductions')}>
            <Input type="number" step="0.01" {...register('deductions', { valueAsNumber: true })} />
          </FormField>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.payroll.overtimeHours')}>
            <Input type="number" step="0.01" {...register('overtimeHours', { valueAsNumber: true })} />
          </FormField>
          <FormField label={t('hrm.payroll.overtimePay')}>
            <Input type="number" step="0.01" {...register('overtimePay', { valueAsNumber: true })} />
          </FormField>
        </div>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.payroll.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Pay slip detail modal ──────────────────────────────────────────────────

const money = (v: number) => new Intl.NumberFormat().format(v)

function Row({ label, value, strong, muted }: { label: string; value: string; strong?: boolean; muted?: boolean }) {
  return (
    <div className={`flex justify-between py-1 ${strong ? 'border-t border-line pt-2 font-semibold' : ''} ${muted ? 'text-ink-secondary' : ''}`}>
      <span>{label}</span>
      <span className="tabular-nums">{value}</span>
    </div>
  )
}

function PaySlipDetailModal({ slip, employeeName, canManage, onClose }: { slip: PaySlip; employeeName: string; canManage: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [reason, setReason] = useState('')
  const [cancelling, setCancelling] = useState(false)
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['hrm', 'paySlips'] })
    toast.success(t('common.saved'))
    onClose()
  }
  const pay = useMutation({ mutationFn: () => payrollApi.payPaySlip(slip.id, { paymentMethod: 'BankTransfer' }), onSuccess: refresh })
  const cancel = useMutation({ mutationFn: () => payrollApi.cancelPaySlip(slip.id, reason), onSuccess: refresh })

  const calculated = slip.standardDays > 0
  const employeeInsurance = slip.employeeSocial + slip.employeeHealth + slip.employeeUnemployment
  const employerInsurance = slip.employerSocial + slip.employerHealth + slip.employerUnemployment
  const failure = pay.isError ? pay.error : cancel.isError ? cancel.error : null
  const busy = pay.isPending || cancel.isPending

  return (
    <Modal
      open
      size="md"
      title={slip.paySlipNumber}
      onClose={onClose}
      closeOnEsc={!busy}
      footer={
        <>
          {canManage && slip.paymentStatus === 'Pending' && !cancelling && (
            <>
              <Button variant="outline" onClick={() => setCancelling(true)}>{t('hrm.payroll.detail.cancel')}</Button>
              <Button loading={pay.isPending} onClick={() => pay.mutate()}>{t('hrm.payroll.detail.pay')}</Button>
            </>
          )}
          {cancelling && (
            <Button variant="danger" loading={cancel.isPending} disabled={!reason.trim()} onClick={() => cancel.mutate()}>
              {t('hrm.payroll.detail.confirmCancel')}
            </Button>
          )}
          <Button variant="outline" onClick={onClose}>{t('common.close')}</Button>
        </>
      }
    >
      <div className="text-sm">
        <div className="mb-3 flex items-start justify-between">
          <p className="text-ink-secondary">{employeeName} · {slip.payPeriod}</p>
          <StatusBadge status={slip.paymentStatus} />
        </div>

        {calculated ? (
          <>
            <Row label={t('hrm.payroll.detail.baseSalary')} value={money(slip.baseSalary)} />
            <Row label={t('hrm.payroll.detail.workedDays', { worked: slip.workedDays, standard: slip.standardDays })} value={money(slip.grossSalary - slip.allowances)} />
            <Row label={t('hrm.payroll.detail.allowances')} value={money(slip.allowances)} />
            <Row label={t('hrm.payroll.detail.overtime', { hours: slip.overtimeHours })} value={money(slip.overtimePay)} />
            <Row label={t('hrm.payroll.grossSalary')} value={money(slip.grossSalary + slip.overtimePay)} strong />
            <div className="mt-3 text-xs font-semibold uppercase tracking-wide text-ink-secondary">{t('hrm.payroll.detail.withheld')}</div>
            <Row label={t('hrm.payroll.detail.social')} value={`− ${money(slip.employeeSocial)}`} />
            <Row label={t('hrm.payroll.detail.health')} value={`− ${money(slip.employeeHealth)}`} />
            <Row label={t('hrm.payroll.detail.unemployment')} value={`− ${money(slip.employeeUnemployment)}`} />
            <Row label={t('hrm.payroll.detail.taxable', { dependents: slip.dependentCount })} value={money(slip.taxableIncome)} muted />
            <Row label={t('hrm.payroll.detail.pit')} value={`− ${money(slip.personalIncomeTax)}`} />
            <Row label={t('hrm.payroll.netSalary')} value={money(slip.netSalary)} strong />
            <div className="mt-3 rounded-md bg-surface-muted p-3 text-xs text-ink-secondary">
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
            <p className="mt-2 text-xs text-ink-secondary">{t('hrm.payroll.detail.manualSlip')}</p>
          </>
        )}

        {failure != null && <Alert variant="danger" className="mt-3">{serverMessage(failure, t('hrm.payroll.failedToSave'))}</Alert>}

        {cancelling && (
          <div className="mt-4">
            <FormField label={t('hrm.payroll.detail.cancelReason')} required>
              <Input value={reason} onChange={(e) => setReason(e.target.value)} />
            </FormField>
          </div>
        )}
      </div>
    </Modal>
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
      { key: 'grossSalary', label: t('hrm.payroll.grossSalary'), align: 'right' as const, render: (p: PaySlip) => money(p.grossSalary + p.overtimePay) },
      { key: 'deductions', label: t('hrm.payroll.deductions'), align: 'right' as const, render: (p: PaySlip) => money(p.deductions) },
      { key: 'netSalary', label: t('hrm.payroll.netSalary'), align: 'right' as const, render: (p: PaySlip) => money(p.netSalary) },
      {
        key: 'paymentStatus',
        label: t('common.status'),
        render: (p: PaySlip) => <StatusBadge status={p.paymentStatus} />,
      },
      {
        key: 'actions',
        label: '',
        render: (p: PaySlip) => (
          <Button variant="link" className="text-xs" onClick={() => setDetail(p)}>
            {t('hrm.payroll.details')}
          </Button>
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
            <Button variant="outline" onClick={() => setModal('generate')}>
              {t('hrm.payroll.generate')}
            </Button>
            <Button onClick={() => setModal('process')}>
              {t('hrm.payroll.process')}
            </Button>
          </div>
        )}
      </div>

      <div className="flex gap-3">
        <Select
          className="max-w-xs"
          aria-label={t('hrm.employee')}
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
        <Input
          type="number"
          aria-label={t('hrm.payroll.year')}
          placeholder={t('hrm.payroll.year')}
          className="max-w-[8rem]"
          value={yearFilter}
          onChange={(e) => {
            setYearFilter(e.target.value)
            setPage(1)
          }}
        />
        <Input
          type="number"
          min={1}
          max={12}
          aria-label={t('hrm.payroll.month')}
          placeholder={t('hrm.payroll.month')}
          className="max-w-[8rem]"
          value={monthFilter}
          onChange={(e) => {
            setMonthFilter(e.target.value)
            setPage(1)
          }}
        />
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          employeeFilter || yearFilter || monthFilter ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => { setEmployeeFilter(''); setYearFilter(''); setMonthFilter(''); setPage(1) }}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />
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
