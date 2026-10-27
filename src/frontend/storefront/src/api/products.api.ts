import { apiClient } from './client'

export interface Product {
  id: string
  sku: string
  name: string
  description: string
  price: number
  imageUrl?: string
  categoryId?: string | null
  categoryName: string
  stockQuantity: number
  /** ProductDto.Status is a number on the wire (not a name). */
  status: number
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
  /** Explicit culture code to request translated content for, e.g. 'vi-VN'. */
  languageCode?: string
}

export interface PaginatedProducts {
  items: Product[]
  totalCount: number
  pageNumber: number
  pageSize: number
}

// Backend list response shape: { Total, Skip, Take, Products }
interface BackendListResponse {
  total: number
  skip: number
  take: number
  products: Product[]
}

export const productsApi = {
  list: async (params: ListProductsParams): Promise<PaginatedProducts> => {
    const page = params.page ?? 1
    const pageSize = params.pageSize ?? 20

    // When search term is present, delegate to the search endpoint (returns Product[])
    if (params.search?.trim()) {
      const { data } = await apiClient.get<Product[]>('/v1/products/search', {
        params: { searchTerm: params.search.trim(), languageCode: params.languageCode },
      })
      return { items: data, totalCount: data.length, pageNumber: 1, pageSize: data.length }
    }

    // Map page/pageSize → skip/take; categoryId and languageCode pass through
    const { data } = await apiClient.get<BackendListResponse>('/v1/products', {
      params: {
        skip: (page - 1) * pageSize,
        take: pageSize,
        categoryId: params.categoryId,
        languageCode: params.languageCode,
      },
    })

    return {
      items: data.products ?? [],
      totalCount: data.total ?? 0,
      pageNumber: page,
      pageSize: data.take ?? pageSize,
    }
  },

  get: (id: string, languageCode?: string) =>
    apiClient.get<Product>(`/v1/products/${id}`, { params: { languageCode } }).then((r) => r.data),

  featured: (languageCode?: string) =>
    apiClient.get<Product[]>('/v1/products/featured', { params: { languageCode } }).then((r) => r.data),
}
