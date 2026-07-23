import { apiClient, PRD_BASE } from './prdClient'

// BrandDto (GetBrandsQuery / GetBrandQuery) — GetBrands has no pagination,
// it always returns the full matching list (optionally filtered by IsActive).
export interface Brand {
  id: string
  code: string
  name: string
  logoUrl?: string | null
  isActive: boolean
  createdAt: string
}

export interface ListBrandsParams {
  isActive?: boolean
}

// CreateBrandCommand(Code, Name, LogoUrl)
export interface CreateBrandRequest {
  code: string
  name: string
  logoUrl?: string
}

export interface CreateBrandResponse {
  brandId: string
  code: string
  name: string
}

// UpdateBrandCommand(Name, LogoUrl) — no Code on update (immutable after create)
export interface UpdateBrandRequest {
  name: string
  logoUrl?: string
}

export const brandsApi = {
  list: (params?: ListBrandsParams) =>
    apiClient.get<Brand[]>(`${PRD_BASE}/brands`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<Brand>(`${PRD_BASE}/brands/${id}`).then((r) => r.data),

  create: (body: CreateBrandRequest) =>
    apiClient.post<CreateBrandResponse>(`${PRD_BASE}/brands`, body).then((r) => r.data),

  update: (id: string, body: UpdateBrandRequest) => apiClient.put(`${PRD_BASE}/brands/${id}`, body),

  activate: (id: string) => apiClient.post(`${PRD_BASE}/brands/${id}/activate`),

  deactivate: (id: string) => apiClient.post(`${PRD_BASE}/brands/${id}/deactivate`),
}
