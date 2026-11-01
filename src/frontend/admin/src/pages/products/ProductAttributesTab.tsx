import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { attributesApi } from '@/api/product/attributes.api'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, Spinner, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
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
  const toast = useToast()
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
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) {
    return <Spinner />
  }

  if (!attributes || attributes.length === 0) {
    return <EmptyState title={t('products.attributes.noneDefined')} />
  }

  return (
    <div className="card space-y-4 p-5">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
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

      {canEdit && (
        <div className="flex justify-end gap-3 border-t border-line pt-4">
          <Button loading={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
            {t('common.save')}
          </Button>
        </div>
      )}
      {saveMutation.isError && <Alert variant="danger">{serverMessage(saveMutation.error, t('common.failedToSave'))}</Alert>}
    </div>
  )
}
