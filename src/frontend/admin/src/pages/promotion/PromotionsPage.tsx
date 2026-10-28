import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { promotionApi, DISCOUNT_TYPES, PROMOTION_SCOPES } from '@/api/promotion/promotion.api'
import type { Promotion, PromotionInput } from '@/api/promotion/promotion.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

const KEY = ['promotion', 'promotions']
const money = (n: number) => `${new Intl.NumberFormat('vi-VN').format(n)} ₫`
const day = (iso: string) => new Date(iso).toLocaleDateString()
/** `datetime-local` wants local time without a zone. */
const toLocalInput = (iso?: string) => {
  if (!iso) return ''
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}
const ids = (text: string) => text.split(/[\s,;]+/).map((s) => s.trim()).filter(Boolean)
const errorText = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { message?: string; detail?: string } } })?.response?.data?.detail ??
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback

interface FormValues {
  name: string
  description: string
  discountType: string
  scope: string
  discountValue: number
  maxDiscountAmount: string
  minOrderAmount: number
  maxUsageTotal: string
  maxUsagePerUser: string
  buyQuantity: string
  getQuantity: string
  startDate: string
  endDate: string
  productIds: string
  categoryIds: string
}

const optionalNumber = (v: string) => (v === '' || v == null ? null : Number(v))

