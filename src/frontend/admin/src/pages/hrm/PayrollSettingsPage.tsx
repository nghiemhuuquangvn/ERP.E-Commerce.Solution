import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { payrollSettingsApi } from '@/api/hrm/payrollSettings.api'
import type { PayrollRateSet } from '@/api/hrm/payrollSettings.api'
import { serverMessage } from '@/api/hrm/payroll.api'
import { usePermission } from '@/hooks/usePermission'

const money = (v: number) => new Intl.NumberFormat().format(v)
const pct = (v: number) => `${Math.round(v * 100000) / 1000}%`
const toPct = (v: number) => Math.round(v * 100000) / 1000
const fromPct = (v: number) => Math.round(Number(v) * 1000) / 100000

// ─── Edit modal ─────────────────────────────────────────────────────────────

interface RateForm {
  name: string
  effectiveFrom: string
  socialEmployeeRate: number
  healthEmployeeRate: number
  unemploymentEmployeeRate: number
  socialEmployerRate: number
  healthEmployerRate: number
  unemploymentEmployerRate: number
  personalDeduction: number
  dependentDeduction: number
  insuranceSalaryCap: number
  unemploymentSalaryCap: number
  annualLeaveDays: number
  isConfirmed: boolean
}

interface BracketRow {
  upTo: string // '' = no upper limit
  rate: string // percent
}

