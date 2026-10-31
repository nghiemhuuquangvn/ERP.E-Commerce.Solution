import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { productsApi } from '@/api/products.api'
import type { ProductUomConversion } from '@/api/products.api'
import { unitsOfMeasureApi } from '@/api/masterdata/unitsOfMeasure.api'
import { DataTable } from '@/components/shared/DataTable'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('products.uomConversions.editTitle') : t('products.uomConversions.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="uom-conversion-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="uom-conversion-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('products.uomConversions.fromUom')} required={!isEdit} error={errors.fromUomId ? t('common.required') : undefined}>
          <Select disabled={isEdit} {...register('fromUomId', { required: !isEdit })}>
            <option value="">{t('common.select')}</option>
            {uoms.map((u) => (
              <option key={u.id} value={u.id}>
                {u.name} ({u.symbol})
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('products.uomConversions.toUom')} required={!isEdit} error={errors.toUomId ? t('common.required') : undefined}>
          <Select disabled={isEdit} {...register('toUomId', { required: !isEdit })}>
            <option value="">{t('common.select')}</option>
            {uoms.map((u) => (
              <option key={u.id} value={u.id}>
                {u.name} ({u.symbol})
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('products.uomConversions.conversionFactor')} required error={errors.conversionFactor ? t('common.required') : undefined}>
          <Input
            type="number"
            step="0.000001"
            {...register('conversionFactor', { required: true, valueAsNumber: true, min: 0.000001 })}
          />
        </FormField>

        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
    return <EmptyState variant="forbidden" title={t('common.noPermission')} />
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
      align: 'right' as const,
    },
    {
      key: 'actions',
      label: '',
      render: (c: ProductUomConversion) =>
        canManage ? (
          <Button variant="link" className="text-xs"
            onClick={() => setModalState({ open: true, conversion: c })}>
            {t('common.edit')}
          </Button>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      {canManage && (
        <div className="flex justify-end">
          <Button onClick={() => setModalState({ open: true, conversion: null })}>
            {t('products.uomConversions.add')}
          </Button>
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
