import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { promotionApi, DISCOUNT_TYPES, PROMOTION_SCOPES } from '@/api/promotion/promotion.api'
import type { Promotion, PromotionInput } from '@/api/promotion/promotion.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  const required = (key: keyof FormValues) => (errors[key] ? t('promotion.required') : undefined)
  return (
    <Modal
      open
      size="lg"
      title={isEdit ? t('promotion.promotions.editTitle') : t('promotion.promotions.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="promotion-form" loading={mutation.isPending}>
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id="promotion-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <FormField className="col-span-2" label={t('common.name')} required error={required('name')}>
            <Input {...register('name', { required: true })} />
          </FormField>
          <FormField className="col-span-2" label={t('promotion.promotions.description')}>
            <Input {...register('description')} />
          </FormField>
          <FormField label={t('promotion.promotions.discountType')}>
            <Select {...register('discountType')}>
              {Object.entries(DISCOUNT_TYPES).map(([name, value]) => (
                <option key={name} value={value}>{t(`promotion.discountType.${name}`)}</option>
              ))}
            </Select>
          </FormField>
          <FormField label={t('promotion.promotions.scope')}>
            <Select {...register('scope')}>
              {Object.entries(PROMOTION_SCOPES).map(([name, value]) => (
                <option key={name} value={value}>{t(`promotion.scope.${name}`)}</option>
              ))}
            </Select>
          </FormField>
          {discountType !== DISCOUNT_TYPES.FreeShipping && discountType !== DISCOUNT_TYPES.BuyXGetY && (
            <FormField
              label={discountType === DISCOUNT_TYPES.Percentage ? t('promotion.promotions.percent') : t('promotion.promotions.amount')}
              required
              error={required('discountValue')}
            >
              <Input type="number" step="any" min={0} {...register('discountValue', { required: true, min: 0 })} />
            </FormField>
          )}
          {discountType === DISCOUNT_TYPES.BuyXGetY && (
            <>
              <FormField label={t('promotion.promotions.buyQuantity')} required error={required('buyQuantity')}>
                <Input type="number" min={1} {...register('buyQuantity', { required: true })} />
              </FormField>
              <FormField label={t('promotion.promotions.getQuantity')} required error={required('getQuantity')}>
                <Input type="number" min={1} {...register('getQuantity', { required: true })} />
              </FormField>
            </>
          )}
          <FormField label={t('promotion.promotions.maxDiscount')}>
            <Input type="number" min={0} {...register('maxDiscountAmount')} />
          </FormField>
          <FormField label={t('promotion.promotions.minOrder')}>
            <Input type="number" min={0} {...register('minOrderAmount')} />
          </FormField>
          <FormField label={t('promotion.promotions.maxUsageTotal')}>
            <Input type="number" min={1} {...register('maxUsageTotal')} />
          </FormField>
          <FormField label={t('promotion.promotions.maxUsagePerUser')}>
            <Input type="number" min={1} {...register('maxUsagePerUser')} />
          </FormField>
          <FormField label={t('promotion.promotions.startDate')} required error={required('startDate')}>
            <Input type="datetime-local" {...register('startDate', { required: true })} />
          </FormField>
          <FormField label={t('promotion.promotions.endDate')} required error={required('endDate')}>
            <Input type="datetime-local" {...register('endDate', { required: true })} />
          </FormField>
          {scope === PROMOTION_SCOPES.Product && (
            <FormField className="col-span-2" label={t('promotion.promotions.productIds')}>
              <Textarea rows={2} {...register('productIds')} />
            </FormField>
          )}
          {scope === PROMOTION_SCOPES.Category && (
            <FormField className="col-span-2" label={t('promotion.promotions.categoryIds')}>
              <Textarea rows={2} {...register('categoryIds')} />
            </FormField>
          )}
        </div>
        {mutation.isError && <Alert variant="danger">{errorText(mutation.error, t('promotion.promotions.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

function CouponsDialog({ promotion, canManage, onClose }: { promotion: Promotion; canManage: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [count, setCount] = useState(10)
  const [prefix, setPrefix] = useState('')
  const key = ['promotion', 'coupons', promotion.id]
  const { data = [], isLoading } = useQuery({ queryKey: key, queryFn: ({ signal }) => promotionApi.coupons(promotion.id, signal) })
  const generate = useMutation({
    mutationFn: () => promotionApi.generateCoupons(promotion.id, { count, prefix: prefix.trim() }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: key })
      toast.success(t('promotion.coupons.generated'))
    },
  })
  const columns = [
    { key: 'code', label: t('promotion.coupons.code') },
    { key: 'isUsed', label: t('common.status'), render: (c: (typeof data)[number]) => (
      <StatusBadge status={c.isUsed ? 'Cancelled' : 'Active'} label={c.isUsed ? t('promotion.coupons.used') : t('promotion.coupons.available')} />
    ) },
    { key: 'usedAt', label: t('promotion.coupons.usedAt'), render: (c: (typeof data)[number]) => (c.usedAt ? new Date(c.usedAt).toLocaleString() : '') },
  ]
  return (
    <Modal
      open
      size="lg"
      title={t('promotion.coupons.title', { name: promotion.name })}
      onClose={onClose}
      dismissOnBackdrop
      footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}
    >
      <div className="space-y-4">
        {canManage && promotion.status !== 'Cancelled' && (
          <div className="flex items-end gap-3">
            <FormField label={t('promotion.coupons.count')}>
              <Input type="number" min={1} max={10000} className="w-28" value={count} onChange={(e) => setCount(Number(e.target.value))} />
            </FormField>
            <FormField label={t('promotion.coupons.prefix')}>
              <Input className="w-36" value={prefix} onChange={(e) => setPrefix(e.target.value)} />
            </FormField>
            <Button loading={generate.isPending} disabled={count < 1} onClick={() => generate.mutate()}>
              {t('promotion.coupons.generate')}
            </Button>
          </div>
        )}
        {generate.isError && <Alert variant="danger">{errorText(generate.error, t('promotion.coupons.failed'))}</Alert>}
        <div className="max-h-80 overflow-y-auto">
          <DataTable columns={columns} data={data} isLoading={isLoading} emptyState={<EmptyState title={t('promotion.coupons.empty')} />} />
        </div>
      </div>
    </Modal>
  )
}

type Transition = { promotion: Promotion; action: 'activate' | 'pause' | 'cancel' }

export function PromotionsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [modal, setModal] = useState<{ open: boolean; promotion: Promotion | null }>({ open: false, promotion: null })
  const [couponsOf, setCouponsOf] = useState<Promotion | null>(null)
  const [transition, setTransition] = useState<Transition | null>(null)

  const canManage = hasPermission('promotions:manage')
  const { data, isLoading } = useQuery({ queryKey: KEY, queryFn: ({ signal }) => promotionApi.list(signal), staleTime: 30_000, enabled: canManage })

  const move = useMutation({
    mutationFn: ({ promotion, action }: Transition) => promotionApi[action](promotion.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      toast.success(t('common.saved'))
      setTransition(null)
    },
    onError: (e) => {
      toast.error(errorText(e, t('common.failedToSave')))
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
            <Button variant="link" className="text-xs" onClick={() => setCouponsOf(p)}>{t('promotion.coupons.open')}</Button>
            {p.status === 'Draft' && (
              <Button variant="link" className="text-xs" onClick={() => setModal({ open: true, promotion: p })}>{t('common.edit')}</Button>
            )}
            {(p.status === 'Draft' || p.status === 'Paused') && (
              <Button variant="link" className="text-xs" onClick={() => setTransition({ promotion: p, action: 'activate' })}>{t('common.activate')}</Button>
            )}
            {p.status === 'Active' && (
              <Button variant="link" className="text-xs" onClick={() => setTransition({ promotion: p, action: 'pause' })}>{t('promotion.promotions.pause')}</Button>
            )}
            {p.status !== 'Cancelled' && p.status !== 'Expired' && (
              <Button variant="link-danger" className="text-xs" onClick={() => setTransition({ promotion: p, action: 'cancel' })}>{t('common.cancel')}</Button>
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
          <Button onClick={() => setModal({ open: true, promotion: null })}>{t('promotion.promotions.add')}</Button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={data ?? []}
        isLoading={isLoading}
        emptyState={
          <EmptyState
            title={t('promotion.promotions.empty')}
            description={t('promotion.promotions.emptyHint')}
            action={canManage ? <Button onClick={() => setModal({ open: true, promotion: null })}>{t('promotion.promotions.add')}</Button> : undefined}
          />
        }
      />

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
