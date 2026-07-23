import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface BusinessUnit {
  id: string
  companyId: string
  name: string
  code: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ListBusinessUnitsParams {
  companyId?: string
  page?: number
  pageSize?: number
}

// CreateBusinessUnitCommand(CompanyId, Name, Code)
export interface CreateBusinessUnitRequest {
  companyId: string
  name: string
  code: string
}

// UpdateBusinessUnitRequest(Name, Code) — no CompanyId on update
export interface UpdateBusinessUnitRequest {
  name: string
  code: string
}

export const businessUnitsApi = {
  list: (params: ListBusinessUnitsParams) =>
    apiClient
      .get<MstPagedResult<BusinessUnit>>(`${MST_BASE}/business-units`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<BusinessUnit>(`${MST_BASE}/business-units/${id}`).then((r) => r.data),

  create: (body: CreateBusinessUnitRequest) =>
    apiClient.post<BusinessUnit>(`${MST_BASE}/business-units`, body).then((r) => r.data),

  update: (id: string, body: UpdateBusinessUnitRequest) =>
    apiClient.put<BusinessUnit>(`${MST_BASE}/business-units/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<BusinessUnit>(`${MST_BASE}/business-units/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<BusinessUnit>(`${MST_BASE}/business-units/${id}/deactivate`).then((r) => r.data),
}
