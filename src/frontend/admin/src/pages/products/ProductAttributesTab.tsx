import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { attributesApi } from '@/api/product/attributes.api'
import { usePermission } from '@/hooks/usePermission'
import {
  DynamicAttributeField,
  buildSetAttributeValueRequest,
  mapExistingValueToAttrValue,
} from './dynamicAttributeField'
import type { AttrValue } from './dynamicAttributeField'

// Product-level descriptive specs (OwnerType=Product) — e.g. "Material", "Country of
// Origin" — as opposed to ProductVariantsTab's per-variant IsVariantOption attributes
// (e.g. Color/Size). Both share the same dynamic-input-by-DataType rendering.
export function ProductAttributesTab({
  productId,
  categoryId,
}: {
  productId: string
  categoryId?: string | null
}) {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const canEdit = hasPermission('products:edit')

  const { data: attributes } = useQuery({
    queryKey: ['product', 'attributes', 'specifications', categoryId ?? null],
    queryFn: () => attributesApi.list(categoryId ?? undefined),
    select: (all) => all.filter((a) => !a.isVariantOption),
  })

  const { data: existingValues, isLoading } = useQuery({
    queryKey: ['products', productId, 'attributes'],
    queryFn: () => attributesApi.getValues(productId),
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

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!attributes) return
      await Promise.all(
        attributes.map((attr) => {
          const request = buildSetAttributeValueRequest(attr, attrValues[attr.id])
          if (!request) return Promise.resolve()
          return attributesApi.setProductValue(productId, attr.id, request)
        }),
      )
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', productId, 'attributes'] })
    },
  })

  if (isLoading) {
    return <div className="text-gray-400">{t('common.loading')}</div>
  }

  if (!attributes || attributes.length === 0) {
    return <p className="text-sm text-gray-400">{t('products.attributes.noneDefined')}</p>
  }

  return (
    <div className="card space-y-4 p-5">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {attributes.map((attr) => (
          <div key={attr.id}>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {attr.name}
              {attr.unit ? ` (${attr.unit})` : ''}
            </label>
            <DynamicAttributeField
              attribute={attr}
              value={attrValues[attr.id]}
              onChange={(value) => setAttrValues((prev) => ({ ...prev, [attr.id]: value }))}
            />
          </div>
        ))}
      </div>

      {canEdit && (
        <div className="flex justify-end gap-3 border-t border-gray-100 pt-4">
          <button
            className="btn-primary"
            disabled={saveMutation.isPending}
            onClick={() => saveMutation.mutate()}
          >
            {saveMutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
      {saveMutation.isError && <p className="text-sm text-red-600">{t('common.failedToSave')}</p>}
    </div>
  )
}
