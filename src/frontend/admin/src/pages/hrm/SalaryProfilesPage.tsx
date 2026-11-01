import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { payrollSettingsApi } from '@/api/hrm/payrollSettings.api'
import type { SalaryProfile } from '@/api/hrm/payrollSettings.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, Checkbox, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const money = (v: number | null | undefined) => (v == null ? '—' : new Intl.NumberFormat().format(v))
const thisMonth = () => new Date().toISOString().slice(0, 7)

// ─── Salary terms modal ─────────────────────────────────────────────────────

interface TermsForm {
  effectiveFrom: string
  baseSalary: number
  taxableAllowance: number
  exemptAllowance: number
  insuranceSalary: string
  insuranceEnabled: boolean
  taxCode: string
  insuranceNumber: string
}

function SalaryTermsModal({ profile, onClose }: { profile: SalaryProfile; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const current = profile.current
  const { register, handleSubmit, formState: { errors } } = useForm<TermsForm>({
    defaultValues: {
      effectiveFrom: thisMonth(),
      baseSalary: current?.baseSalary ?? 0,
      taxableAllowance: current?.taxableAllowance ?? 0,
      exemptAllowance: current?.exemptAllowance ?? 0,
      insuranceSalary: current?.insuranceSalary != null ? String(current.insuranceSalary) : '',
      insuranceEnabled: current?.insuranceEnabled ?? true,
      taxCode: current?.taxCode ?? '',
      insuranceNumber: current?.insuranceNumber ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (v: TermsForm) =>
      payrollSettingsApi.saveCompensation(profile.employeeId, {
        effectiveFrom: `${v.effectiveFrom}-01`,
        baseSalary: Number(v.baseSalary),
        taxableAllowance: Number(v.taxableAllowance) || 0,
        exemptAllowance: Number(v.exemptAllowance) || 0,
        insuranceSalary: v.insuranceSalary.trim() === '' ? null : Number(v.insuranceSalary),
        insuranceEnabled: v.insuranceEnabled,
        taxCode: v.taxCode.trim() || null,
        insuranceNumber: v.insuranceNumber.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'salaryProfiles'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="md"
      title={`${t('hrm.salaryProfiles.termsTitle')} — ${profile.fullName} (${profile.employeeNumber})`}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="salary-terms-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="salary-terms-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.salaryProfiles.effectiveFrom')} required error={errors.effectiveFrom ? t('common.required') : undefined}>
            <Input type="month" {...register('effectiveFrom', { required: true })} />
          </FormField>
          <FormField label={t('hrm.salaryProfiles.baseSalary')} required error={errors.baseSalary ? t('common.required') : undefined}>
            <Input type="number" step="1" {...register('baseSalary', { required: true, min: 1, valueAsNumber: true })} />
          </FormField>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.salaryProfiles.taxableAllowance')}>
            <Input type="number" step="1" {...register('taxableAllowance', { valueAsNumber: true })} />
          </FormField>
          <FormField label={t('hrm.salaryProfiles.exemptAllowance')}>
            <Input type="number" step="1" {...register('exemptAllowance', { valueAsNumber: true })} />
          </FormField>
        </div>
        <FormField label={t('hrm.salaryProfiles.insuranceSalary')}>
          <Input type="number" step="1" placeholder={t('hrm.salaryProfiles.insuranceSalaryHint')} {...register('insuranceSalary')} />
        </FormField>
        <Checkbox label={t('hrm.salaryProfiles.insuranceEnabled')} {...register('insuranceEnabled')} />
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.salaryProfiles.taxCode')}>
            <Input maxLength={20} {...register('taxCode')} />
          </FormField>
          <FormField label={t('hrm.salaryProfiles.insuranceNumber')}>
            <Input maxLength={20} {...register('insuranceNumber')} />
          </FormField>
        </div>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.payroll.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Dependents modal ───────────────────────────────────────────────────────

interface DependentForm {
  fullName: string
  relationship: string
  taxCode: string
  fromMonth: string
  toMonth: string
}

function DependentsModal({ profile, onClose }: { profile: SalaryProfile; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const key = ['hrm', 'dependents', profile.employeeId]
  const { data: dependents = [], isLoading } = useQuery({ queryKey: key, queryFn: () => payrollSettingsApi.dependents(profile.employeeId) })
  const { register, handleSubmit, reset, formState: { errors } } = useForm<DependentForm>({
    defaultValues: { fullName: '', relationship: '', taxCode: '', fromMonth: thisMonth(), toMonth: '' },
  })

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: key })
    queryClient.invalidateQueries({ queryKey: ['hrm', 'salaryProfiles'] })
  }
  const add = useMutation({
    mutationFn: (v: DependentForm) =>
      payrollSettingsApi.addDependent(profile.employeeId, {
        fullName: v.fullName,
        relationship: v.relationship,
        taxCode: v.taxCode.trim() || null,
        fromMonth: `${v.fromMonth}-01`,
        toMonth: v.toMonth ? `${v.toMonth}-01` : null,
      }),
    onSuccess: () => {
      reset({ fullName: '', relationship: '', taxCode: '', fromMonth: thisMonth(), toMonth: '' })
      toast.success(t('common.saved'))
      refresh()
    },
  })
  const remove = useMutation({
    mutationFn: (id: string) => payrollSettingsApi.removeDependent(id),
    onSuccess: () => {
      toast.success(t('common.saved'))
      refresh()
    },
    onError: (e) => toast.error(serverMessage(e, t('hrm.payroll.failedToSave'))),
  })

  return (
    <Modal
      open
      size="lg"
      title={`${t('hrm.salaryProfiles.dependentsTitle')} — ${profile.fullName} (${profile.employeeNumber})`}
      onClose={onClose}
      closeOnEsc={!add.isPending}
      footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}
    >
      <table className="mb-4 min-w-full divide-y divide-line text-sm">
        <thead className="bg-surface-muted text-left text-xs uppercase text-ink-secondary">
          <tr>
            <th className="px-3 py-2">{t('hrm.salaryProfiles.dependentName')}</th>
            <th className="px-3 py-2">{t('hrm.salaryProfiles.relationship')}</th>
            <th className="px-3 py-2">{t('hrm.salaryProfiles.fromMonth')}</th>
            <th className="px-3 py-2">{t('hrm.salaryProfiles.toMonth')}</th>
            <th><span className="sr-only">{t('common.actions')}</span></th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {isLoading && <tr><td className="px-3 py-3 text-ink-secondary" colSpan={5}>{t('common.loading')}</td></tr>}
          {!isLoading && dependents.length === 0 && (
            <tr><td className="px-3 py-3 text-ink-secondary" colSpan={5}>{t('hrm.salaryProfiles.noDependents')}</td></tr>
          )}
          {dependents.map((d) => (
            <tr key={d.id}>
              <td className="px-3 py-2">{d.fullName}{d.taxCode ? ` · ${d.taxCode}` : ''}</td>
              <td className="px-3 py-2">{d.relationship}</td>
              <td className="px-3 py-2">{d.fromMonth.slice(0, 7)}</td>
              <td className="px-3 py-2">{d.toMonth ? d.toMonth.slice(0, 7) : t('hrm.salaryProfiles.openEnded')}</td>
              <td className="px-3 py-2 text-right">
                <Button variant="link-danger" className="text-xs" onClick={() => remove.mutate(d.id)}>
                  {t('hrm.salaryProfiles.removeDependent')}
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <form onSubmit={handleSubmit((v) => add.mutate(v))} className="space-y-3 rounded-lg border border-line p-4">
        <p className="text-sm font-medium text-ink">{t('hrm.salaryProfiles.addDependent')}</p>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.salaryProfiles.dependentName')} required error={errors.fullName ? t('common.required') : undefined}>
            <Input {...register('fullName', { required: true })} />
          </FormField>
          <FormField label={t('hrm.salaryProfiles.relationship')} required error={errors.relationship ? t('common.required') : undefined}>
            <Input placeholder={t('hrm.salaryProfiles.relationshipHint')} {...register('relationship', { required: true })} />
          </FormField>
        </div>
        <div className="grid grid-cols-3 gap-3">
          <FormField label={t('hrm.salaryProfiles.taxCode')}>
            <Input maxLength={20} {...register('taxCode')} />
          </FormField>
          <FormField label={t('hrm.salaryProfiles.fromMonth')} required error={errors.fromMonth ? t('common.required') : undefined}>
            <Input type="month" {...register('fromMonth', { required: true })} />
          </FormField>
          <FormField label={t('hrm.salaryProfiles.toMonth')}>
            <Input type="month" {...register('toMonth')} />
          </FormField>
        </div>
        {add.isError && <Alert variant="danger">{serverMessage(add.error, t('hrm.payroll.failedToSave'))}</Alert>}
        <div className="flex justify-end">
          <Button type="submit" loading={add.isPending}>{t('hrm.salaryProfiles.addDependent')}</Button>
        </div>
      </form>
    </Modal>
  )
}

// ─── Page ───────────────────────────────────────────────────────────────────

export function SalaryProfilesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')
  const [terms, setTerms] = useState<SalaryProfile | null>(null)
  const [dependents, setDependents] = useState<SalaryProfile | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['hrm', 'salaryProfiles', { page, pageSize, search }],
    queryFn: () => payrollSettingsApi.salaryProfiles({ page, pageSize, search: search || undefined }),
  })
  const canManage = hasPermission('hrm:payroll:manage')

  const rows = useMemo(() => (data?.items ?? []).map((p) => ({ ...p, id: p.employeeId })), [data])
  const columns = useMemo(
    () => [
      { key: 'employeeNumber', label: t('hrm.salaryProfiles.employeeNumber'), className: 'font-mono text-xs' },
      { key: 'fullName', label: t('hrm.employee') },
      {
        key: 'baseSalary',
        label: t('hrm.salaryProfiles.baseSalary'),
        align: 'right' as const,
        render: (p: SalaryProfile) =>
          p.current ? money(p.current.baseSalary) : <span className="text-warning-soft-text">{t('hrm.salaryProfiles.noProfile')}</span>,
      },
      {
        key: 'allowances',
        label: t('hrm.salaryProfiles.allowances'),
        render: (p: SalaryProfile) => (p.current ? money(p.current.taxableAllowance + p.current.exemptAllowance) : '—'),
      },
      {
        key: 'insurance',
        label: t('hrm.salaryProfiles.insuranceSalary'),
        render: (p: SalaryProfile) =>
          !p.current ? '—' : !p.current.insuranceEnabled ? t('hrm.salaryProfiles.noInsurance') : money(p.current.insuranceSalary ?? p.current.baseSalary),
      },
      { key: 'taxCode', label: t('hrm.salaryProfiles.taxCode'), render: (p: SalaryProfile) => p.current?.taxCode ?? '—' },
      { key: 'dependentCount', label: t('hrm.salaryProfiles.dependents'), align: 'right' as const },
      {
        key: 'actions',
        label: '',
        render: (p: SalaryProfile) =>
          canManage ? (
            <div className="flex gap-3">
              <Button variant="link" className="text-xs" onClick={() => setTerms(p)}>{t('hrm.salaryProfiles.editTerms')}</Button>
              <Button variant="link" className="text-xs" onClick={() => setDependents(p)}>{t('hrm.salaryProfiles.manageDependents')}</Button>
            </div>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{t('hrm.salaryProfiles.title')}</h1>
        <p className="text-sm text-ink-secondary">{t('hrm.salaryProfiles.hint')}</p>
      </div>
      <Input
        className="max-w-xs"
        aria-label={t('common.search')}
        placeholder={t('common.search')}
        value={search}
        onChange={(e) => {
          setSearch(e.target.value)
          setPage(1)
        }}
      />
      <DataTable columns={columns} data={rows} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
      {terms && <SalaryTermsModal profile={terms} onClose={() => setTerms(null)} />}
      {dependents && <DependentsModal profile={dependents} onClose={() => setDependents(null)} />}
    </div>
  )
}
