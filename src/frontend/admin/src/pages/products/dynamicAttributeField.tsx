import { useTranslation } from 'react-i18next'
import { ATTRIBUTE_DATA_TYPE } from '@/api/product/attributes.api'
import type { AttributeValue, ProductAttribute, SetAttributeValueRequest } from '@/api/product/attributes.api'

// Shared between ProductVariantsTab's per-variant "Color/Size" fields and
// ProductAttributesTab's product-level "Specifications" fields — both render one input
// per ProductAttribute definition, switched on DataType, and both submit through
// SetProductAttributeValueCommand's exactly-one-Value*-field contract.

// A dynamic attribute's in-progress form value — shape depends on the attribute's DataType.
export type AttrValue = string | number | boolean | string[]

export function isEmptyAttrValue(value: AttrValue | undefined): boolean {
  if (value === undefined || value === '') return true
  if (Array.isArray(value)) return value.length === 0
  return false
}

// Builds the request SetProductAttributeValueCommand expects — exactly one Value* field
// populated, matching the attribute's DataType (server-validated, mirrored here so bad
// input never reaches the API).
export function buildSetAttributeValueRequest(
  attribute: ProductAttribute,
  value: AttrValue | undefined,
): SetAttributeValueRequest | null {
  if (isEmptyAttrValue(value)) return null

  switch (attribute.dataType) {
    case ATTRIBUTE_DATA_TYPE.Text:
      return { valueText: String(value) }
    case ATTRIBUTE_DATA_TYPE.Number:
      return { valueNumber: Number(value) }
    case ATTRIBUTE_DATA_TYPE.Boolean:
      return { valueBoolean: Boolean(value) }
    case ATTRIBUTE_DATA_TYPE.Date:
      return { valueDate: String(value) }
    case ATTRIBUTE_DATA_TYPE.SingleSelect:
      return { productAttributeOptionId: String(value) }
    case ATTRIBUTE_DATA_TYPE.MultiSelect:
      return { valueOptionIds: value as string[] }
    default:
      return null
  }
}

// GetProductAttributeValuesQuery resolves SingleSelect/MultiSelect down to the option's
// display Value string, not its Id — this maps back to option Id(s) so a <select>/
// checkbox-list bound to Ids can be prefilled.
export function mapExistingValueToAttrValue(
  attribute: ProductAttribute,
  existing: AttributeValue | undefined,
): AttrValue | undefined {
  if (!existing) return undefined

  switch (attribute.dataType) {
    case ATTRIBUTE_DATA_TYPE.Text:
      return existing.valueText ?? undefined
    case ATTRIBUTE_DATA_TYPE.Number:
      return existing.valueNumber ?? undefined
    case ATTRIBUTE_DATA_TYPE.Boolean:
      return existing.valueBoolean ?? undefined
    case ATTRIBUTE_DATA_TYPE.Date:
      return existing.valueDate ? existing.valueDate.slice(0, 10) : undefined
    case ATTRIBUTE_DATA_TYPE.SingleSelect:
      return attribute.options.find((o) => o.value === existing.optionValue)?.id
    case ATTRIBUTE_DATA_TYPE.MultiSelect:
      return attribute.options.filter((o) => existing.optionValues.includes(o.value)).map((o) => o.id)
    default:
      return undefined
  }
}

export function DynamicAttributeField({
  attribute,
  value,
  onChange,
}: {
  attribute: ProductAttribute
  value: AttrValue | undefined
  onChange: (value: AttrValue) => void
}) {
  const { t } = useTranslation()

  switch (attribute.dataType) {
    case ATTRIBUTE_DATA_TYPE.Number:
      return (
        <input
          type="number"
          step="0.01"
          className="input"
          value={value === undefined ? '' : (value as number)}
          onChange={(e) => onChange(e.target.value === '' ? '' : Number(e.target.value))}
        />
      )
    case ATTRIBUTE_DATA_TYPE.Boolean:
      return (
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={Boolean(value)} onChange={(e) => onChange(e.target.checked)} />
          {t('common.yes')}
        </label>
      )
    case ATTRIBUTE_DATA_TYPE.Date:
      return (
        <input
          type="date"
          className="input"
          value={(value as string) ?? ''}
          onChange={(e) => onChange(e.target.value)}
        />
      )
    case ATTRIBUTE_DATA_TYPE.SingleSelect:
      return (
        <select className="input" value={(value as string) ?? ''} onChange={(e) => onChange(e.target.value)}>
          <option value="">{t('common.none')}</option>
          {attribute.options.map((o) => (
            <option key={o.id} value={o.id}>
              {o.value}
            </option>
          ))}
        </select>
      )
    case ATTRIBUTE_DATA_TYPE.MultiSelect: {
      const selected = Array.isArray(value) ? value : []
      return (
        <div className="flex flex-wrap gap-3">
          {attribute.options.map((o) => (
            <label key={o.id} className="flex items-center gap-1 text-xs text-gray-700">
              <input
                type="checkbox"
                checked={selected.includes(o.id)}
                onChange={(e) => {
                  onChange(e.target.checked ? [...selected, o.id] : selected.filter((id) => id !== o.id))
                }}
              />
              {o.value}
            </label>
          ))}
        </div>
      )
    }
    default:
      return <input className="input" value={(value as string) ?? ''} onChange={(e) => onChange(e.target.value)} />
  }
}
