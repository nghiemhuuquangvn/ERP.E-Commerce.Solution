import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { shippingApi, type CarrierChoicePreview, type CarrierRule, type QuoteParcel, type SaveCarrierRule } from '@/api/shipping.api'
import type { CarrierCode } from '@/api/settings/shipping.settings.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Alert, Badge, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, Spinner, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const CARRIERS: CarrierCode[] = ['GHTK', 'VTP', 'GHN']

const EMPTY: SaveCarrierRule = {
  name: '', priority: 100, isActive: true, matchProvinces: '', minWeightGrams: null, maxWeightGrams: null,
  minOrderValue: null, maxOrderValue: null, codOnly: null, carrierCode: 'GHTK',
}

const num = (v: string): number | null => (v.trim() === '' ? null : Number(v))

type RuleErrors = Partial<Record<'name' | 'priority' | 'weight' | 'value', string>>

/** The checks the form can do before sending; the server still has the last word. */
function validate(form: SaveCarrierRule, t: (key: string) => string): RuleErrors {
  const errors: RuleErrors = {}
  if (!form.name.trim()) errors.name = t('common.required')
  if (!Number.isInteger(form.priority)) errors.priority = t('settings.shipping.rules.priorityWhole')
  if (form.minWeightGrams != null && form.maxWeightGrams != null && form.minWeightGrams > form.maxWeightGrams) errors.weight = t('settings.shipping.rules.rangeOrder')
  if (form.minOrderValue != null && form.maxOrderValue != null && form.minOrderValue > form.maxOrderValue) errors.value = t('settings.shipping.rules.rangeOrder')
  return errors
}

