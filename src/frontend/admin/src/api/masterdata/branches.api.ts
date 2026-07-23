import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface Branch {
  id: string
  companyId: string
  name: string
  code: string
  address?: string | null
  wardId?: string | null
  managerEmployeeId?: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ListBranchesParams {
  companyId?: string
  page?: number
  pageSize?: number
}

// CreateBranchCommand(CompanyId, Name, Code, Address, WardId, ManagerEmployeeId)
export interface CreateBranchRequest {
  companyId: string
  name: string
  code: string
  address?: string | null
  wardId?: string | null
  managerEmployeeId?: string | null
}

// UpdateBranchRequest(Name, Code, Address, WardId, ManagerEmployeeId) — no CompanyId on update
export interface UpdateBranchRequest {
  name: string
  code: string
  address?: string | null
  wardId?: string | null
  managerEmployeeId?: string | null
}

export const branchesApi = {
  list: (params: ListBranchesParams) =>
    apiClient
      .get<MstPagedResult<Branch>>(`${MST_BASE}/branches`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Branch>(`${MST_BASE}/branches/${id}`).then((r) => r.data),

  create: (body: CreateBranchRequest) =>
    apiClient.post<Branch>(`${MST_BASE}/branches`, body).then((r) => r.data),

  update: (id: string, body: UpdateBranchRequest) =>
    apiClient.put<Branch>(`${MST_BASE}/branches/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<Branch>(`${MST_BASE}/branches/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<Branch>(`${MST_BASE}/branches/${id}/deactivate`).then((r) => r.data),
}
