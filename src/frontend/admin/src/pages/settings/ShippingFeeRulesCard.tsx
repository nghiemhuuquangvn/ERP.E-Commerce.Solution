import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { shippingApi, type FeeQuote, type SaveShippingFeeRule, type ShippingFeeRule } from '@/api/shipping.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Alert, Badge, Button, Checkbox, EmptyState, FormField, Input, Modal, Spinner, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const EMPTY: SaveShippingFeeRule = { name: '', priority: 100, isActive: true, matchProvinces: '', minWeightGrams: null, maxWeightGrams: null, fee: 0 }

const num = (v: string): number | null => (v.trim() === '' ? null : Number(v))
const fmt = (n: number) => new Intl.NumberFormat('vi-VN').format(n)

type RuleErrors = Partial<Record<'name' | 'priority' | 'fee' | 'weight', string>>

/** The checks the form can do before sending; the server still has the last word. */
function validate(form: SaveShippingFeeRule, t: (key: string) => string): RuleErrors {
  const errors: RuleErrors = {}
  if (!form.name.trim()) errors.name = t('common.required')
  if (!Number.isInteger(form.priority)) errors.priority = t('settings.shipping.fees.priorityWhole')
  if (!(form.fee >= 0)) errors.fee = t('settings.shipping.fees.feeMin')
  if (form.minWeightGrams != null && form.maxWeightGrams != null && form.minWeightGrams > form.maxWeightGrams) errors.weight = t('settings.shipping.fees.rangeOrder')
  return errors
}

