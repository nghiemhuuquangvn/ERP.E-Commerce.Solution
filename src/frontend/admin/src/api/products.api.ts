import { apiClient } from './client'

// ProductService is mounted at the Gateway's bare `/v1/...` route (confirmed against
// ProductsController/ImagesController/VariantsController/ProductUomConversionsController's
// actual `api/v{version:apiVersion}/...` routes) — unlike Brands/Categories/PriceLists
// (a separate, parallel effort) this file deliberately keeps using the default
// `apiClient` with no extra gateway-root prefix.

// Matches ERP.PRD.Domain.Enums.ProductType
export const PRODUCT_TYPE = {
  Physical: 0,
  Digital: 1,
  Service: 2,
  Bundle: 3,
} as const

// Matches ERP.PRD.Domain.Enums.ProductStatus
export const PRODUCT_STATUS = {
  Draft: 0,
  Active: 1,
  Discontinued: 2,
  PendingReview: 3,
} as const

export const PRODUCT_STATUS_LABELS: Record<number, string> = {
  0: 'Draft',
  1: 'Active',
  2: 'Discontinued',
  3: 'PendingReview',
}

export const PRODUCT_TYPE_LABELS: Record<number, string> = {
  0: 'Physical',
  1: 'Digital',
  2: 'Service',
  3: 'Bundle',
}

// Matches GetProductQuery/ListProductsQuery's ProductDto
export interface Product {
  id: string
  name: string
  sku: string
  description?: string | null
  productType: number
  status: number
  categoryId?: string | null
  categoryName: string
  brandId?: string | null
  baseUomId?: string | null
  hasImage: boolean
  imageUrl?: string | null
  variantCount: number
  stockQuantity: number
  price: number
  languageCode: string
  createdAt: string
  updatedAt?: string | null
}

export interface ListProductsParams {
  skip?: number
  take?: number
  categoryId?: string
  status?: number
  languageCode?: string
}

// Matches ListProductsQuery's ListProductsResponse: { total, skip, take, products } —
// NOT the app-wide { items, totalCount, pageNumber, pageSize } PaginatedResponse<T>
// shape (ProductsController.ListProducts takes skip/take, not page/pageSize, and has
// no `search` query param at all — free-text search is the separate /search endpoint).
export interface ListProductsResponse {
  total: number
  skip: number
  take: number
  products: Product[]
}

export interface CreateProductRequest {
  name: string
  sku: string
  description?: string
  productType: number
  categoryId?: string | null
  brandId?: string | null
  baseUomId?: string | null
}

export interface CreateProductResponse {
  productId: string
  name: string
  sku: string
  status: number
}

export interface SetProductSeoRequest {
  slug?: string | null
  metaTitle?: string | null
  metaDescription?: string | null
}

// --- Images ---
export interface ProductImage {
  id: string
  variantId?: string | null
  imageUrl: string
  altText?: string | null
  sortOrder: number
  isPrimary: boolean
  createdAt: string
}

export interface UploadProductImageRequest {
  variantId?: string | null
  imageUrl: string
  altText?: string
  setAsPrimary?: boolean
}

export interface UploadProductImageResponse {
  imageId: string
  imageUrl: string
  isPrimary: boolean
}

