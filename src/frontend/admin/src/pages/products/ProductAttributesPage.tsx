import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  attributesApi,
  ATTRIBUTE_DATA_TYPE,
  ATTRIBUTE_DATA_TYPE_LABELS,
} from '@/api/product/attributes.api'
import type { ProductAttribute } from '@/api/product/attributes.api'
import { categoriesApi } from '@/api/product/categories.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePermission } from '@/hooks/usePermission'

const DATA_TYPE_OPTIONS = [
  { value: ATTRIBUTE_DATA_TYPE.Text, labelKey: 'products.attributes.dataTypes.text' },
  { value: ATTRIBUTE_DATA_TYPE.Number, labelKey: 'products.attributes.dataTypes.number' },
  { value: ATTRIBUTE_DATA_TYPE.Boolean, labelKey: 'products.attributes.dataTypes.boolean' },
  { value: ATTRIBUTE_DATA_TYPE.Date, labelKey: 'products.attributes.dataTypes.date' },
  { value: ATTRIBUTE_DATA_TYPE.SingleSelect, labelKey: 'products.attributes.dataTypes.singleSelect' },
  { value: ATTRIBUTE_DATA_TYPE.MultiSelect, labelKey: 'products.attributes.dataTypes.multiSelect' },
]

const SELECT_TYPES: number[] = [ATTRIBUTE_DATA_TYPE.SingleSelect, ATTRIBUTE_DATA_TYPE.MultiSelect]

interface AttributeFormValues {
  code: string
  name: string
  dataType: number
  categoryId: string
  unit: string
  isVariantOption: boolean
  isRequired: boolean
  isFilterable: boolean
}

