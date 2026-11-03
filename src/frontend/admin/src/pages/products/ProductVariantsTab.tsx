import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { productsApi } from '@/api/products.api'
import type { ProductVariant } from '@/api/products.api'
import { attributesApi } from '@/api/product/attributes.api'
import { pricingApi } from '@/api/product/pricing.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePermission } from '@/hooks/usePermission'
import {
  DynamicAttributeField,
  buildSetAttributeValueRequest,
  mapExistingValueToAttrValue,
} from './dynamicAttributeField'
import type { AttrValue } from './dynamicAttributeField'
import { Alert, Button, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface VariantFormValues {
  sku: string
  barcode: string
  cost: number
  weight: number | ''
  weightUnit: string
  length: number | ''
  width: number | ''
  height: number | ''
  dimensionUnit: string
}

const EMPTY_FORM: VariantFormValues = {
  sku: '',
  barcode: '',
  cost: 0,
  weight: '',
  weightUnit: 'kg',
  length: '',
  width: '',
  height: '',
  dimensionUnit: 'cm',
}

function VariantModal({
  productId,
  categoryId,
  variant,
  onClose,
}: {
  productId: string
  categoryId?: string | null
  variant: ProductVariant | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isEdit = !!variant

  const { register, handleSubmit, formState: { errors } } = useForm<VariantFormValues>({
    defaultValues: variant
      ? {
          sku: variant.sku,
          barcode: variant.barcode ?? '',
          cost: variant.cost,
          weight: variant.weight ?? '',
          weightUnit: variant.weightUnit ?? 'kg',
          length: variant.length ?? '',
          width: variant.width ?? '',
          height: variant.height ?? '',
          dimensionUnit: variant.dimensionUnit ?? 'cm',
        }
      : EMPTY_FORM,
  })

  // Only attributes marked IsVariantOption (e.g. Color/Size) are relevant here — purely
  // descriptive product-level specs live on the Specifications tab instead.
  const { data: attributes } = useQuery({
    queryKey: ['product', 'attributes', 'variantOptions', categoryId ?? null],
    queryFn: () => attributesApi.list(categoryId ?? undefined),
    select: (all) => all.filter((a) => a.isVariantOption),
  })

  const { data: existingValues } = useQuery({
    queryKey: ['products', productId, 'variants', variant?.id, 'attributes'],
    queryFn: () => attributesApi.getValues(productId, variant!.id),
    enabled: isEdit,
  })

  const [attrValues, setAttrValues] = useState<Record<string, AttrValue>>({})

  useEffect(() => {
    if (!attributes || !existingValues) return

    const initial: Record<string, AttrValue> = {}
    for (const attr of attributes) {
      const existing = existingValues.find((v) => v.attributeId === attr.id)
      const mapped = mapExistingValueToAttrValue(attr, existing)
      if (mapped !== undefined) initial[attr.id] = mapped
    }
    setAttrValues(initial)
  }, [attributes, existingValues])

  const mutation = useMutation({
    mutationFn: async (values: VariantFormValues): Promise<void> => {
      const shared = {
        barcode: values.barcode.trim() || undefined,
        cost: Number(values.cost),
        weight: values.weight === '' ? undefined : Number(values.weight),
        weightUnit: values.weightUnit || undefined,
        length: values.length === '' ? undefined : Number(values.length),
        width: values.width === '' ? undefined : Number(values.width),
        height: values.height === '' ? undefined : Number(values.height),
        dimensionUnit: values.dimensionUnit || undefined,
      }

      let targetVariantId = variant?.id
      if (isEdit) {
        await productsApi.updateVariant(productId, variant!.id, shared)
      } else {
        const created = await productsApi.createVariant(productId, {
          sku: values.sku.trim().toUpperCase(),
          ...shared,
        })
        targetVariantId = created.variantId
      }

      if (targetVariantId && attributes && attributes.length > 0) {
        await Promise.all(
          attributes.map((attr) => {
            const request = buildSetAttributeValueRequest(attr, attrValues[attr.id])
            if (!request) return Promise.resolve()
            return attributesApi.setVariantValue(productId, targetVariantId!, attr.id, request)
          }),
        )
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', productId, 'variants'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="lg"
      title={isEdit ? t('products.variants.editTitle') : t('products.variants.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="variant-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="variant-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="grid grid-cols-2 gap-4">
        <FormField className="col-span-2" label={t('products.sku')} required={!isEdit} error={errors.sku ? t('common.required') : undefined}>
          <Input className="font-mono" disabled={isEdit} {...register('sku', { required: !isEdit })} />
        </FormField>
        <FormField className="col-span-2" label={t('products.variants.barcode')}>
          <Input {...register('barcode')} />
        </FormField>
        <FormField label={t('products.variants.cost')}>
          <Input type="number" step="0.01" {...register('cost', { valueAsNumber: true })} />
        </FormField>
        <FormField label={t('products.variants.weight')}>
          <div className="flex gap-2">
            <Input type="number" step="0.01" {...register('weight', { valueAsNumber: true })} />
            <Input className="w-20" aria-label={t('products.variants.weightUnit')} {...register('weightUnit')} />
          </div>
        </FormField>
        <FormField label={t('products.variants.length')}>
          <Input type="number" step="0.01" {...register('length', { valueAsNumber: true })} />
        </FormField>
        <FormField label={t('products.variants.width')}>
          <Input type="number" step="0.01" {...register('width', { valueAsNumber: true })} />
        </FormField>
        <FormField label={t('products.variants.height')}>
          <div className="flex gap-2">
            <Input type="number" step="0.01" {...register('height', { valueAsNumber: true })} />
            <Input className="w-20" aria-label={t('products.variants.dimensionUnit')} {...register('dimensionUnit')} />
          </div>
        </FormField>

        {attributes && attributes.length > 0 && (
          <div className="col-span-2 space-y-3 border-t border-line pt-3">
            <h4 className="text-xs font-semibold uppercase tracking-wide text-ink-secondary">
              {t('products.tabs.specifications')}
            </h4>
            {attributes.map((attr) => (
              <DynamicAttributeField
                key={attr.id}
                attribute={attr}
                label={`${attr.name}${attr.unit ? ` (${attr.unit})` : ''}`}
                value={attrValues[attr.id]}
                onChange={(value) => setAttrValues((prev) => ({ ...prev, [attr.id]: value }))}
              />
            ))}
          </div>
        )}

        {mutation.isError && (
          <Alert variant="danger" className="col-span-2">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>
        )}
      </form>
    </Modal>
  )
}

interface PriceFormValues {
  basePrice: number
  discountedPrice: number | ''
}

/** Sets the retail price of one variant (the shop's "Default retail" price list); the cost is not touched. */
function PriceModal({ productId, variant, current, onClose }: { productId: string; variant: ProductVariant; current?: number | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, getValues, formState: { errors } } = useForm<PriceFormValues>({
    defaultValues: { basePrice: current ?? 0, discountedPrice: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: PriceFormValues) =>
      pricingApi.setVariantPrice({
        variantId: variant.id,
        basePrice: Number(values.basePrice),
        // an empty number field reads as NaN (valueAsNumber): no discount
        discountedPrice: values.discountedPrice === '' || Number.isNaN(Number(values.discountedPrice)) ? null : Number(values.discountedPrice),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', productId, 'retailPrices'] })
      queryClient.invalidateQueries({ queryKey: ['products'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      title={t('products.variants.setPriceTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="variant-price-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="variant-price-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <p className="text-sm text-ink-secondary">{t('products.variants.setPriceHelp', { sku: variant.sku })}</p>
        <FormField label={t('products.variants.basePrice')} required error={errors.basePrice ? t('common.required') : undefined}>
          <Input type="number" step="0.01" min="0" {...register('basePrice', { required: true, valueAsNumber: true, min: 0 })} />
        </FormField>
        <FormField
          label={t('products.variants.discountedPrice')}
          error={errors.discountedPrice ? t('products.variants.discountedBelowBase') : undefined}
        >
          <Input
            type="number"
            step="0.01"
            min="0"
            {...register('discountedPrice', {
              valueAsNumber: true,
              validate: (value) => value === '' || Number.isNaN(Number(value)) || Number(value) < Number(getValues('basePrice')),
            })}
          />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function ProductVariantsTab({ productId, categoryId }: { productId: string; categoryId?: string | null }) {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const canEdit = hasPermission('products:edit')
  const [modalState, setModalState] = useState<{ open: boolean; variant: ProductVariant | null }>({
    open: false,
    variant: null,
  })
  const [deleteTarget, setDeleteTarget] = useState<ProductVariant | null>(null)
  const [priceTarget, setPriceTarget] = useState<ProductVariant | null>(null)

  const { data: variants, isLoading } = useQuery({
    queryKey: ['products', productId, 'variants'],
    queryFn: () => productsApi.listVariants(productId),
  })

  const { data: retailPrices } = useQuery({
    queryKey: ['products', productId, 'retailPrices'],
    queryFn: () => pricingApi.productPrices(productId),
  })
  const retailOf = (variantId: string) => retailPrices?.find((p) => p.variantId === variantId)?.retailPrice ?? null

  const deleteMutation = useMutation({
    mutationFn: (variantId: string) => productsApi.deleteVariant(productId, variantId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', productId, 'variants'] })
      toast.success(t('common.saved'))
      setDeleteTarget(null)
    },
    onError: (e) => {
      setDeleteTarget(null)
      toast.error(serverMessage(e, t('products.variants.deleteLastError')))
    },
  })

  const columns = [
    { key: 'sku', label: t('products.sku'), className: 'font-mono text-xs' },
    { key: 'barcode', label: t('products.variants.barcode'), render: (v: ProductVariant) => v.barcode ?? '—' },
    {
      key: 'cost',
      label: t('products.variants.cost'),
      render: (v: ProductVariant) => v.cost.toFixed(2),
      align: 'right' as const,
    },
    {
      key: 'retailPrice',
      label: t('products.variants.retailPrice'),
      render: (v: ProductVariant) => (retailOf(v.id) != null ? retailOf(v.id)!.toFixed(2) : <span className="text-ink-muted">{t('products.variants.noRetailPrice')}</span>),
      align: 'right' as const,
    },
    {
      key: 'weight',
      label: t('products.variants.weight'),
      render: (v: ProductVariant) => (v.weight != null ? `${v.weight} ${v.weightUnit ?? ''}` : '—'),
    },
    {
      key: 'dimensions',
      label: t('products.variants.dimensions'),
      render: (v: ProductVariant) =>
        v.length != null || v.width != null || v.height != null
          ? `${v.length ?? '?'} × ${v.width ?? '?'} × ${v.height ?? '?'} ${v.dimensionUnit ?? ''}`
          : '—',
    },
    {
      key: 'actions',
      label: '',
      render: (v: ProductVariant) =>
        canEdit ? (
          <div className="flex gap-3">
            <Button variant="link" className="text-xs" onClick={() => setPriceTarget(v)}>
              {t('products.variants.setPrice')}
            </Button>
            <Button variant="link" className="text-xs"
              onClick={() => setModalState({ open: true, variant: v })}>
              {t('common.edit')}
            </Button>
            <Button variant="link-danger" className="text-xs" onClick={() => setDeleteTarget(v)}>
              {t('common.delete')}
            </Button>
          </div>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      {canEdit && (
        <div className="flex justify-end">
          <Button onClick={() => setModalState({ open: true, variant: null })}>
            {t('products.variants.add')}
          </Button>
        </div>
      )}

      <DataTable columns={columns} data={variants ?? []} isLoading={isLoading} />

      {modalState.open && (
        <VariantModal
          productId={productId}
          categoryId={categoryId}
          variant={modalState.variant}
          onClose={() => setModalState({ open: false, variant: null })}
        />
      )}

      {priceTarget && (
        <PriceModal productId={productId} variant={priceTarget} current={retailOf(priceTarget.id)} onClose={() => setPriceTarget(null)} />
      )}

      <ConfirmDialog
        open={!!deleteTarget}
        title={t('products.variants.deleteTitle')}
        message={t('products.variants.deleteConfirm', { sku: deleteTarget?.sku })}
        confirmLabel={t('common.delete')}
        danger
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  )
}