// --- Variants ---
export interface ProductVariant {
  id: string
  productId: string
  sku: string
  barcode?: string | null
  cost: number
  weight?: number | null
  dimensions?: string | null
  length?: number | null
  width?: number | null
  height?: number | null
  dimensionUnit?: string | null
  weightUnit?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface CreateProductVariantRequest {
  sku: string
  barcode?: string
  cost: number
  weight?: number
  length?: number
  width?: number
  height?: number
  dimensionUnit?: string
  weightUnit?: string
}

export interface CreateProductVariantResponse {
  variantId: string
  sku: string
  barcode?: string | null
  cost: number
}

export interface UpdateProductVariantRequest {
  cost?: number
  weight?: number
  barcode?: string
  length?: number
  width?: number
  height?: number
  dimensionUnit?: string
  weightUnit?: string
}

// --- UoM Conversions ---
export interface ProductUomConversion {
  id: string
  productId: string
  fromUomId: string
  toUomId: string
  conversionFactor: number
  createdAt: string
}

export interface CreateProductUomConversionRequest {
  fromUomId: string
  toUomId: string
  conversionFactor: number
}

export interface UpdateProductUomConversionRequest {
  conversionFactor: number
}

export const productsApi = {
  list: (params: ListProductsParams) =>
    apiClient.get<ListProductsResponse>('/v1/products', { params }).then((r) => r.data),

  // Dedicated free-text search endpoint (name/SKU) — capped at 50 results server-side,
  // no pagination metadata returned.
  search: (searchTerm: string, languageCode?: string) =>
    apiClient
      .get<Product[]>('/v1/products/search', { params: { searchTerm, languageCode } })
      .then((r) => r.data),

  get: (id: string, languageCode?: string) =>
    apiClient.get<Product>(`/v1/products/${id}`, { params: { languageCode } }).then((r) => r.data),

  create: (body: CreateProductRequest) =>
    apiClient.post<CreateProductResponse>('/v1/products', body).then((r) => r.data),

  // There is no generic PUT /products/{id} — Name/Sku/ProductType/CategoryId cannot be
  // changed after creation via the current API. Only these granular actions exist:
  updateDescription: (id: string, description: string) =>
    apiClient.put(`/v1/products/${id}/description`, { description }),

  setSeo: (id: string, body: SetProductSeoRequest) => apiClient.put(`/v1/products/${id}/seo`, body),

  setBrand: (id: string, brandId: string | null) =>
    apiClient.put(`/v1/products/${id}/brand`, { brandId }),

  setBaseUom: (id: string, baseUomId: string | null) =>
    apiClient.put(`/v1/products/${id}/base-uom`, { baseUomId }),

  submitForReview: (id: string) => apiClient.post(`/v1/products/${id}/submit-for-review`),

  publish: (id: string) => apiClient.post(`/v1/products/${id}/publish`),

  discontinue: (id: string) => apiClient.post(`/v1/products/${id}/discontinue`),

  // NOTE: Product.Reactivate() exists on the domain entity, but ProductsController has
  // no matching endpoint (verified — no `reactivate`/`activate` route defined there).
  // Deliberately no `reactivate()` call here; add one once the API exposes it.

  // --- Images: products/{productId}/images ---
  listImages: (productId: string) =>
    apiClient.get<ProductImage[]>(`/v1/products/${productId}/images`).then((r) => r.data),

  uploadImage: (productId: string, body: UploadProductImageRequest) =>
    apiClient
      .post<UploadProductImageResponse>(`/v1/products/${productId}/images`, body)
      .then((r) => r.data),

  // Real file upload (multipart/form-data) — the primary path from the Images tab.
  // `apiClient` sets a default 'Content-Type: application/json' header at the
  // instance level, and axios does NOT clear an instance-level default for FormData
  // bodies the way it does a per-request one — without this override the request
  // goes out as JSON and ASP.NET Core's model binder rejects it with "the file field
  // is required" before ever reaching the controller. Explicitly unsetting it here
  // lets the browser set the correct multipart boundary.
  uploadImageFile: (
    productId: string,
    file: File,
    options?: { variantId?: string | null; altText?: string; setAsPrimary?: boolean },
  ) => {
    const formData = new FormData()
    formData.append('file', file)
    if (options?.variantId) formData.append('variantId', options.variantId)
    if (options?.altText) formData.append('altText', options.altText)
    if (options?.setAsPrimary) formData.append('setAsPrimary', String(options.setAsPrimary))
    return apiClient
      .post<UploadProductImageResponse>(`/v1/products/${productId}/images/file`, formData, {
        headers: { 'Content-Type': undefined },
      })
      .then((r) => r.data)
  },

  // Edits an existing image's metadata, optionally replacing its file entirely
  // ("Replace image") by including a new `file`.
  updateImage: (
    productId: string,
    imageId: string,
    body: { altText?: string; sortOrder: number; file?: File },
  ) => {
    const formData = new FormData()
    if (body.file) formData.append('file', body.file)
    if (body.altText) formData.append('altText', body.altText)
    formData.append('sortOrder', String(body.sortOrder))
    return apiClient.put(`/v1/products/${productId}/images/${imageId}`, formData, {
      headers: { 'Content-Type': undefined },
    })
  },

  removeImage: (productId: string, imageId: string) =>
    apiClient.delete(`/v1/products/${productId}/images/${imageId}`),

  setPrimaryImage: (productId: string, imageId: string) =>
    apiClient.post(`/v1/products/${productId}/images/${imageId}/primary`),

  // --- Variants: products/{productId}/variants ---
  listVariants: (productId: string) =>
    apiClient.get<ProductVariant[]>(`/v1/products/${productId}/variants`).then((r) => r.data),

  createVariant: (productId: string, body: CreateProductVariantRequest) =>
    apiClient
      .post<CreateProductVariantResponse>(`/v1/products/${productId}/variants`, body)
      .then((r) => r.data),

  updateVariant: (productId: string, variantId: string, body: UpdateProductVariantRequest) =>
    apiClient.put(`/v1/products/${productId}/variants/${variantId}`, body),

  deleteVariant: (productId: string, variantId: string) =>
    apiClient.delete(`/v1/products/${productId}/variants/${variantId}`),

  // --- UoM Conversions: products/{productId}/uom-conversions ---
  listUomConversions: (productId: string) =>
    apiClient
      .get<ProductUomConversion[]>(`/v1/products/${productId}/uom-conversions`)
      .then((r) => r.data),

  createUomConversion: (productId: string, body: CreateProductUomConversionRequest) =>
    apiClient
      .post<ProductUomConversion>(`/v1/products/${productId}/uom-conversions`, body)
      .then((r) => r.data),

  updateUomConversion: (
    productId: string,
    uomConversionId: string,
    body: UpdateProductUomConversionRequest,
  ) => apiClient.put(`/v1/products/${productId}/uom-conversions/${uomConversionId}`, body),
}
