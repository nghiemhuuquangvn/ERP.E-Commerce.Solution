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
import { Alert, Button, Checkbox, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  const existingOptions = attribute?.options ?? []

  return (
    <Modal
      open
      size="lg"
      title={isEdit ? t('products.attributes.editTitle') : t('products.attributes.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="attribute-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="attribute-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="grid grid-cols-2 gap-4">
        <FormField
          label={t('products.attributes.code')}
          required={!isEdit}
          error={errors.code ? (errors.code.type === 'pattern' ? t('products.attributes.codeFormat') : t('common.required')) : undefined}
        >
          <Input className="font-mono"
            disabled={isEdit}
            {...register('code', { required: !isEdit, pattern: /^[a-z0-9_]+$/ })}
            placeholder="e.g. material"
          />
        </FormField>
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('products.attributes.dataType')}>
          <Select disabled={isEdit} {...register('dataType', { valueAsNumber: true })}>
            {DATA_TYPE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {t(o.labelKey)}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('products.category')}>
          <Select disabled={isEdit} {...register('categoryId')}>
            <option value="">{t('products.attributes.global')}</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('products.attributes.unit')}>
          <Input {...register('unit')} placeholder="e.g. kg, cm" />
        </FormField>
        <div className="flex flex-col justify-end gap-1">
          <Checkbox label={t('products.attributes.isVariantOption')} disabled={isEdit} {...register('isVariantOption')} />
          <Checkbox label={t('products.attributes.isRequired')} {...register('isRequired')} />
          <Checkbox label={t('products.attributes.isFilterable')} {...register('isFilterable')} />
        </div>

        {isSelectType && (
          <div className="col-span-2 space-y-2 border-t border-line pt-3">
            <FormField label={t('products.attributes.options')}>
              <div className="flex gap-2">
                <Input
                  value={optionDraft}
                  onChange={(e) => setOptionDraft(e.target.value)}
                  placeholder={t('products.attributes.addOptionPlaceholder')}
                />
                <Button variant="outline"
                  type="button"
                  onClick={() => {
                    const value = optionDraft.trim()
                    if (!value) return
                    setNewOptions((prev) => [...prev, value])
                    setOptionDraft('')
                  }}>
                  {t('common.add')}
                </Button>
              </div>
            </FormField>
            {existingOptions.length > 0 && (
              <ul className="flex flex-wrap gap-2">
                {existingOptions.map((o) => (
                  <li key={o.id} className="badge bg-surface-muted text-ink-secondary">
                    {o.value}
                  </li>
                ))}
              </ul>
            )}
            {newOptions.length > 0 && (
              <ul className="flex flex-wrap gap-2">
                {newOptions.map((value, idx) => (
                  <li key={`${value}-${idx}`} className="badge bg-info-soft text-info-soft-text">
                    {value}
                  </li>
                ))}
              </ul>
            )}
            {!isEdit && (
              <p className="text-xs text-ink-secondary">{t('products.attributes.optionsRequiredHint')}</p>
            )}
          </div>
        )}

        {mutation.isError && (
          <Alert variant="danger" className="col-span-2">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>
        )}
      </form>
    </Modal>
  )
}

export function ProductAttributesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
      setDeleteTarget(null)
    },
    onError: (e) => {
      setDeleteTarget(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
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
            <Button variant="link" className="text-xs"
              onClick={() => setModalState({ open: true, attribute: a })}>
              {t('common.edit')}
            </Button>
            <Button variant="link-danger" className="text-xs" onClick={() => setDeleteTarget(a)}>
              {t('common.delete')}
            </Button>
          </div>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t('products.attributes.title')}</h1>
          <p className="text-sm text-ink-secondary">{t('products.attributes.subtitle')}</p>
        </div>
        {canManage && (
          <Button onClick={() => setModalState({ open: true, attribute: null })}>
            {t('products.attributes.add')}
          </Button>
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