function PromotionModal({ promotion, onClose }: { promotion: Promotion | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!promotion
  const { register, handleSubmit, watch, formState: { errors } } = useForm<FormValues>({
    defaultValues: {
      name: promotion?.name ?? '',
      description: promotion?.description ?? '',
      discountType: String(promotion ? DISCOUNT_TYPES[promotion.discountType] : DISCOUNT_TYPES.Percentage),
      scope: String(promotion ? PROMOTION_SCOPES[promotion.scope] : PROMOTION_SCOPES.Order),
      discountValue: promotion?.discountValue ?? 0,
      maxDiscountAmount: promotion?.maxDiscountAmount?.toString() ?? '',
      minOrderAmount: promotion?.minOrderAmount ?? 0,
      maxUsageTotal: promotion?.maxUsageTotal?.toString() ?? '',
      maxUsagePerUser: promotion?.maxUsagePerUser?.toString() ?? '',
      buyQuantity: promotion?.buyQuantity?.toString() ?? '',
      getQuantity: promotion?.getQuantity?.toString() ?? '',
      startDate: toLocalInput(promotion?.startDate),
      endDate: toLocalInput(promotion?.endDate),
      productIds: promotion?.applicableProductIds.join(', ') ?? '',
      categoryIds: promotion?.applicableCategoryIds.join(', ') ?? '',
    },
  })
  const discountType = Number(watch('discountType'))
  const scope = Number(watch('scope'))

  const mutation = useMutation({
    mutationFn: (v: FormValues) => {
      const body: PromotionInput = {
        name: v.name.trim(),
        description: v.description.trim() || null,
        discountType: Number(v.discountType),
        scope: Number(v.scope),
        discountValue: Number(v.discountValue),
        maxDiscountAmount: optionalNumber(v.maxDiscountAmount),
        minOrderAmount: Number(v.minOrderAmount),
        maxUsageTotal: optionalNumber(v.maxUsageTotal),
        maxUsagePerUser: optionalNumber(v.maxUsagePerUser),
        buyQuantity: Number(v.discountType) === DISCOUNT_TYPES.BuyXGetY ? optionalNumber(v.buyQuantity) : null,
        getQuantity: Number(v.discountType) === DISCOUNT_TYPES.BuyXGetY ? optionalNumber(v.getQuantity) : null,
        startDate: new Date(v.startDate).toISOString(),
        endDate: new Date(v.endDate).toISOString(),
        applicableProductIds: Number(v.scope) === PROMOTION_SCOPES.Product ? ids(v.productIds) : [],
        applicableCategoryIds: Number(v.scope) === PROMOTION_SCOPES.Category ? ids(v.categoryIds) : [],
      }
      return isEdit ? promotionApi.update(promotion!.id, body) : promotionApi.create(body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      onClose()
    },
  })

  const label = 'mb-1 block text-sm font-medium text-gray-700'
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 p-4">
      <div className="card w-full max-w-2xl p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('promotion.promotions.editTitle') : t('promotion.promotions.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="col-span-2">
              <label className={label}>{t('common.name')}</label>
              <input className="input" {...register('name', { required: true })} />
              {errors.name && <p className="mt-1 text-xs text-red-600">{t('promotion.required')}</p>}
            </div>
            <div className="col-span-2">
              <label className={label}>{t('promotion.promotions.description')}</label>
              <input className="input" {...register('description')} />
            </div>
            <div>
              <label className={label}>{t('promotion.promotions.discountType')}</label>
              <select className="input" {...register('discountType')}>
                {Object.entries(DISCOUNT_TYPES).map(([name, value]) => (
                  <option key={name} value={value}>{t(`promotion.discountType.${name}`)}</option>
                ))}
              </select>
            </div>
            <div>
              <label className={label}>{t('promotion.promotions.scope')}</label>
              <select className="input" {...register('scope')}>
                {Object.entries(PROMOTION_SCOPES).map(([name, value]) => (
                  <option key={name} value={value}>{t(`promotion.scope.${name}`)}</option>
                ))}
              </select>
            </div>
            {discountType !== DISCOUNT_TYPES.FreeShipping && discountType !== DISCOUNT_TYPES.BuyXGetY && (
              <div>
                <label className={label}>
                  {discountType === DISCOUNT_TYPES.Percentage ? t('promotion.promotions.percent') : t('promotion.promotions.amount')}
                </label>
                <input type="number" step="any" min={0} className="input" {...register('discountValue', { required: true, min: 0 })} />
              </div>
            )}
            {discountType === DISCOUNT_TYPES.BuyXGetY && (
              <>
                <div>
                  <label className={label}>{t('promotion.promotions.buyQuantity')}</label>
                  <input type="number" min={1} className="input" {...register('buyQuantity', { required: true })} />
                </div>
                <div>
                  <label className={label}>{t('promotion.promotions.getQuantity')}</label>
                  <input type="number" min={1} className="input" {...register('getQuantity', { required: true })} />
                </div>
              </>
            )}
            <div>
              <label className={label}>{t('promotion.promotions.maxDiscount')}</label>
              <input type="number" min={0} className="input" {...register('maxDiscountAmount')} />
            </div>
            <div>
              <label className={label}>{t('promotion.promotions.minOrder')}</label>
              <input type="number" min={0} className="input" {...register('minOrderAmount')} />
            </div>
            <div>
              <label className={label}>{t('promotion.promotions.maxUsageTotal')}</label>
              <input type="number" min={1} className="input" {...register('maxUsageTotal')} />
            </div>
            <div>
              <label className={label}>{t('promotion.promotions.maxUsagePerUser')}</label>
              <input type="number" min={1} className="input" {...register('maxUsagePerUser')} />
            </div>
            <div>
              <label className={label}>{t('promotion.promotions.startDate')}</label>
              <input type="datetime-local" className="input" {...register('startDate', { required: true })} />
            </div>
            <div>
              <label className={label}>{t('promotion.promotions.endDate')}</label>
              <input type="datetime-local" className="input" {...register('endDate', { required: true })} />
            </div>
            {scope === PROMOTION_SCOPES.Product && (
              <div className="col-span-2">
                <label className={label}>{t('promotion.promotions.productIds')}</label>
                <textarea className="input" rows={2} {...register('productIds')} />
              </div>
            )}
            {scope === PROMOTION_SCOPES.Category && (
              <div className="col-span-2">
                <label className={label}>{t('promotion.promotions.categoryIds')}</label>
                <textarea className="input" rows={2} {...register('categoryIds')} />
              </div>
            )}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{errorText(mutation.error, t('promotion.promotions.failedToSave'))}</p>}
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

function CouponsDialog({ promotion, canManage, onClose }: { promotion: Promotion; canManage: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [count, setCount] = useState(10)
  const [prefix, setPrefix] = useState('')
  const key = ['promotion', 'coupons', promotion.id]
  const { data = [], isLoading } = useQuery({ queryKey: key, queryFn: ({ signal }) => promotionApi.coupons(promotion.id, signal) })
  const generate = useMutation({
    mutationFn: () => promotionApi.generateCoupons(promotion.id, { count, prefix: prefix.trim() }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
  })
  const columns = [
    { key: 'code', label: t('promotion.coupons.code') },
    { key: 'isUsed', label: t('common.status'), render: (c: (typeof data)[number]) => (
      <StatusBadge status={c.isUsed ? 'Cancelled' : 'Active'} label={c.isUsed ? t('promotion.coupons.used') : t('promotion.coupons.available')} />
    ) },
    { key: 'usedAt', label: t('promotion.coupons.usedAt'), render: (c: (typeof data)[number]) => (c.usedAt ? new Date(c.usedAt).toLocaleString() : '') },
  ]
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 p-4">
      <div className="card w-full max-w-2xl space-y-4 p-6">
        <h3 className="text-base font-semibold text-gray-900">{t('promotion.coupons.title', { name: promotion.name })}</h3>
        {canManage && promotion.status !== 'Cancelled' && (
          <div className="flex items-end gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('promotion.coupons.count')}</label>
              <input type="number" min={1} max={10000} className="input w-28" value={count} onChange={(e) => setCount(Number(e.target.value))} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('promotion.coupons.prefix')}</label>
              <input className="input w-36" value={prefix} onChange={(e) => setPrefix(e.target.value)} />
            </div>
            <button className="btn-primary" disabled={generate.isPending || count < 1} onClick={() => generate.mutate()}>
              {t('promotion.coupons.generate')}
            </button>
          </div>
        )}
        {generate.isError && <p className="text-xs text-red-600">{errorText(generate.error, t('promotion.coupons.failed'))}</p>}
        <div className="max-h-80 overflow-y-auto">
          <DataTable columns={columns} data={data} isLoading={isLoading} />
        </div>
        <div className="flex justify-end">
          <button className="btn-outline" onClick={onClose}>{t('common.close')}</button>
        </div>
      </div>
    </div>
  )
}

type Transition = { promotion: Promotion; action: 'activate' | 'pause' | 'cancel' }

export function PromotionsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [modal, setModal] = useState<{ open: boolean; promotion: Promotion | null }>({ open: false, promotion: null })
  const [couponsOf, setCouponsOf] = useState<Promotion | null>(null)
  const [transition, setTransition] = useState<Transition | null>(null)

  const canManage = hasPermission('promotions:manage')
  const { data, isLoading } = useQuery({ queryKey: KEY, queryFn: ({ signal }) => promotionApi.list(signal), staleTime: 30_000, enabled: canManage })

  const move = useMutation({
    mutationFn: ({ promotion, action }: Transition) => promotionApi[action](promotion.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      setTransition(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'name', label: t('common.name') },
      { key: 'discountType', label: t('promotion.promotions.discountType'), render: (p: Promotion) => t(`promotion.discountType.${p.discountType}`) },
      {
        key: 'discountValue',
        label: t('promotion.promotions.value'),
        render: (p: Promotion) =>
          p.discountType === 'Percentage' ? `${p.discountValue}%`
            : p.discountType === 'FixedAmount' ? money(p.discountValue)
              : p.discountType === 'BuyXGetY' ? `${p.buyQuantity}+${p.getQuantity}` : '—',
      },
      { key: 'period', label: t('promotion.promotions.period'), render: (p: Promotion) => `${day(p.startDate)} – ${day(p.endDate)}` },
      { key: 'usage', label: t('promotion.promotions.usage'), render: (p: Promotion) => `${p.currentUsage}${p.maxUsageTotal ? ` / ${p.maxUsageTotal}` : ''}` },
      { key: 'status', label: t('common.status'), render: (p: Promotion) => <StatusBadge status={p.status} label={t(`promotion.status.${p.status}`)} /> },
      {
        key: 'actions',
        label: '',
        render: (p: Promotion) => (
          <div className="flex flex-wrap gap-3">
            <button className="text-xs text-primary-600 hover:underline" onClick={() => setCouponsOf(p)}>{t('promotion.coupons.open')}</button>
            {p.status === 'Draft' && (
              <button className="text-xs text-primary-600 hover:underline" onClick={() => setModal({ open: true, promotion: p })}>{t('common.edit')}</button>
            )}
            {(p.status === 'Draft' || p.status === 'Paused') && (
              <button className="text-xs text-green-700 hover:underline" onClick={() => setTransition({ promotion: p, action: 'activate' })}>{t('common.activate')}</button>
            )}
            {p.status === 'Active' && (
              <button className="text-xs text-yellow-700 hover:underline" onClick={() => setTransition({ promotion: p, action: 'pause' })}>{t('promotion.promotions.pause')}</button>
            )}
            {p.status !== 'Cancelled' && p.status !== 'Expired' && (
              <button className="text-xs text-red-600 hover:underline" onClick={() => setTransition({ promotion: p, action: 'cancel' })}>{t('common.cancel')}</button>
            )}
          </div>
        ),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('promotion.promotions.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModal({ open: true, promotion: null })}>{t('promotion.promotions.add')}</button>
        )}
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modal.open && <PromotionModal promotion={modal.promotion} onClose={() => setModal({ open: false, promotion: null })} />}
      {couponsOf && <CouponsDialog promotion={couponsOf} canManage={canManage} onClose={() => setCouponsOf(null)} />}

      <ConfirmDialog
        open={!!transition}
        title={transition ? t(`promotion.promotions.${transition.action}Title`) : ''}
        message={transition ? t(`promotion.promotions.${transition.action}Confirm`, { name: transition.promotion.name }) : ''}
        confirmLabel={transition ? t(`promotion.promotions.${transition.action}Label`) : undefined}
        danger={transition?.action === 'cancel'}
        onConfirm={() => transition && move.mutate(transition)}
        onCancel={() => setTransition(null)}
      />
    </div>
  )
}
