import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { productsApi } from '@/api/products.api'
import type { ProductVariant } from '@/api/products.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePermission } from '@/hooks/usePermission'

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
  variant,
  onClose,
}: {
  productId: string
  variant: ProductVariant | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
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
      if (isEdit) {
        await productsApi.updateVariant(productId, variant!.id, shared)
        return
      }
      await productsApi.createVariant(productId, { sku: values.sku.trim().toUpperCase(), ...shared })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', productId, 'variants'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-lg p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('products.variants.editTitle') : t('products.variants.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="grid grid-cols-2 gap-4">
          <div className="col-span-2">
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.sku')}</label>
            <input className="input font-mono" disabled={isEdit} {...register('sku', { required: !isEdit })} />
            {errors.sku && <p className="mt-1 text-xs text-red-600">{t('products.sku')} is required.</p>}
          </div>
          <div className="col-span-2">
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.variants.barcode')}</label>
            <input className="input" {...register('barcode')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.variants.cost')}</label>
            <input type="number" step="0.01" className="input" {...register('cost', { valueAsNumber: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.variants.weight')}</label>
            <div className="flex gap-2">
              <input type="number" step="0.01" className="input" {...register('weight', { valueAsNumber: true })} />
              <input className="input w-20" {...register('weightUnit')} />
            </div>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.variants.length')}</label>
            <input type="number" step="0.01" className="input" {...register('length', { valueAsNumber: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.variants.width')}</label>
            <input type="number" step="0.01" className="input" {...register('width', { valueAsNumber: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.variants.height')}</label>
            <div className="flex gap-2">
              <input type="number" step="0.01" className="input" {...register('height', { valueAsNumber: true })} />
              <input className="input w-20" {...register('dimensionUnit')} />
            </div>
          </div>

          {mutation.isError && (
            <p className="col-span-2 text-xs text-red-600">{t('common.failedToSave')}</p>
          )}

          <div className="col-span-2 flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function ProductVariantsTab({ productId }: { productId: string }) {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const canEdit = hasPermission('products:edit')
  const [modalState, setModalState] = useState<{ open: boolean; variant: ProductVariant | null }>({
    open: false,
    variant: null,
  })
  const [deleteTarget, setDeleteTarget] = useState<ProductVariant | null>(null)

  const { data: variants, isLoading } = useQuery({
    queryKey: ['products', productId, 'variants'],
    queryFn: () => productsApi.listVariants(productId),
  })

  const deleteMutation = useMutation({
    mutationFn: (variantId: string) => productsApi.deleteVariant(productId, variantId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', productId, 'variants'] })
      setDeleteTarget(null)
    },
  })

  const columns = [
    { key: 'sku', label: t('products.sku'), className: 'font-mono text-xs' },
    { key: 'barcode', label: t('products.variants.barcode'), render: (v: ProductVariant) => v.barcode ?? '—' },
    {
      key: 'cost',
      label: t('products.variants.cost'),
      render: (v: ProductVariant) => v.cost.toFixed(2),
      className: 'text-right',
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
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => setModalState({ open: true, variant: v })}
            >
              {t('common.edit')}
            </button>
            <button className="text-xs text-red-600 hover:underline" onClick={() => setDeleteTarget(v)}>
              {t('common.delete')}
            </button>
          </div>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      {canEdit && (
        <div className="flex justify-end">
          <button className="btn-primary" onClick={() => setModalState({ open: true, variant: null })}>
            {t('products.variants.add')}
          </button>
        </div>
      )}

      <DataTable columns={columns} data={variants ?? []} isLoading={isLoading} />

      {modalState.open && (
        <VariantModal
          productId={productId}
          variant={modalState.variant}
          onClose={() => setModalState({ open: false, variant: null })}
        />
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
      {deleteMutation.isError && (
        <p className="text-xs text-red-600">{t('products.variants.deleteLastError')}</p>
      )}
    </div>
  )
}