function RateSetModal({ initial, isNew, onClose }: { initial: PayrollRateSet; isNew: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [brackets, setBrackets] = useState<BracketRow[]>(
    initial.brackets.map((b) => ({ upTo: b.upTo == null ? '' : String(b.upTo), rate: String(toPct(b.rate)) })),
  )
  const { register, handleSubmit } = useForm<RateForm>({
    defaultValues: {
      name: initial.name,
      effectiveFrom: initial.effectiveFrom.slice(0, 7),
      socialEmployeeRate: toPct(initial.socialEmployeeRate),
      healthEmployeeRate: toPct(initial.healthEmployeeRate),
      unemploymentEmployeeRate: toPct(initial.unemploymentEmployeeRate),
      socialEmployerRate: toPct(initial.socialEmployerRate),
      healthEmployerRate: toPct(initial.healthEmployerRate),
      unemploymentEmployerRate: toPct(initial.unemploymentEmployerRate),
      personalDeduction: initial.personalDeduction,
      dependentDeduction: initial.dependentDeduction,
      insuranceSalaryCap: initial.insuranceSalaryCap,
      unemploymentSalaryCap: initial.unemploymentSalaryCap,
      annualLeaveDays: initial.annualLeaveDays,
      isConfirmed: initial.isConfirmed,
    },
  })

  const mutation = useMutation({
    mutationFn: (v: RateForm) =>
      payrollSettingsApi.saveRateSet({
        id: isNew ? null : initial.id,
        name: v.name,
        effectiveFrom: `${v.effectiveFrom}-01`,
        socialEmployeeRate: fromPct(v.socialEmployeeRate),
        healthEmployeeRate: fromPct(v.healthEmployeeRate),
        unemploymentEmployeeRate: fromPct(v.unemploymentEmployeeRate),
        socialEmployerRate: fromPct(v.socialEmployerRate),
        healthEmployerRate: fromPct(v.healthEmployerRate),
        unemploymentEmployerRate: fromPct(v.unemploymentEmployerRate),
        personalDeduction: Number(v.personalDeduction),
        dependentDeduction: Number(v.dependentDeduction),
        insuranceSalaryCap: Number(v.insuranceSalaryCap),
        unemploymentSalaryCap: Number(v.unemploymentSalaryCap),
        annualLeaveDays: Number(v.annualLeaveDays),
        isConfirmed: v.isConfirmed,
        brackets: brackets.map((b) => ({ upTo: b.upTo.trim() === '' ? null : Number(b.upTo), rate: fromPct(Number(b.rate)) })),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hrm', 'rateSets'] })
      onClose()
    },
  })

  const label = 'mb-1 block text-sm font-medium text-gray-700'
  const num = (name: keyof RateForm, step = '0.001') => (
    <input type="number" step={step} className="input" {...register(name, { valueAsNumber: true })} />
  )

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[92vh] w-full max-w-3xl overflow-y-auto p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isNew ? t('hrm.payrollSettings.newTitle') : t('hrm.payrollSettings.editTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-5">
          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-2">
              <label className={label}>{t('hrm.payrollSettings.name')}</label>
              <input className="input" {...register('name', { required: true })} />
            </div>
            <div>
              <label className={label}>{t('hrm.payrollSettings.effectiveFrom')}</label>
              <input type="month" className="input" {...register('effectiveFrom', { required: true })} />
            </div>
          </div>

          <div>
            <p className="mb-2 text-sm font-semibold text-gray-700">{t('hrm.payrollSettings.insurance')}</p>
            <div className="grid grid-cols-4 gap-3 text-sm">
              <span />
              <span className="font-medium text-gray-500">{t('hrm.payrollSettings.social')}</span>
              <span className="font-medium text-gray-500">{t('hrm.payrollSettings.health')}</span>
              <span className="font-medium text-gray-500">{t('hrm.payrollSettings.unemployment')}</span>
              <span className="self-center text-gray-700">{t('hrm.payrollSettings.employeeShare')} (%)</span>
              {num('socialEmployeeRate')}
              {num('healthEmployeeRate')}
              {num('unemploymentEmployeeRate')}
              <span className="self-center text-gray-700">{t('hrm.payrollSettings.employerShare')} (%)</span>
              {num('socialEmployerRate')}
              {num('healthEmployerRate')}
              {num('unemploymentEmployerRate')}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div><label className={label}>{t('hrm.payrollSettings.insuranceCap')}</label>{num('insuranceSalaryCap', '1')}</div>
            <div><label className={label}>{t('hrm.payrollSettings.unemploymentCap')}</label>{num('unemploymentSalaryCap', '1')}</div>
            <div><label className={label}>{t('hrm.payrollSettings.personalDeduction')}</label>{num('personalDeduction', '1')}</div>
            <div><label className={label}>{t('hrm.payrollSettings.dependentDeduction')}</label>{num('dependentDeduction', '1')}</div>
            <div><label className={label}>{t('hrm.payrollSettings.annualLeaveDays')}</label>{num('annualLeaveDays', '0.5')}</div>
          </div>

          <div>
            <p className="mb-2 text-sm font-semibold text-gray-700">{t('hrm.payrollSettings.brackets')}</p>
            <p className="mb-2 text-xs text-gray-500">{t('hrm.payrollSettings.bracketsHint')}</p>
            <table className="min-w-full text-sm">
              <thead className="text-left text-xs uppercase text-gray-500">
                <tr><th className="py-1">{t('hrm.payrollSettings.upTo')}</th><th className="py-1">{t('hrm.payrollSettings.rate')} (%)</th><th /></tr>
              </thead>
              <tbody>
                {brackets.map((b, i) => (
                  <tr key={i}>
                    <td className="pr-2 py-1">
                      <input
                        type="number"
                        className="input"
                        placeholder={t('hrm.payrollSettings.noLimit')}
                        value={b.upTo}
                        onChange={(e) => setBrackets(brackets.map((x, j) => (j === i ? { ...x, upTo: e.target.value } : x)))}
                      />
                    </td>
                    <td className="pr-2 py-1">
                      <input
                        type="number"
                        step="0.5"
                        className="input"
                        value={b.rate}
                        onChange={(e) => setBrackets(brackets.map((x, j) => (j === i ? { ...x, rate: e.target.value } : x)))}
                      />
                    </td>
                    <td className="py-1 text-right">
                      <button type="button" className="text-xs text-red-600 hover:underline" onClick={() => setBrackets(brackets.filter((_, j) => j !== i))}>
                        {t('common.delete')}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <button type="button" className="btn-outline mt-2" onClick={() => setBrackets([...brackets, { upTo: '', rate: '0' }])}>
              {t('hrm.payrollSettings.addBracket')}
            </button>
          </div>

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" {...register('isConfirmed')} />
            {t('hrm.payrollSettings.confirmed')}
          </label>

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

// ─── Page ───────────────────────────────────────────────────────────────────

export function PayrollSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('hrm:payroll:manage')
  const [editing, setEditing] = useState<{ set: PayrollRateSet; isNew: boolean } | null>(null)

  const { data: sets = [], isLoading } = useQuery({ queryKey: ['hrm', 'rateSets'], queryFn: payrollSettingsApi.rateSets })

  const startNew = () => {
    const latest = sets[0]
    const next = new Date()
    setEditing({
      isNew: true,
      set: {
        ...(latest ?? ({} as PayrollRateSet)),
        id: '',
        name: '',
        effectiveFrom: new Date(next.getFullYear() + 1, 0, 1).toISOString(),
        isConfirmed: false,
        brackets: latest?.brackets ?? [{ upTo: null, rate: 0.35 }],
      },
    })
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t('hrm.payrollSettings.title')}</h1>
          <p className="text-sm text-gray-500">{t('hrm.payrollSettings.hint')}</p>
        </div>
        {canManage && <button className="btn-primary" onClick={startNew}>{t('hrm.payrollSettings.add')}</button>}
      </div>

      {isLoading && <p className="text-gray-400">{t('common.loading')}</p>}

      {sets.map((s) => (
        <div key={s.id} className="card space-y-3 p-5">
          <div className="flex items-start justify-between">
            <div>
              <h2 className="text-sm font-semibold text-gray-900">{s.name}</h2>
              <p className="text-xs text-gray-500">{t('hrm.payrollSettings.effectiveFrom')}: {s.effectiveFrom.slice(0, 7)}</p>
            </div>
            <div className="flex items-center gap-3">
              <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${s.isConfirmed ? 'bg-green-100 text-green-700' : 'bg-amber-100 text-amber-700'}`}>
                {s.isConfirmed ? t('hrm.payrollSettings.statusConfirmed') : t('hrm.payrollSettings.statusDraft')}
              </span>
              {canManage && (
                <button className="text-xs text-blue-600 hover:underline" onClick={() => setEditing({ set: s, isNew: false })}>
                  {t('common.edit')}
                </button>
              )}
            </div>
          </div>

          <div className="grid gap-4 text-sm md:grid-cols-2">
            <table className="min-w-full">
              <thead className="text-left text-xs uppercase text-gray-500">
                <tr><th /><th>{t('hrm.payrollSettings.social')}</th><th>{t('hrm.payrollSettings.health')}</th><th>{t('hrm.payrollSettings.unemployment')}</th></tr>
              </thead>
              <tbody>
                <tr><td className="py-1 text-gray-600">{t('hrm.payrollSettings.employeeShare')}</td><td>{pct(s.socialEmployeeRate)}</td><td>{pct(s.healthEmployeeRate)}</td><td>{pct(s.unemploymentEmployeeRate)}</td></tr>
                <tr><td className="py-1 text-gray-600">{t('hrm.payrollSettings.employerShare')}</td><td>{pct(s.socialEmployerRate)}</td><td>{pct(s.healthEmployerRate)}</td><td>{pct(s.unemploymentEmployerRate)}</td></tr>
              </tbody>
            </table>
            <dl className="grid grid-cols-2 gap-x-4 gap-y-1">
              <dt className="text-gray-600">{t('hrm.payrollSettings.personalDeduction')}</dt><dd>{money(s.personalDeduction)}</dd>
              <dt className="text-gray-600">{t('hrm.payrollSettings.dependentDeduction')}</dt><dd>{money(s.dependentDeduction)}</dd>
              <dt className="text-gray-600">{t('hrm.payrollSettings.insuranceCap')}</dt><dd>{money(s.insuranceSalaryCap)}</dd>
              <dt className="text-gray-600">{t('hrm.payrollSettings.unemploymentCap')}</dt><dd>{money(s.unemploymentSalaryCap)}</dd>
              <dt className="text-gray-600">{t('hrm.payrollSettings.annualLeaveDays')}</dt><dd>{s.annualLeaveDays}</dd>
            </dl>
          </div>

          <table className="min-w-full text-sm">
            <thead className="text-left text-xs uppercase text-gray-500">
              <tr><th className="py-1">{t('hrm.payrollSettings.upTo')}</th><th>{t('hrm.payrollSettings.rate')}</th></tr>
            </thead>
            <tbody>
              {s.brackets.map((b, i) => (
                <tr key={i}><td className="py-1">{b.upTo == null ? t('hrm.payrollSettings.noLimit') : money(b.upTo)}</td><td>{pct(b.rate)}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}

      {editing && <RateSetModal initial={editing.set} isNew={editing.isNew} onClose={() => setEditing(null)} />}
    </div>
  )
}
