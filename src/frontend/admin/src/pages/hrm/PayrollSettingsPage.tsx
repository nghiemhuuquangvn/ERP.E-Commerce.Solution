import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { payrollSettingsApi } from '@/api/hrm/payrollSettings.api'
import type { PayrollRateSet } from '@/api/hrm/payrollSettings.api'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Modal, Spinner, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
  const [brackets, setBrackets] = useState<BracketRow[]>(
    initial.brackets.map((b) => ({ upTo: b.upTo == null ? '' : String(b.upTo), rate: String(toPct(b.rate)) })),
  )
  const { register, handleSubmit, formState: { errors } } = useForm<RateForm>({
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  const num = (name: keyof RateForm, step = '0.001', ariaLabel?: string) => (
    <Input type="number" step={step} aria-label={ariaLabel} {...register(name, { valueAsNumber: true })} />
  )
  const employee = t('hrm.payrollSettings.employeeShare')
  const employer = t('hrm.payrollSettings.employerShare')
  const social = t('hrm.payrollSettings.social')
  const health = t('hrm.payrollSettings.health')
  const unemployment = t('hrm.payrollSettings.unemployment')

  return (
    <Modal
      open
      size="lg"
      title={isNew ? t('hrm.payrollSettings.newTitle') : t('hrm.payrollSettings.editTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="rate-set-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="rate-set-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-5">
        <div className="grid grid-cols-3 gap-3">
          <FormField className="col-span-2" label={t('hrm.payrollSettings.name')} required error={errors.name ? t('common.required') : undefined}>
            <Input {...register('name', { required: true })} />
          </FormField>
          <FormField label={t('hrm.payrollSettings.effectiveFrom')} required error={errors.effectiveFrom ? t('common.required') : undefined}>
            <Input type="month" {...register('effectiveFrom', { required: true })} />
          </FormField>
        </div>

        <fieldset>
          <legend className="mb-2 text-sm font-semibold text-ink">{t('hrm.payrollSettings.insurance')}</legend>
          <div className="grid grid-cols-4 gap-3 text-sm">
            <span />
            <span className="font-medium text-ink-secondary">{social}</span>
            <span className="font-medium text-ink-secondary">{health}</span>
            <span className="font-medium text-ink-secondary">{unemployment}</span>
            <span className="self-center text-ink-secondary">{employee} (%)</span>
            {num('socialEmployeeRate', '0.001', `${employee} (%) - ${social}`)}
            {num('healthEmployeeRate', '0.001', `${employee} (%) - ${health}`)}
            {num('unemploymentEmployeeRate', '0.001', `${employee} (%) - ${unemployment}`)}
            <span className="self-center text-ink-secondary">{employer} (%)</span>
            {num('socialEmployerRate', '0.001', `${employer} (%) - ${social}`)}
            {num('healthEmployerRate', '0.001', `${employer} (%) - ${health}`)}
            {num('unemploymentEmployerRate', '0.001', `${employer} (%) - ${unemployment}`)}
          </div>
        </fieldset>

        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('hrm.payrollSettings.insuranceCap')}>{num('insuranceSalaryCap', '1')}</FormField>
          <FormField label={t('hrm.payrollSettings.unemploymentCap')}>{num('unemploymentSalaryCap', '1')}</FormField>
          <FormField label={t('hrm.payrollSettings.personalDeduction')}>{num('personalDeduction', '1')}</FormField>
          <FormField label={t('hrm.payrollSettings.dependentDeduction')}>{num('dependentDeduction', '1')}</FormField>
          <FormField label={t('hrm.payrollSettings.annualLeaveDays')}>{num('annualLeaveDays', '0.5')}</FormField>
        </div>

        <fieldset>
          <legend className="mb-2 text-sm font-semibold text-ink">{t('hrm.payrollSettings.brackets')}</legend>
          <p className="mb-2 text-xs text-ink-secondary">{t('hrm.payrollSettings.bracketsHint')}</p>
          <table className="min-w-full text-sm">
            <thead className="text-left text-xs uppercase text-ink-secondary">
              <tr><th className="py-1">{t('hrm.payrollSettings.upTo')}</th><th className="py-1">{t('hrm.payrollSettings.rate')} (%)</th><th><span className="sr-only">{t('common.actions')}</span></th></tr>
            </thead>
            <tbody>
              {brackets.map((b, i) => (
                <tr key={i}>
                  <td className="py-1 pr-2">
                    <Input
                      type="number"
                      aria-label={`${t('hrm.payrollSettings.upTo')} ${i + 1}`}
                      placeholder={t('hrm.payrollSettings.noLimit')}
                      value={b.upTo}
                      onChange={(e) => setBrackets(brackets.map((x, j) => (j === i ? { ...x, upTo: e.target.value } : x)))}
                    />
                  </td>
                  <td className="py-1 pr-2">
                    <Input
                      type="number"
                      step="0.5"
                      aria-label={`${t('hrm.payrollSettings.rate')} (%) ${i + 1}`}
                      value={b.rate}
                      onChange={(e) => setBrackets(brackets.map((x, j) => (j === i ? { ...x, rate: e.target.value } : x)))}
                    />
                  </td>
                  <td className="py-1 text-right">
                    <Button variant="link-danger" className="text-xs" onClick={() => setBrackets(brackets.filter((_, j) => j !== i))}>
                      {t('common.delete')}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <Button variant="outline" size="sm" className="mt-2" onClick={() => setBrackets([...brackets, { upTo: '', rate: '0' }])}>
            {t('hrm.payrollSettings.addBracket')}
          </Button>
        </fieldset>

        <Checkbox label={t('hrm.payrollSettings.confirmed')} {...register('isConfirmed')} />

        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('hrm.payroll.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
          <p className="text-sm text-ink-secondary">{t('hrm.payrollSettings.hint')}</p>
        </div>
        {canManage && <Button onClick={startNew}>{t('hrm.payrollSettings.add')}</Button>}
      </div>

      {isLoading && <Spinner />}
      {!isLoading && sets.length === 0 && <EmptyState title={t('common.noRecordsFound')} />}

      {sets.map((s) => (
        <div key={s.id} className="card space-y-3 p-5">
          <div className="flex items-start justify-between">
            <div>
              <h2 className="text-sm font-semibold text-ink">{s.name}</h2>
              <p className="text-xs text-ink-secondary">{t('hrm.payrollSettings.effectiveFrom')}: {s.effectiveFrom.slice(0, 7)}</p>
            </div>
            <div className="flex items-center gap-3">
              <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${s.isConfirmed ? 'bg-success-soft text-success-soft-text' : 'bg-warning-soft text-warning-soft-text'}`}>
                {s.isConfirmed ? t('hrm.payrollSettings.statusConfirmed') : t('hrm.payrollSettings.statusDraft')}
              </span>
              {canManage && (
                <Button variant="link" className="text-xs" onClick={() => setEditing({ set: s, isNew: false })}>
                  {t('common.edit')}
                </Button>
              )}
            </div>
          </div>

          <div className="grid gap-4 text-sm md:grid-cols-2">
            <table className="min-w-full">
              <thead className="text-left text-xs uppercase text-ink-secondary">
                <tr><th /><th>{t('hrm.payrollSettings.social')}</th><th>{t('hrm.payrollSettings.health')}</th><th>{t('hrm.payrollSettings.unemployment')}</th></tr>
              </thead>
              <tbody>
                <tr><td className="py-1 text-ink-secondary">{t('hrm.payrollSettings.employeeShare')}</td><td>{pct(s.socialEmployeeRate)}</td><td>{pct(s.healthEmployeeRate)}</td><td>{pct(s.unemploymentEmployeeRate)}</td></tr>
                <tr><td className="py-1 text-ink-secondary">{t('hrm.payrollSettings.employerShare')}</td><td>{pct(s.socialEmployerRate)}</td><td>{pct(s.healthEmployerRate)}</td><td>{pct(s.unemploymentEmployerRate)}</td></tr>
              </tbody>
            </table>
            <dl className="grid grid-cols-2 gap-x-4 gap-y-1">
              <dt className="text-ink-secondary">{t('hrm.payrollSettings.personalDeduction')}</dt><dd>{money(s.personalDeduction)}</dd>
              <dt className="text-ink-secondary">{t('hrm.payrollSettings.dependentDeduction')}</dt><dd>{money(s.dependentDeduction)}</dd>
              <dt className="text-ink-secondary">{t('hrm.payrollSettings.insuranceCap')}</dt><dd>{money(s.insuranceSalaryCap)}</dd>
              <dt className="text-ink-secondary">{t('hrm.payrollSettings.unemploymentCap')}</dt><dd>{money(s.unemploymentSalaryCap)}</dd>
              <dt className="text-ink-secondary">{t('hrm.payrollSettings.annualLeaveDays')}</dt><dd>{s.annualLeaveDays}</dd>
            </dl>
          </div>

          <table className="min-w-full text-sm">
            <thead className="text-left text-xs uppercase text-ink-secondary">
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
