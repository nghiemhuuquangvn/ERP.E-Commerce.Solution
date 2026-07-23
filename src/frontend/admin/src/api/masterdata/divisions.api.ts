import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface Division {
  id: string
  branchId: string
  name: string
  code: string
  parentDivisionId?: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ListDivisionsParams {
  branchId?: string
  page?: number
  pageSize?: number
}

// CreateDivisionCommand(BranchId, Name, Code, ParentDivisionId)
export interface CreateDivisionRequest {
  branchId: string
  name: string
  code: string
  parentDivisionId?: string | null
}

// UpdateDivisionRequest(Name, Code, ParentDivisionId) — no BranchId on update
export interface UpdateDivisionRequest {
  name: string
  code: string
  parentDivisionId?: string | null
}

export const divisionsApi = {
  list: (params: ListDivisionsParams) =>
    apiClient
      .get<MstPagedResult<Division>>(`${MST_BASE}/divisions`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Division>(`${MST_BASE}/divisions/${id}`).then((r) => r.data),

  create: (body: CreateDivisionRequest) =>
    apiClient.post<Division>(`${MST_BASE}/divisions`, body).then((r) => r.data),

  update: (id: string, body: UpdateDivisionRequest) =>
    apiClient.put<Division>(`${MST_BASE}/divisions/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<Division>(`${MST_BASE}/divisions/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<Division>(`${MST_BASE}/divisions/${id}/deactivate`).then((r) => r.data),
}
