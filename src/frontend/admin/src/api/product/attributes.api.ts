import { apiClient } from '../client'

// AttributesController is mounted the same way as ProductsController/ImagesController —
// bare `api/v1/...` routes on ProductService, not the `/prd/api/v1/...` gateway prefix
// used by Brand/Category/PriceList (see prdClient.ts) — so this file uses the default
// `apiClient`, mirroring products.api.ts's own convention.

// Matches ERP.PRD.Domain.Enums.AttributeDataType
export const ATTRIBUTE_DATA_TYPE = {
  Text: 0,
  Number: 1,
  Boolean: 2,
  Date: 3,
  SingleSelect: 4,
  MultiSelect: 5,
} as const

export const ATTRIBUTE_DATA_TYPE_LABELS: Record<number, string> = {
  0: 'Text',
  1: 'Number',
  2: 'Boolean',
  3: 'Date',
  4: 'SingleSelect',
  5: 'MultiSelect',
}

// Matches GetProductAttributesQuery's AttributeOptionDto
export interface AttributeOption {
  id: string
  value: string
  sortOrder: number
}

// Matches GetProductAttributesQuery's ProductAttributeDto
export interface ProductAttribute {
  id: string
  code: string
  name: string
  dataType: number
  unit?: string | null
  categoryId?: string | null
  isVariantOption: boolean
  isRequired: boolean
  isFilterable: boolean
  sortOrder: number
  options: AttributeOption[]
}

// Matches CreateProductAttributeCommand — Code is immutable once created (server-enforced).
export interface CreateProductAttributeRequest {
  code: string
  name: string
  dataType: number
  categoryId?: string | null
  unit?: string
  isVariantOption: boolean
  isRequired: boolean
  isFilterable: boolean
  options?: string[]
}

export interface CreateProductAttributeResponse {
  attributeId: string
  code: string
}

// Matches UpdateProductAttributeCommand — Code/DataType deliberately not editable.
export interface UpdateProductAttributeRequest {
  name?: string
  unit?: string
  isRequired?: boolean
  isFilterable?: boolean
  sortOrder?: number
}

export interface AddAttributeOptionRequest {
  value: string
  sortOrder: number
}

export interface AddAttributeOptionResponse {
  optionId: string
  value: string
}

// Matches GetProductAttributeValuesQuery's AttributeValueDto
export interface AttributeValue {
  attributeId: string
  code: string
  name: string
  dataType: number
  unit?: string | null
  valueText?: string | null
  valueNumber?: number | null
  valueBoolean?: boolean | null
  valueDate?: string | null
  optionValue?: string | null
  optionValues: string[]
}

// Matches SetProductAttributeValueCommand — exactly one Value* field must be populated,
// matching the attribute's DataType (server-validated).
export interface SetAttributeValueRequest {
  valueText?: string
  valueNumber?: number
  valueBoolean?: boolean
  valueDate?: string
  productAttributeOptionId?: string
  valueOptionIds?: string[]
}

export const attributesApi = {
  // GetProductAttributesQuery: omitting categoryId returns only global (CategoryId ==
  // null) attributes; passing one includes both that category's and global attributes.
  list: (categoryId?: string) =>
    apiClient.get<ProductAttribute[]>('/v1/product-attributes', { params: { categoryId } }).then((r) => r.data),

  create: (body: CreateProductAttributeRequest) =>
    apiClient.post<CreateProductAttributeResponse>('/v1/product-attributes', body).then((r) => r.data),

  update: (id: string, body: UpdateProductAttributeRequest) =>
    apiClient.put(`/v1/product-attributes/${id}`, body),

  remove: (id: string) => apiClient.delete(`/v1/product-attributes/${id}`),

  // Additive only — no delete-option endpoint exists server-side.
  addOption: (id: string, body: AddAttributeOptionRequest) =>
    apiClient.post<AddAttributeOptionResponse>(`/v1/product-attributes/${id}/options`, body).then((r) => r.data),

  getValues: (productId: string, variantId?: string) =>
    apiClient
      .get<AttributeValue[]>(`/v1/products/${productId}/attributes`, { params: { variantId } })
      .then((r) => r.data),

  setProductValue: (productId: string, attributeId: string, body: SetAttributeValueRequest) =>
    apiClient.put(`/v1/products/${productId}/attributes/${attributeId}`, body),

  setVariantValue: (productId: string, variantId: string, attributeId: string, body: SetAttributeValueRequest) =>
    apiClient.put(`/v1/products/${productId}/variants/${variantId}/attributes/${attributeId}`, body),
}
