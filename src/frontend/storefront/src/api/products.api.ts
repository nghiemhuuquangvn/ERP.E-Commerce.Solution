import { apiClient } from './client'

export interface Product {
  id: string
  sku: string
  name: string
  description: string
  price: number
  imageUrl?: string
  categoryId: string
  categoryName: string
  stockQuantity: number
  status: 'Active' | 'Discontinued'
  /** The culture code the backend actually returned content in (translated, or fallback default). */
  languageCode?: string
}

export interface ListProductsParams {
  page?: number
  pageSize?: number
  search?: string
  categoryId?: string
  minPrice?: number
  maxPrice?: number
  sortBy?: 'price_asc' | 'price_desc' | 'name' | 'newest'
  /** Explicit culture code to request translated content for, e.g. 'vi-VN'. Defaults to the Accept-Language header when omitted. */
  languageCode?: string
}

export interface PaginatedProducts {
  items: Product[]
  totalCount: number
  pageNumber: number
  pageSize: number
}

export const productsApi = {
  list: (params: ListProductsParams) =>
    apiClient.get<PaginatedProducts>('/v1/products', { params }).then((r) => r.data),

  get: (id: string, languageCode?: string) =>
    apiClient.get<Product>(`/v1/products/${id}`, { params: { languageCode } }).then((r) => r.data),

  featured: (languageCode?: string) =>
    apiClient.get<Product[]>('/v1/products/featured', { params: { languageCode } }).then((r) => r.data),
}
