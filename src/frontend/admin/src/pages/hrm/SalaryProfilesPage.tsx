import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { payrollSettingsApi } from '@/api/hrm/payrollSettings.api'
import type { SalaryProfile } from '@/api/hrm/payrollSettings.api'
import { serverMessage } from '@/api/hrm/payroll.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

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
      onClose()
    },
  })

  const field = 'mb-1 block text-sm font-medium text-gray-700'
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-lg overflow-y-auto p-6">
        <h3 className="mb-1 text-base font-semibold text-gray-900">{t('hrm.salaryProfiles.termsTitle')}</h3>
        <p className="mb-4 text-sm text-gray-500">{profile.fullName} ({profile.employeeNumber})</p>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={field}>{t('hrm.salaryProfiles.effectiveFrom')}</label>
              <input type="month" className="input" {...register('effectiveFrom', { required: true })} />
            </div>
            <div>
              <label className={field}>{t('hrm.salaryProfiles.baseSalary')}</label>
              <input type="number" step="1" className="input" {...register('baseSalary', { required: true, min: 1, valueAsNumber: true })} />
              {errors.baseSalary && <p className="mt-1 text-xs text-red-600">{t('hrm.required')}</p>}
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={field}>{t('hrm.salaryProfiles.taxableAllowance')}</label>
              <input type="number" step="1" className="input" {...register('taxableAllowance', { valueAsNumber: true })} />
            </div>
            <div>
              <label className={field}>{t('hrm.salaryProfiles.exemptAllowance')}</label>
              <input type="number" step="1" className="input" {...register('exemptAllowance', { valueAsNumber: true })} />
            </div>
          </div>
          <div>
            <label className={field}>{t('hrm.salaryProfiles.insuranceSalary')}</label>
            <input type="number" step="1" className="input" placeholder={t('hrm.salaryProfiles.insuranceSalaryHint')} {...register('insuranceSalary')} />
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" {...register('insuranceEnabled')} />
            {t('hrm.salaryProfiles.insuranceEnabled')}
          </label>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={field}>{t('hrm.salaryProfiles.taxCode')}</label>
              <input className="input" maxLength={20} {...register('taxCode')} />
            </div>
            <div>
              <label className={field}>{t('hrm.salaryProfiles.insuranceNumber')}</label>
              <input className="input" maxLength={20} {...register('insuranceNumber')} />
            </div>
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{serverMessage(mutation.error, t('hrm.payroll.failedToSave'))}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">{t('common.cancel')}</button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
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
  const key = ['hrm', 'dependents', profile.employeeId]
  const { data: dependents = [], isLoading } = useQuery({ queryKey: key, queryFn: () => payrollSettingsApi.dependents(profile.employeeId) })
  const { register, handleSubmit, reset } = useForm<DependentForm>({
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
      refresh()
    },
  })
  const remove = useMutation({ mutationFn: (id: string) => payrollSettingsApi.removeDependent(id), onSuccess: refresh })

  const field = 'mb-1 block text-sm font-medium text-gray-700'
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-2xl overflow-y-auto p-6">
        <h3 className="mb-1 text-base font-semibold text-gray-900">{t('hrm.salaryProfiles.dependentsTitle')}</h3>
        <p className="mb-4 text-sm text-gray-500">{profile.fullName} ({profile.employeeNumber})</p>

        <table className="mb-4 min-w-full divide-y divide-gray-200 text-sm">
          <thead className="bg-gray-50 text-left text-xs uppercase text-gray-500">
            <tr>
              <th className="px-3 py-2">{t('hrm.salaryProfiles.dependentName')}</th>
              <th className="px-3 py-2">{t('hrm.salaryProfiles.relationship')}</th>
              <th className="px-3 py-2">{t('hrm.salaryProfiles.fromMonth')}</th>
              <th className="px-3 py-2">{t('hrm.salaryProfiles.toMonth')}</th>
              <th />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading && <tr><td className="px-3 py-3 text-gray-400" colSpan={5}>{t('common.loading')}</td></tr>}
            {!isLoading && dependents.length === 0 && (
              <tr><td className="px-3 py-3 text-gray-400" colSpan={5}>{t('hrm.salaryProfiles.noDependents')}</td></tr>
            )}
            {dependents.map((d) => (
              <tr key={d.id}>
                <td className="px-3 py-2">{d.fullName}{d.taxCode ? ` · ${d.taxCode}` : ''}</td>
                <td className="px-3 py-2">{d.relationship}</td>
                <td className="px-3 py-2">{d.fromMonth.slice(0, 7)}</td>
                <td className="px-3 py-2">{d.toMonth ? d.toMonth.slice(0, 7) : t('hrm.salaryProfiles.openEnded')}</td>
                <td className="px-3 py-2 text-right">
                  <button className="text-xs text-red-600 hover:underline" onClick={() => remove.mutate(d.id)}>
                    {t('hrm.salaryProfiles.removeDependent')}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <form onSubmit={handleSubmit((v) => add.mutate(v))} className="space-y-3 rounded-lg border border-gray-200 p-4">
          <p className="text-sm font-medium text-gray-700">{t('hrm.salaryProfiles.addDependent')}</p>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={field}>{t('hrm.salaryProfiles.dependentName')}</label>
              <input className="input" {...register('fullName', { required: true })} />
            </div>
            <div>
              <label className={field}>{t('hrm.salaryProfiles.relationship')}</label>
              <input className="input" placeholder={t('hrm.salaryProfiles.relationshipHint')} {...register('relationship', { required: true })} />
            </div>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className={field}>{t('hrm.salaryProfiles.taxCode')}</label>
              <input className="input" maxLength={20} {...register('taxCode')} />
            </div>
            <div>
              <label className={field}>{t('hrm.salaryProfiles.fromMonth')}</label>
              <input type="month" className="input" {...register('fromMonth', { required: true })} />
            </div>
            <div>
              <label className={field}>{t('hrm.salaryProfiles.toMonth')}</label>
              <input type="month" className="input" {...register('toMonth')} />
            </div>
          </div>
          {add.isError && <p className="text-xs text-red-600">{serverMessage(add.error, t('hrm.payroll.failedToSave'))}</p>}
          <div className="flex justify-end">
            <button type="submit" disabled={add.isPending} className="btn-primary">{t('hrm.salaryProfiles.addDependent')}</button>
          </div>
        </form>

        <div className="mt-4 flex justify-end">
          <button type="button" onClick={onClose} className="btn-outline">{t('common.close')}</button>
        </div>
      </div>
    </div>
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
        render: (p: SalaryProfile) =>
          p.current ? money(p.current.baseSalary) : <span className="text-amber-600">{t('hrm.salaryProfiles.noProfile')}</span>,
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
      { key: 'dependentCount', label: t('hrm.salaryProfiles.dependents') },
      {
        key: 'actions',
        label: '',
        render: (p: SalaryProfile) =>
          canManage ? (
            <div className="flex gap-3">
              <button className="text-xs text-blue-600 hover:underline" onClick={() => setTerms(p)}>{t('hrm.salaryProfiles.editTerms')}</button>
              <button className="text-xs text-blue-600 hover:underline" onClick={() => setDependents(p)}>{t('hrm.salaryProfiles.manageDependents')}</button>
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
        <p className="text-sm text-gray-500">{t('hrm.salaryProfiles.hint')}</p>
      </div>
      <input
        className="input max-w-xs"
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