function RuleForm({ rule, onClose }: { rule: ShippingFeeRule | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [form, setForm] = useState<SaveShippingFeeRule>(rule ? { ...rule } : EMPTY)
  const [errors, setErrors] = useState<RuleErrors>({})
  const set = <K extends keyof SaveShippingFeeRule>(key: K, value: SaveShippingFeeRule[K]) => setForm((f) => ({ ...f, [key]: value }))

  const save = useMutation({
    mutationFn: () => (rule ? shippingApi.updateFeeRule(rule.id, form) : shippingApi.createFeeRule(form)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['shipping', 'fee-rules'] })
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
      title={rule ? t('settings.shipping.fees.edit') : t('settings.shipping.fees.add')}
      onClose={onClose}
      closeOnEsc={!save.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="fee-rule-form" loading={save.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form
        id="fee-rule-form"
        noValidate
        onSubmit={(e) => {
          e.preventDefault()
          submit()
        }}
        className="space-y-4"
      >
        <FormField label={t('settings.shipping.fees.name')} required error={errors.name}>
          <Input value={form.name} onChange={(e) => set('name', e.target.value)} />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('settings.shipping.fees.priority')} error={errors.priority}>
            <Input type="number" value={form.priority} onChange={(e) => set('priority', Number(e.target.value))} />
          </FormField>
          <FormField label={t('settings.shipping.fees.fee')} error={errors.fee}>
            <Input type="number" min={0} value={form.fee} onChange={(e) => set('fee', Number(e.target.value))} />
          </FormField>
        </div>
        <FormField label={t('settings.shipping.fees.provinces')} hint={t('settings.shipping.fees.provincesHint')}>
          <Input placeholder="Hồ Chí Minh, Bình Dương" value={form.matchProvinces ?? ''} onChange={(e) => set('matchProvinces', e.target.value)} />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('settings.shipping.fees.minWeight')} error={errors.weight}>
            <Input type="number" value={form.minWeightGrams ?? ''} onChange={(e) => set('minWeightGrams', num(e.target.value))} />
          </FormField>
          <FormField label={t('settings.shipping.fees.maxWeight')}>
            <Input type="number" value={form.maxWeightGrams ?? ''} onChange={(e) => set('maxWeightGrams', num(e.target.value))} />
          </FormField>
        </div>
        <Checkbox label={t('settings.shipping.fees.active')} checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />
        {save.isError && <Alert variant="danger">{serverMessage(save.error, t('settings.shipping.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
      <h3 className="text-sm font-semibold text-ink">{t('settings.shipping.fees.tryTitle')}</h3>
      <div className="grid grid-cols-2 items-end gap-3 lg:grid-cols-4">
        <FormField label={t('settings.shipping.fees.province')}>
          <Input value={province} onChange={(e) => setProvince(e.target.value)} />
        </FormField>
        <FormField label={t('settings.shipping.fees.weightG')}>
          <Input type="number" value={weight} onChange={(e) => setWeight(Number(e.target.value))} />
        </FormField>
        <FormField label={t('settings.shipping.fees.goodsValue')}>
          <Input type="number" value={value} onChange={(e) => setValue(Number(e.target.value))} />
        </FormField>
        <Button variant="outline" loading={run.isPending} disabled={!province.trim()} onClick={() => run.mutate()}>
          {t('settings.shipping.fees.try')}
        </Button>
      </div>
      {run.isError && <Alert variant="danger">{serverMessage(run.error, t('settings.shipping.failedToSave'))}</Alert>}
      <div aria-live="polite">
        {result && (
          <p className="text-sm">
            {result.fee === 0 && result.isFree
              ? t('settings.shipping.fees.resultFree', { threshold: fmt(result.freeShippingThreshold) })
              : t('settings.shipping.fees.result', { fee: fmt(result.fee), row: result.ruleName ?? t('settings.shipping.fees.flatFee') })}
          </p>
        )}
      </div>
    </div>
  )
}

/** The shop's delivery price list: parcels to these provinces in this weight band cost this much; the flat fee on the Tenant tab covers the rest. */
export function ShippingFeeRulesCard({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [editing, setEditing] = useState<ShippingFeeRule | null | 'new'>(null)
  const [deleting, setDeleting] = useState<ShippingFeeRule | null>(null)

  const { data, isLoading } = useQuery({ queryKey: ['shipping', 'fee-rules'], queryFn: () => shippingApi.listFeeRules() })
  const remove = useMutation({
    mutationFn: (id: string) => shippingApi.deleteFeeRule(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['shipping', 'fee-rules'] })
      toast.success(t('common.saved'))
      setDeleting(null)
    },
    onError: (e) => {
      setDeleting(null)
      toast.error(serverMessage(e, t('settings.shipping.failedToSave')))
    },
  })

  const describe = (r: ShippingFeeRule) =>
    [
      r.matchProvinces ? `${t('settings.shipping.fees.provinces')}: ${r.matchProvinces}` : t('settings.shipping.fees.allProvinces'),
      (r.minWeightGrams != null || r.maxWeightGrams != null) && `${t('settings.shipping.fees.weightG')}: ${r.minWeightGrams ?? 0}–${r.maxWeightGrams ?? '∞'}`,
    ].filter(Boolean).join(' · ')

  if (isLoading) return <Spinner label={t('common.loading')} />

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-base font-semibold text-ink">{t('settings.shipping.fees.title')}</h2>
          <p className="text-xs text-ink-secondary">{t('settings.shipping.fees.hint')}</p>
        </div>
        {canManage && <Button onClick={() => setEditing('new')}>{t('settings.shipping.fees.add')}</Button>}
      </div>

      <div className="card divide-y divide-line">
        {(data ?? []).map((r) => (
          <div key={r.id} className="flex items-center justify-between gap-4 p-3 text-sm">
            <div>
              <div className="flex flex-wrap items-center gap-2 font-medium text-ink">
                <Badge>{r.priority}</Badge>
                <span>{r.name} → {fmt(r.fee)} ₫</span>
                {!r.isActive && <Badge variant="warning">{t('settings.shipping.fees.inactive')}</Badge>}
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
          <EmptyState title={t('settings.shipping.fees.none')} action={canManage ? <Button onClick={() => setEditing('new')}>{t('settings.shipping.fees.add')}</Button> : undefined} />
        )}
      </div>

      <FeeTester />

      {editing && <RuleForm rule={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}

      <ConfirmDialog
        open={!!deleting}
        title={t('settings.shipping.fees.deleteTitle')}
        message={t('settings.shipping.fees.deleteConfirm', { name: deleting?.name })}
        confirmLabel={t('common.delete')}
        danger
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onCancel={() => setDeleting(null)}
      />
    </div>
  )
}