function AttributeModal({
  attribute,
  categories,
  onClose,
}: {
  attribute: ProductAttribute | null
  categories: { id: string; name: string }[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!attribute

  const { register, handleSubmit, watch, formState: { errors } } = useForm<AttributeFormValues>({
    defaultValues: {
      code: attribute?.code ?? '',
      name: attribute?.name ?? '',
      dataType: attribute?.dataType ?? ATTRIBUTE_DATA_TYPE.Text,
      categoryId: attribute?.categoryId ?? '',
      unit: attribute?.unit ?? '',
      isVariantOption: attribute?.isVariantOption ?? false,
      isRequired: attribute?.isRequired ?? false,
      isFilterable: attribute?.isFilterable ?? true,
    },
  })

  // New-option additions are staged locally and submitted right after the attribute
  // itself is created — there is no combined create-with-options-in-one-call for edits.
  const [newOptions, setNewOptions] = useState<string[]>([])
  const [optionDraft, setOptionDraft] = useState('')
  const dataType = Number(watch('dataType'))
  const isSelectType = SELECT_TYPES.includes(dataType)

  const mutation = useMutation({
    mutationFn: async (values: AttributeFormValues) => {
      if (isEdit) {
        await attributesApi.update(attribute!.id, {
          name: values.name,
          unit: values.unit.trim() || undefined,
          isRequired: values.isRequired,
          isFilterable: values.isFilterable,
        })
        for (const value of newOptions) {
          await attributesApi.addOption(attribute!.id, { value, sortOrder: 0 })
        }
        return
      }

      const created = await attributesApi.create({
        code: values.code.trim().toLowerCase(),
        name: values.name.trim(),
        dataType: values.dataType,
        categoryId: values.categoryId || null,
        unit: values.unit.trim() || undefined,
        isVariantOption: values.isVariantOption,
        isRequired: values.isRequired,
        isFilterable: values.isFilterable,
        options: SELECT_TYPES.includes(values.dataType) ? newOptions : undefined,
      })
      return created
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', 'attributes'] })
      onClose()
    },
  })

  const existingOptions = attribute?.options ?? []

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-lg space-y-4 p-6">
        <h3 className="text-base font-semibold text-gray-900">
          {isEdit ? t('products.attributes.editTitle') : t('products.attributes.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="grid grid-cols-2 gap-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.attributes.code')}</label>
            <input
              className="input font-mono"
              disabled={isEdit}
              {...register('code', { required: !isEdit, pattern: /^[a-z0-9_]+$/ })}
              placeholder="e.g. material"
            />
            {errors.code && (
              <p className="mt-1 text-xs text-red-600">{t('products.attributes.codeHint')}</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.attributes.dataType')}</label>
            <select className="input" disabled={isEdit} {...register('dataType', { valueAsNumber: true })}>
              {DATA_TYPE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {t(o.labelKey)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.category')}</label>
            <select className="input" disabled={isEdit} {...register('categoryId')}>
              <option value="">{t('products.attributes.global')}</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.attributes.unit')}</label>
            <input className="input" {...register('unit')} placeholder="e.g. kg, cm" />
          </div>
          <div className="flex flex-col justify-end gap-1">
            <label className="flex items-center gap-2 text-xs text-gray-700">
              <input type="checkbox" disabled={isEdit} {...register('isVariantOption')} />
              {t('products.attributes.isVariantOption')}
            </label>
            <label className="flex items-center gap-2 text-xs text-gray-700">
              <input type="checkbox" {...register('isRequired')} />
              {t('products.attributes.isRequired')}
            </label>
            <label className="flex items-center gap-2 text-xs text-gray-700">
              <input type="checkbox" {...register('isFilterable')} />
              {t('products.attributes.isFilterable')}
            </label>
          </div>

          {isSelectType && (
            <div className="col-span-2 space-y-2 border-t border-gray-100 pt-3">
              <label className="block text-sm font-medium text-gray-700">{t('products.attributes.options')}</label>
              {existingOptions.length > 0 && (
                <ul className="flex flex-wrap gap-2">
                  {existingOptions.map((o) => (
                    <li key={o.id} className="badge bg-gray-100 text-gray-700">
                      {o.value}
                    </li>
                  ))}
                </ul>
              )}
              {newOptions.length > 0 && (
                <ul className="flex flex-wrap gap-2">
                  {newOptions.map((value, idx) => (
                    <li key={`${value}-${idx}`} className="badge bg-primary-50 text-primary-700">
                      {value}
                    </li>
                  ))}
                </ul>
              )}
              <div className="flex gap-2">
                <input
                  className="input"
                  value={optionDraft}
                  onChange={(e) => setOptionDraft(e.target.value)}
                  placeholder={t('products.attributes.addOptionPlaceholder')}
                />
                <button
                  type="button"
                  className="btn-outline"
                  onClick={() => {
                    const value = optionDraft.trim()
                    if (!value) return
                    setNewOptions((prev) => [...prev, value])
                    setOptionDraft('')
                  }}
                >
                  {t('common.add')}
                </button>
              </div>
              {!isEdit && (
                <p className="text-xs text-gray-400">{t('products.attributes.optionsRequiredHint')}</p>
              )}
            </div>
          )}

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

export function ProductAttributesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const canManage = hasPermission('products:edit')

  const [modalState, setModalState] = useState<{ open: boolean; attribute: ProductAttribute | null }>({
    open: false,
    attribute: null,
  })
  const [deleteTarget, setDeleteTarget] = useState<ProductAttribute | null>(null)

  const { data: attributes, isLoading } = useQuery({
    queryKey: ['product', 'attributes'],
    queryFn: () => attributesApi.list(),
    staleTime: 30_000,
  })

  const { data: categories } = useQuery({
    queryKey: ['product', 'categories', 'picker'],
    queryFn: () => categoriesApi.listAll(),
    staleTime: 60_000,
  })

  const categoryNameById = useMemo(
    () => new Map((categories ?? []).map((c) => [c.id, c.name])),
    [categories],
  )

  const deleteMutation = useMutation({
    mutationFn: (id: string) => attributesApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', 'attributes'] })
      setDeleteTarget(null)
    },
  })

  const columns = [
    { key: 'code', label: t('products.attributes.code'), className: 'font-mono text-xs' },
    { key: 'name', label: t('common.name') },
    {
      key: 'dataType',
      label: t('products.attributes.dataType'),
      render: (a: ProductAttribute) => ATTRIBUTE_DATA_TYPE_LABELS[a.dataType] ?? String(a.dataType),
    },
    {
      key: 'categoryId',
      label: t('products.category'),
      render: (a: ProductAttribute) =>
        a.categoryId ? (categoryNameById.get(a.categoryId) ?? '—') : t('products.attributes.global'),
    },
    {
      key: 'isVariantOption',
      label: t('products.attributes.isVariantOption'),
      render: (a: ProductAttribute) => (a.isVariantOption ? t('common.yes') : t('common.no')),
    },
    {
      key: 'isFilterable',
      label: t('products.attributes.isFilterable'),
      render: (a: ProductAttribute) => (a.isFilterable ? t('common.yes') : t('common.no')),
    },
    {
      key: 'actions',
      label: '',
      render: (a: ProductAttribute) =>
        canManage ? (
          <div className="flex gap-3">
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => setModalState({ open: true, attribute: a })}
            >
              {t('common.edit')}
            </button>
            <button className="text-xs text-red-600 hover:underline" onClick={() => setDeleteTarget(a)}>
              {t('common.delete')}
            </button>
          </div>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t('products.attributes.title')}</h1>
          <p className="text-sm text-gray-500">{t('products.attributes.subtitle')}</p>
        </div>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, attribute: null })}>
            {t('products.attributes.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={attributes ?? []} isLoading={isLoading} />

      {modalState.open && (
        <AttributeModal
          attribute={modalState.attribute}
          categories={categories ?? []}
          onClose={() => setModalState({ open: false, attribute: null })}
        />
      )}

      <ConfirmDialog
        open={!!deleteTarget}
        title={t('products.attributes.deleteTitle')}
        message={t('products.attributes.deleteConfirm', { name: deleteTarget?.name })}
        confirmLabel={t('common.delete')}
        danger
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  )
}
