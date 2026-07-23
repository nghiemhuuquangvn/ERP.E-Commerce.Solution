import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { productsApi } from '@/api/products.api'
import type { ProductUomConversion } from '@/api/products.api'
import { unitsOfMeasureApi } from '@/api/masterdata/unitsOfMeasure.api'
import { DataTable } from '@/components/shared/DataTable'
import { usePermission } from '@/hooks/usePermission'

interface UomConversionFormValues {
  fromUomId: string
  toUomId: string
  conversionFactor: number
}

function UomConversionModal({
  productId,
  conversion,
  uoms,
  onClose,
}: {
  productId: string
  conversion: ProductUomConversion | null
  uoms: { id: string; name: string; symbol: string }[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!conversion

  const { register, handleSubmit, formState: { errors } } = useForm<UomConversionFormValues>({
    defaultValues: {
      fromUomId: conversion?.fromUomId ?? '',
      toUomId: conversion?.toUomId ?? '',
      conversionFactor: conversion?.conversionFactor ?? 1,
    },
  })

  const mutation = useMutation({
    mutationFn: async (values: UomConversionFormValues): Promise<void> => {
      if (isEdit) {
        await productsApi.updateUomConversion(productId, conversion!.id, {
          conversionFactor: Number(values.conversionFactor),
        })
        return
      }
      await productsApi.createUomConversion(productId, {
        fromUomId: values.fromUomId,
        toUomId: values.toUomId,
        conversionFactor: Number(values.conversionFactor),
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', productId, 'uomConversions'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('products.uomConversions.editTitle') : t('products.uomConversions.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.uomConversions.fromUom')}</label>
            <select className="input" disabled={isEdit} {...register('fromUomId', { required: !isEdit })}>
              <option value="">{t('common.select')}</option>
              {uoms.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name} ({u.symbol})
                </option>
              ))}
            </select>
            {errors.fromUomId && <p className="mt-1 text-xs text-red-600">{t('products.uomConversions.fromUom')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.uomConversions.toUom')}</label>
            <select className="input" disabled={isEdit} {...register('toUomId', { required: !isEdit })}>
              <option value="">{t('common.select')}</option>
              {uoms.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name} ({u.symbol})
                </option>
              ))}
            </select>
            {errors.toUomId && <p className="mt-1 text-xs text-red-600">{t('products.uomConversions.toUom')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('products.uomConversions.conversionFactor')}
            </label>
            <input
              type="number"
              step="0.000001"
              className="input"
              {...register('conversionFactor', { required: true, valueAsNumber: true, min: 0.000001 })}
            />
            {errors.conversionFactor && (
              <p className="mt-1 text-xs text-red-600">{t('products.uomConversions.conversionFactorInvalid')}</p>
            )}
          </div>

          {mutation.isError && <p className="text-xs text-red-600">{t('common.failedToSave')}</p>}

          <div className="flex justify-end gap-3 pt-2">
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

export function ProductUomConversionsTab({ productId }: { productId: string }) {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canView = hasPermission('product:uom-conversions:view')
  const canManage = hasPermission('product:uom-conversions:manage')
  const [modalState, setModalState] = useState<{ open: boolean; conversion: ProductUomConversion | null }>({
    open: false,
    conversion: null,
  })

  const { data: conversions, isLoading } = useQuery({
    queryKey: ['products', productId, 'uomConversions'],
    queryFn: () => productsApi.listUomConversions(productId),
    enabled: canView,
  })

  const { data: uomPage } = useQuery({
    queryKey: ['mst', 'unitsOfMeasure', 'picker'],
    queryFn: () => unitsOfMeasureApi.list({ pageSize: 200 }),
    staleTime: 60_000,
  })

  const uoms = uomPage?.items ?? []
  const uomName = (id: string) => {
    const u = uoms.find((x) => x.id === id)
    return u ? `${u.name} (${u.symbol})` : id
  }

  if (!canView) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  const columns = [
    {
      key: 'fromUomId',
      label: t('products.uomConversions.fromUom'),
      render: (c: ProductUomConversion) => uomName(c.fromUomId),
    },
    {
      key: 'toUomId',
      label: t('products.uomConversions.toUom'),
      render: (c: ProductUomConversion) => uomName(c.toUomId),
    },
    {
      key: 'conversionFactor',
      label: t('products.uomConversions.conversionFactor'),
      className: 'text-right',
    },
    {
      key: 'actions',
      label: '',
      render: (c: ProductUomConversion) =>
        canManage ? (
          <button
            className="text-xs text-primary-600 hover:underline"
            onClick={() => setModalState({ open: true, conversion: c })}
          >
            {t('common.edit')}
          </button>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      {canManage && (
        <div className="flex justify-end">
          <button className="btn-primary" onClick={() => setModalState({ open: true, conversion: null })}>
            {t('products.uomConversions.add')}
          </button>
        </div>
      )}

      <DataTable columns={columns} data={conversions ?? []} isLoading={isLoading} />

      {modalState.open && (
        <UomConversionModal
          productId={productId}
          conversion={modalState.conversion}
          uoms={uoms}
          onClose={() => setModalState({ open: false, conversion: null })}
        />
      )}
    </div>
  )
}
