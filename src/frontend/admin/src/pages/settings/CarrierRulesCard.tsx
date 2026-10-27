import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { shippingApi, type CarrierChoicePreview, type CarrierRule, type QuoteParcel, type SaveCarrierRule } from '@/api/shipping.api'
import type { CarrierCode } from '@/api/settings/shipping.settings.api'
import { serverMessage } from '@/api/purchasing/procurement.api'

const CARRIERS: CarrierCode[] = ['GHTK', 'VTP', 'GHN']

const EMPTY: SaveCarrierRule = {
  name: '', priority: 100, isActive: true, matchProvinces: '', minWeightGrams: null, maxWeightGrams: null,
  minOrderValue: null, maxOrderValue: null, codOnly: null, carrierCode: 'GHTK',
}

const num = (v: string): number | null => (v.trim() === '' ? null : Number(v))

function RuleForm({ rule, onClose }: { rule: CarrierRule | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<SaveCarrierRule>(rule ? { ...rule } : EMPTY)
  const set = <K extends keyof SaveCarrierRule>(key: K, value: SaveCarrierRule[K]) => setForm((f) => ({ ...f, [key]: value }))

  const save = useMutation({
    mutationFn: () => (rule ? shippingApi.updateRule(rule.id, form) : shippingApi.createRule(form)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['shipping', 'rules'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-lg space-y-3 overflow-y-auto p-6">
        <h3 className="text-base font-semibold text-gray-900">{rule ? t('settings.shipping.rules.edit') : t('settings.shipping.rules.add')}</h3>
        <label className="block text-sm text-gray-700">
          {t('settings.shipping.rules.name')}
          <input className="input mt-1 w-full" value={form.name} onChange={(e) => set('name', e.target.value)} />
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.rules.priority')}
            <input type="number" className="input mt-1 w-full" value={form.priority} onChange={(e) => set('priority', Number(e.target.value))} />
          </label>
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.rules.carrier')}
            <select className="input mt-1 w-full" value={form.carrierCode} onChange={(e) => set('carrierCode', e.target.value as CarrierCode)}>
              {CARRIERS.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </label>
        </div>
        <label className="block text-sm text-gray-700">
          {t('settings.shipping.rules.provinces')}
          <input className="input mt-1 w-full" placeholder="Hà Nội, Hải Phòng" value={form.matchProvinces ?? ''} onChange={(e) => set('matchProvinces', e.target.value)} />
          <span className="text-xs text-gray-400">{t('settings.shipping.rules.provincesHint')}</span>
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.rules.minWeight')}
            <input type="number" className="input mt-1 w-full" value={form.minWeightGrams ?? ''} onChange={(e) => set('minWeightGrams', num(e.target.value))} />
          </label>
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.rules.maxWeight')}
            <input type="number" className="input mt-1 w-full" value={form.maxWeightGrams ?? ''} onChange={(e) => set('maxWeightGrams', num(e.target.value))} />
          </label>
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.rules.minValue')}
            <input type="number" className="input mt-1 w-full" value={form.minOrderValue ?? ''} onChange={(e) => set('minOrderValue', num(e.target.value))} />
          </label>
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.rules.maxValue')}
            <input type="number" className="input mt-1 w-full" value={form.maxOrderValue ?? ''} onChange={(e) => set('maxOrderValue', num(e.target.value))} />
          </label>
        </div>
        <label className="block text-sm text-gray-700">
          {t('settings.shipping.rules.payment')}
          <select
            className="input mt-1 w-full"
            value={form.codOnly === null || form.codOnly === undefined ? '' : String(form.codOnly)}
            onChange={(e) => set('codOnly', e.target.value === '' ? null : e.target.value === 'true')}
          >
            <option value="">{t('settings.shipping.rules.paymentAny')}</option>
            <option value="true">{t('settings.shipping.rules.paymentCod')}</option>
            <option value="false">{t('settings.shipping.rules.paymentPrepaid')}</option>
          </select>
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />
          {t('settings.shipping.rules.active')}
        </label>
        {save.isError && <p className="text-xs text-red-600">{serverMessage(save.error, t('settings.shipping.failedToSave'))}</p>}
        <div className="flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={save.isPending || !form.name.trim()} onClick={() => save.mutate()}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </div>
    </div>
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
      <h3 className="text-sm font-semibold text-gray-900">{t('settings.shipping.rules.tryTitle')}</h3>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-6">
        <input className="input" placeholder={t('settings.shipping.rules.province')} value={parcel.province} onChange={(e) => setParcel({ ...parcel, province: e.target.value })} />
        <input className="input" placeholder={t('settings.shipping.rules.district')} value={parcel.district} onChange={(e) => setParcel({ ...parcel, district: e.target.value })} />
        <input type="number" className="input" title={t('settings.shipping.rules.weightG')} placeholder={t('settings.shipping.rules.weightG')} value={parcel.weightGrams} onChange={(e) => setParcel({ ...parcel, weightGrams: Number(e.target.value) })} />
        <input type="number" className="input" title={t('settings.shipping.rules.value')} placeholder={t('settings.shipping.rules.value')} value={parcel.declaredValue} onChange={(e) => setParcel({ ...parcel, declaredValue: Number(e.target.value) })} />
        <input type="number" className="input" title={t('settings.shipping.rules.cod')} placeholder={t('settings.shipping.rules.cod')} value={parcel.codAmount} onChange={(e) => setParcel({ ...parcel, codAmount: Number(e.target.value) })} />
        <button className="btn-outline" disabled={run.isPending || !parcel.province || !parcel.district} onClick={() => run.mutate()}>
          {t('settings.shipping.rules.try')}
        </button>
      </div>
      {run.isError && <p className="text-xs text-red-600">{serverMessage(run.error, t('settings.shipping.failedToSave'))}</p>}
      {result && (
        <div className="space-y-1 text-sm">
          <p>
            {result.ruleCarrierCode
              ? t('settings.shipping.rules.ruleWins', { carrier: result.ruleCarrierCode, rule: result.ruleName })
              : t('settings.shipping.rules.noRule')}
          </p>
          <ul className="space-y-1 text-xs text-gray-600">
            {result.quotes.map((q) => (
              <li key={q.carrierCode}>
                <span className="font-medium">{q.carrierName}</span>:{' '}
                {q.success ? `${fmt(q.fee)} ₫` : <span className="text-amber-700">{q.error}</span>}
              </li>
            ))}
            {result.quotes.length === 0 && <li>{t('settings.shipping.rules.noCarrier')}</li>}
          </ul>
        </div>
      )}
    </div>
  )
}

/** The shop's selection rules: orders like this go with that carrier; whatever no rule matches goes to the cheapest quote. */
export function CarrierRulesCard({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<CarrierRule | null | 'new'>(null)

  const { data, isLoading } = useQuery({ queryKey: ['shipping', 'rules'], queryFn: () => shippingApi.listRules() })
  const remove = useMutation({
    mutationFn: (id: string) => shippingApi.deleteRule(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['shipping', 'rules'] }),
  })

  const describe = (r: CarrierRule) =>
    [
      r.matchProvinces && `${t('settings.shipping.rules.provinces')}: ${r.matchProvinces}`,
      (r.minWeightGrams != null || r.maxWeightGrams != null) && `${t('settings.shipping.rules.weightG')}: ${r.minWeightGrams ?? 0}–${r.maxWeightGrams ?? '∞'}`,
      (r.minOrderValue != null || r.maxOrderValue != null) && `${t('settings.shipping.rules.value')}: ${r.minOrderValue ?? 0}–${r.maxOrderValue ?? '∞'}`,
      r.codOnly === true && t('settings.shipping.rules.paymentCod'),
      r.codOnly === false && t('settings.shipping.rules.paymentPrepaid'),
    ].filter(Boolean).join(' · ') || t('settings.shipping.rules.everything')

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-base font-semibold text-gray-900">{t('settings.shipping.rules.title')}</h2>
          <p className="text-xs text-gray-500">{t('settings.shipping.rules.hint')}</p>
        </div>
        {canManage && <button className="btn-primary" onClick={() => setEditing('new')}>+ {t('settings.shipping.rules.add')}</button>}
      </div>

      <div className="card divide-y divide-gray-100">
        {(data ?? []).map((r) => (
          <div key={r.id} className="flex items-center justify-between gap-4 p-3 text-sm">
            <div>
              <div className="font-medium text-gray-900">
                <span className="mr-2 rounded bg-gray-100 px-1.5 py-0.5 text-xs">{r.priority}</span>
                {r.name} → {r.carrierCode}
                {!r.isActive && <span className="ml-2 text-xs text-gray-400">({t('settings.shipping.rules.inactive')})</span>}
              </div>
              <div className="text-xs text-gray-500">{describe(r)}</div>
            </div>
            {canManage && (
              <div className="flex gap-2">
                <button className="btn-outline text-xs" onClick={() => setEditing(r)}>{t('common.edit')}</button>
                <button className="btn-outline text-xs text-red-600" onClick={() => remove.mutate(r.id)}>{t('common.delete')}</button>
              </div>
            )}
          </div>
        ))}
        {(data ?? []).length === 0 && <p className="p-4 text-sm text-gray-400">{t('settings.shipping.rules.none')}</p>}
      </div>

      <QuoteTester />

      {editing && <RuleForm rule={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </div>
  )
}
