import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface ProfitCenter {
  id: string
  companyId: string
  businessUnitId?: string | null
  name: string
  code: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ListProfitCentersParams {
  companyId?: string
  page?: number
  pageSize?: number
}

// CreateProfitCenterCommand(CompanyId, Name, Code, BusinessUnitId)
export interface CreateProfitCenterRequest {
  companyId: string
  name: string
  code: string
  businessUnitId?: string | null
}

// UpdateProfitCenterRequest(Name, Code, BusinessUnitId) — no CompanyId on update
export interface UpdateProfitCenterRequest {
  name: string
  code: string
  businessUnitId?: string | null
}

export const profitCentersApi = {
  list: (params: ListProfitCentersParams) =>
    apiClient
      .get<MstPagedResult<ProfitCenter>>(`${MST_BASE}/profit-centers`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<ProfitCenter>(`${MST_BASE}/profit-centers/${id}`).then((r) => r.data),

  create: (body: CreateProfitCenterRequest) =>
    apiClient.post<ProfitCenter>(`${MST_BASE}/profit-centers`, body).then((r) => r.data),

  update: (id: string, body: UpdateProfitCenterRequest) =>
    apiClient.put<ProfitCenter>(`${MST_BASE}/profit-centers/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<ProfitCenter>(`${MST_BASE}/profit-centers/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<ProfitCenter>(`${MST_BASE}/profit-centers/${id}/deactivate`).then((r) => r.data),
}
