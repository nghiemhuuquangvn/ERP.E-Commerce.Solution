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
}

export interface ListProductsParams {
  page?: number
  pageSize?: number
  search?: string
  categoryId?: string
  minPrice?: number
  maxPrice?: number
  sortBy?: 'price_asc' | 'price_desc' | 'name' | 'newest'
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

  get: (id: string) =>
    apiClient.get<Product>(`/v1/products/${id}`).then((r) => r.data),

  featured: () =>
    apiClient.get<Product[]>('/v1/products/featured').then((r) => r.data),
}
