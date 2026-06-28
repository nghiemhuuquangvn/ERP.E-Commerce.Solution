import { apiClient, PaginatedResponse } from './client'

export interface Product {
  id: string
  sku: string
  name: string
  categoryId: string
  categoryName: string
  price: number
  stockQuantity: number
  status: 'Active' | 'Discontinued' | 'Draft'
  imageUrl?: string
  createdAt: string
}

export interface ListProductsParams {
  page?: number
  pageSize?: number
  search?: string
  categoryId?: string
  status?: string
}

export const productsApi = {
  list: (params: ListProductsParams) =>
    apiClient.get<PaginatedResponse<Product>>('/v1/products', { params }).then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Product>(`/v1/products/${id}`).then((r) => r.data),

  create: (body: Omit<Product, 'id' | 'createdAt'>) =>
    apiClient.post<Product>('/v1/products', body).then((r) => r.data),

  update: (id: string, body: Partial<Product>) =>
    apiClient.put<Product>(`/v1/products/${id}`, body).then((r) => r.data),

  discontinue: (id: string) =>
    apiClient.post(`/v1/products/${id}/discontinue`),
}
