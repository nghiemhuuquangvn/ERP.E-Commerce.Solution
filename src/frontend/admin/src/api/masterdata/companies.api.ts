import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface Company {
  id: string
  name: string
  code: string
  taxCode?: string | null
  legalAddress?: string | null
  currencyCode?: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ListCompaniesParams {
  page?: number
  pageSize?: number
}

// CreateCompanyCommand(Name, Code, TaxCode, LegalAddress, CurrencyCode)
export interface CreateCompanyRequest {
  name: string
  code: string
  taxCode?: string | null
  legalAddress?: string | null
  currencyCode?: string | null
}

// UpdateCompanyRequest(Name, Code, TaxCode, LegalAddress, CurrencyCode)
export interface UpdateCompanyRequest {
  name: string
  code: string
  taxCode?: string | null
  legalAddress?: string | null
  currencyCode?: string | null
}

export const companiesApi = {
  list: (params: ListCompaniesParams) =>
    apiClient
      .get<MstPagedResult<Company>>(`${MST_BASE}/companies`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Company>(`${MST_BASE}/companies/${id}`).then((r) => r.data),

  create: (body: CreateCompanyRequest) =>
    apiClient.post<Company>(`${MST_BASE}/companies`, body).then((r) => r.data),

  update: (id: string, body: UpdateCompanyRequest) =>
    apiClient.put<Company>(`${MST_BASE}/companies/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<Company>(`${MST_BASE}/companies/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<Company>(`${MST_BASE}/companies/${id}/deactivate`).then((r) => r.data),
}