function RuleForm({ rule, onClose }: { rule: CarrierRule | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [form, setForm] = useState<SaveCarrierRule>(rule ? { ...rule } : EMPTY)
  const [errors, setErrors] = useState<RuleErrors>({})
  const set = <K extends keyof SaveCarrierRule>(key: K, value: SaveCarrierRule[K]) => setForm((f) => ({ ...f, [key]: value }))

  const save = useMutation({
    mutationFn: () => (rule ? shippingApi.updateRule(rule.id, form) : shippingApi.createRule(form)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['shipping', 'rules'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  const submit = () => {
    const found = validate(form, t)
    setErrors(found)
    if (Object.keys(found).length === 0) save.mutate()
  }

  return (
    <Modal
      open
      size="md"
      title={rule ? t('settings.shipping.rules.edit') : t('settings.shipping.rules.add')}
      onClose={onClose}
      closeOnEsc={!save.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="carrier-rule-form" loading={save.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form
        id="carrier-rule-form"
        noValidate
        onSubmit={(e) => {
          e.preventDefault()
          submit()
        }}
        className="space-y-4"
      >
        <FormField label={t('settings.shipping.rules.name')} required error={errors.name}>
          <Input value={form.name} onChange={(e) => set('name', e.target.value)} />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('settings.shipping.rules.priority')} error={errors.priority}>
            <Input type="number" value={form.priority} onChange={(e) => set('priority', Number(e.target.value))} />
          </FormField>
          <FormField label={t('settings.shipping.rules.carrier')}>
            <Select value={form.carrierCode} onChange={(e) => set('carrierCode', e.target.value as CarrierCode)}>
              {CARRIERS.map((c) => <option key={c} value={c}>{c}</option>)}
            </Select>
          </FormField>
        </div>
        <FormField label={t('settings.shipping.rules.provinces')} hint={t('settings.shipping.rules.provincesHint')}>
          <Input placeholder="Hà Nội, Hải Phòng" value={form.matchProvinces ?? ''} onChange={(e) => set('matchProvinces', e.target.value)} />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('settings.shipping.rules.minWeight')} error={errors.weight}>
            <Input type="number" value={form.minWeightGrams ?? ''} onChange={(e) => set('minWeightGrams', num(e.target.value))} />
          </FormField>
          <FormField label={t('settings.shipping.rules.maxWeight')}>
            <Input type="number" value={form.maxWeightGrams ?? ''} onChange={(e) => set('maxWeightGrams', num(e.target.value))} />
          </FormField>
          <FormField label={t('settings.shipping.rules.minValue')} error={errors.value}>
            <Input type="number" value={form.minOrderValue ?? ''} onChange={(e) => set('minOrderValue', num(e.target.value))} />
          </FormField>
          <FormField label={t('settings.shipping.rules.maxValue')}>
            <Input type="number" value={form.maxOrderValue ?? ''} onChange={(e) => set('maxOrderValue', num(e.target.value))} />
          </FormField>
        </div>
        <FormField label={t('settings.shipping.rules.payment')}>
          <Select
            value={form.codOnly === null || form.codOnly === undefined ? '' : String(form.codOnly)}
            onChange={(e) => set('codOnly', e.target.value === '' ? null : e.target.value === 'true')}
          >
            <option value="">{t('settings.shipping.rules.paymentAny')}</option>
            <option value="true">{t('settings.shipping.rules.paymentCod')}</option>
            <option value="false">{t('settings.shipping.rules.paymentPrepaid')}</option>
          </Select>
        </FormField>
        <Checkbox label={t('settings.shipping.rules.active')} checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />
        {save.isError && <Alert variant="danger">{serverMessage(save.error, t('settings.shipping.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

/** "Try a parcel": which rule would pick the carrier and what every carrier asks. Books nothing. */
function QuoteTester() {
  const { t } = useTranslation()
  const [parcel, setParcel] = useState<QuoteParcel>({ province: '', district: '', weightGrams: 1000, declaredValue: 300000, codAmount: 0 })
  const [result, setResult] = useState<CarrierChoicePreview | null>(null)
  const run = useMutation({ mutationFn: () => shippingApi.quote(parcel), onSuccess: setResult })
  const fmt = (n: number) => new Intl.NumberFormat('vi-VN').format(n)

  return (
    <div className="card space-y-3 p-4">
      <h3 className="text-sm font-semibold text-ink">{t('settings.shipping.rules.tryTitle')}</h3>
      <div className="grid grid-cols-2 items-end gap-3 lg:grid-cols-6">
        <FormField label={t('settings.shipping.rules.province')}>
          <Input value={parcel.province} onChange={(e) => setParcel({ ...parcel, province: e.target.value })} />
        </FormField>
        <FormField label={t('settings.shipping.rules.district')}>
          <Input value={parcel.district} onChange={(e) => setParcel({ ...parcel, district: e.target.value })} />
        </FormField>
        <FormField label={t('settings.shipping.rules.weightG')}>
          <Input type="number" value={parcel.weightGrams} onChange={(e) => setParcel({ ...parcel, weightGrams: Number(e.target.value) })} />
        </FormField>
        <FormField label={t('settings.shipping.rules.value')}>
          <Input type="number" value={parcel.declaredValue} onChange={(e) => setParcel({ ...parcel, declaredValue: Number(e.target.value) })} />
        </FormField>
        <FormField label={t('settings.shipping.rules.cod')}>
          <Input type="number" value={parcel.codAmount} onChange={(e) => setParcel({ ...parcel, codAmount: Number(e.target.value) })} />
        </FormField>
        <Button variant="outline" loading={run.isPending} disabled={!parcel.province || !parcel.district} onClick={() => run.mutate()}>
          {t('settings.shipping.rules.try')}
        </Button>
      </div>
      {run.isError && <Alert variant="danger">{serverMessage(run.error, t('settings.shipping.failedToSave'))}</Alert>}
      <div aria-live="polite">
        {result && (
          <div className="space-y-1 text-sm">
            <p>
              {result.ruleCarrierCode
                ? t('settings.shipping.rules.ruleWins', { carrier: result.ruleCarrierCode, rule: result.ruleName })
                : t('settings.shipping.rules.noRule')}
            </p>
            <ul className="space-y-1 text-xs text-ink-secondary">
              {result.quotes.map((q) => (
                <li key={q.carrierCode}>
                  <span className="font-medium">{q.carrierName}</span>:{' '}
                  {q.success ? `${fmt(q.fee)} ₫` : <span className="text-warning-soft-text">{q.error}</span>}
                </li>
              ))}
              {result.quotes.length === 0 && <li>{t('settings.shipping.rules.noCarrier')}</li>}
            </ul>
          </div>
        )}
      </div>
    </div>
  )
}

/** The shop's selection rules: orders like this go with that carrier; whatever no rule matches goes to the cheapest quote. */
export function CarrierRulesCard({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [editing, setEditing] = useState<CarrierRule | null | 'new'>(null)
  const [deleting, setDeleting] = useState<CarrierRule | null>(null)

  const { data, isLoading } = useQuery({ queryKey: ['shipping', 'rules'], queryFn: () => shippingApi.listRules() })
  const remove = useMutation({
    mutationFn: (id: string) => shippingApi.deleteRule(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['shipping', 'rules'] })
      toast.success(t('common.saved'))
      setDeleting(null)
    },
    onError: (e) => {
      setDeleting(null)
      toast.error(serverMessage(e, t('settings.shipping.failedToSave')))
    },
  })

  const describe = (r: CarrierRule) =>
    [
      r.matchProvinces && `${t('settings.shipping.rules.provinces')}: ${r.matchProvinces}`,
      (r.minWeightGrams != null || r.maxWeightGrams != null) && `${t('settings.shipping.rules.weightG')}: ${r.minWeightGrams ?? 0}–${r.maxWeightGrams ?? '∞'}`,
      (r.minOrderValue != null || r.maxOrderValue != null) && `${t('settings.shipping.rules.value')}: ${r.minOrderValue ?? 0}–${r.maxOrderValue ?? '∞'}`,
      r.codOnly === true && t('settings.shipping.rules.paymentCod'),
      r.codOnly === false && t('settings.shipping.rules.paymentPrepaid'),
    ].filter(Boolean).join(' · ') || t('settings.shipping.rules.everything')

  if (isLoading) return <Spinner label={t('common.loading')} />

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-base font-semibold text-ink">{t('settings.shipping.rules.title')}</h2>
          <p className="text-xs text-ink-secondary">{t('settings.shipping.rules.hint')}</p>
        </div>
        {canManage && <Button onClick={() => setEditing('new')}>{t('settings.shipping.rules.add')}</Button>}
      </div>

      <div className="card divide-y divide-line">
        {(data ?? []).map((r) => (
          <div key={r.id} className="flex items-center justify-between gap-4 p-3 text-sm">
            <div>
              <div className="flex flex-wrap items-center gap-2 font-medium text-ink">
                <Badge>{r.priority}</Badge>
                <span>{r.name} → {r.carrierCode}</span>
                {!r.isActive && <Badge variant="warning">{t('settings.shipping.rules.inactive')}</Badge>}
              </div>
              <div className="text-xs text-ink-secondary">{describe(r)}</div>
            </div>
            {canManage && (
              <div className="flex gap-2">
                <Button variant="outline" size="sm" aria-label={`${t('common.edit')} ${r.name}`} onClick={() => setEditing(r)}>{t('common.edit')}</Button>
                <Button variant="outline" size="sm" aria-label={`${t('common.delete')} ${r.name}`} className="text-danger-soft-text" onClick={() => setDeleting(r)}>{t('common.delete')}</Button>
              </div>
            )}
          </div>
        ))}
        {(data ?? []).length === 0 && (
          <EmptyState title={t('settings.shipping.rules.none')} action={canManage ? <Button onClick={() => setEditing('new')}>{t('settings.shipping.rules.add')}</Button> : undefined} />
        )}
      </div>

      <QuoteTester />

      {editing && <RuleForm rule={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}

      <ConfirmDialog
        open={!!deleting}
        title={t('settings.shipping.rules.deleteTitle')}
        message={t('settings.shipping.rules.deleteConfirm', { name: deleting?.name })}
        confirmLabel={t('common.delete')}
        danger
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onCancel={() => setDeleting(null)}
      />
    </div>
  )
}
