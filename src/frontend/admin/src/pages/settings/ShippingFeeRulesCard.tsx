import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { shippingApi, type FeeQuote, type SaveShippingFeeRule, type ShippingFeeRule } from '@/api/shipping.api'
import { serverMessage } from '@/api/purchasing/procurement.api'

const EMPTY: SaveShippingFeeRule = { name: '', priority: 100, isActive: true, matchProvinces: '', minWeightGrams: null, maxWeightGrams: null, fee: 0 }

const num = (v: string): number | null => (v.trim() === '' ? null : Number(v))
const fmt = (n: number) => new Intl.NumberFormat('vi-VN').format(n)

function RuleForm({ rule, onClose }: { rule: ShippingFeeRule | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<SaveShippingFeeRule>(rule ? { ...rule } : EMPTY)
  const set = <K extends keyof SaveShippingFeeRule>(key: K, value: SaveShippingFeeRule[K]) => setForm((f) => ({ ...f, [key]: value }))

  const save = useMutation({
    mutationFn: () => (rule ? shippingApi.updateFeeRule(rule.id, form) : shippingApi.createFeeRule(form)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['shipping', 'fee-rules'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-lg space-y-3 overflow-y-auto p-6">
        <h3 className="text-base font-semibold text-gray-900">{rule ? t('settings.shipping.fees.edit') : t('settings.shipping.fees.add')}</h3>
        <label className="block text-sm text-gray-700">
          {t('settings.shipping.fees.name')}
          <input className="input mt-1 w-full" value={form.name} onChange={(e) => set('name', e.target.value)} />
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.fees.priority')}
            <input type="number" className="input mt-1 w-full" value={form.priority} onChange={(e) => set('priority', Number(e.target.value))} />
          </label>
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.fees.fee')}
            <input type="number" min={0} className="input mt-1 w-full" value={form.fee} onChange={(e) => set('fee', Number(e.target.value))} />
          </label>
        </div>
        <label className="block text-sm text-gray-700">
          {t('settings.shipping.fees.provinces')}
          <input className="input mt-1 w-full" placeholder="Hồ Chí Minh, Bình Dương" value={form.matchProvinces ?? ''} onChange={(e) => set('matchProvinces', e.target.value)} />
          <span className="text-xs text-gray-400">{t('settings.shipping.fees.provincesHint')}</span>
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.fees.minWeight')}
            <input type="number" className="input mt-1 w-full" value={form.minWeightGrams ?? ''} onChange={(e) => set('minWeightGrams', num(e.target.value))} />
          </label>
          <label className="block text-sm text-gray-700">
            {t('settings.shipping.fees.maxWeight')}
            <input type="number" className="input mt-1 w-full" value={form.maxWeightGrams ?? ''} onChange={(e) => set('maxWeightGrams', num(e.target.value))} />
          </label>
        </div>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />
          {t('settings.shipping.fees.active')}
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

/** "Try a parcel": what the customer would pay for these goods going to this province, and which row set it. Charges nothing. */
function FeeTester() {
  const { t } = useTranslation()
  const [province, setProvince] = useState('')
  const [weight, setWeight] = useState(1000)
  const [value, setValue] = useState(300000)
  const [result, setResult] = useState<FeeQuote | null>(null)
  const run = useMutation({
    mutationFn: () => shippingApi.previewFee({ merchandiseValue: value, province, weightGrams: weight, items: [] }),
    onSuccess: setResult,
  })

  return (
    <div className="card space-y-3 p-4">
      <h3 className="text-sm font-semibold text-gray-900">{t('settings.shipping.fees.tryTitle')}</h3>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <input className="input" placeholder={t('settings.shipping.fees.province')} value={province} onChange={(e) => setProvince(e.target.value)} />
        <input type="number" className="input" title={t('settings.shipping.fees.weightG')} placeholder={t('settings.shipping.fees.weightG')} value={weight} onChange={(e) => setWeight(Number(e.target.value))} />
        <input type="number" className="input" title={t('settings.shipping.fees.goodsValue')} placeholder={t('settings.shipping.fees.goodsValue')} value={value} onChange={(e) => setValue(Number(e.target.value))} />
        <button className="btn-outline" disabled={run.isPending || !province.trim()} onClick={() => run.mutate()}>
          {t('settings.shipping.fees.try')}
        </button>
      </div>
      {run.isError && <p className="text-xs text-red-600">{serverMessage(run.error, t('settings.shipping.failedToSave'))}</p>}
      {result && (
        <p className="text-sm">
          {result.fee === 0 && result.isFree
            ? t('settings.shipping.fees.resultFree', { threshold: fmt(result.freeShippingThreshold) })
            : t('settings.shipping.fees.result', { fee: fmt(result.fee), row: result.ruleName ?? t('settings.shipping.fees.flatFee') })}
        </p>
      )}
    </div>
  )
}

/** The shop's delivery price list: parcels to these provinces in this weight band cost this much; the flat fee on the Tenant tab covers the rest. */
export function ShippingFeeRulesCard({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<ShippingFeeRule | null | 'new'>(null)

  const { data, isLoading } = useQuery({ queryKey: ['shipping', 'fee-rules'], queryFn: () => shippingApi.listFeeRules() })
  const remove = useMutation({
    mutationFn: (id: string) => shippingApi.deleteFeeRule(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['shipping', 'fee-rules'] }),
  })

  const describe = (r: ShippingFeeRule) =>
    [
      r.matchProvinces ? `${t('settings.shipping.fees.provinces')}: ${r.matchProvinces}` : t('settings.shipping.fees.allProvinces'),
      (r.minWeightGrams != null || r.maxWeightGrams != null) && `${t('settings.shipping.fees.weightG')}: ${r.minWeightGrams ?? 0}–${r.maxWeightGrams ?? '∞'}`,
    ].filter(Boolean).join(' · ')

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-base font-semibold text-gray-900">{t('settings.shipping.fees.title')}</h2>
          <p className="text-xs text-gray-500">{t('settings.shipping.fees.hint')}</p>
        </div>
        {canManage && <button className="btn-primary" onClick={() => setEditing('new')}>+ {t('settings.shipping.fees.add')}</button>}
      </div>

      <div className="card divide-y divide-gray-100">
        {(data ?? []).map((r) => (
          <div key={r.id} className="flex items-center justify-between gap-4 p-3 text-sm">
            <div>
              <div className="font-medium text-gray-900">
                <span className="mr-2 rounded bg-gray-100 px-1.5 py-0.5 text-xs">{r.priority}</span>
                {r.name} → {fmt(r.fee)} ₫
                {!r.isActive && <span className="ml-2 text-xs text-gray-400">({t('settings.shipping.fees.inactive')})</span>}
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
        {(data ?? []).length === 0 && <p className="p-4 text-sm text-gray-400">{t('settings.shipping.fees.none')}</p>}
      </div>

      <FeeTester />

      {editing && <RuleForm rule={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </div>
  )
}
