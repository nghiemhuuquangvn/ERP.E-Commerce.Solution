import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface CostCenter {
  id: string
  companyId: string
  departmentId?: string | null
  name: string
  code: string
  budgetAmount: number
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ListCostCentersParams {
  companyId?: string
  page?: number
  pageSize?: number
}

// CreateCostCenterCommand(CompanyId, Name, Code, BudgetAmount, DepartmentId)
export interface CreateCostCenterRequest {
  companyId: string
  name: string
  code: string
  budgetAmount: number
  departmentId?: string | null
}

// UpdateCostCenterRequest(Name, Code, BudgetAmount, DepartmentId) — no CompanyId on update
export interface UpdateCostCenterRequest {
  name: string
  code: string
  budgetAmount: number
  departmentId?: string | null
}

export const costCentersApi = {
  list: (params: ListCostCentersParams) =>
    apiClient
      .get<MstPagedResult<CostCenter>>(`${MST_BASE}/cost-centers`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<CostCenter>(`${MST_BASE}/cost-centers/${id}`).then((r) => r.data),

  create: (body: CreateCostCenterRequest) =>
    apiClient.post<CostCenter>(`${MST_BASE}/cost-centers`, body).then((r) => r.data),

  update: (id: string, body: UpdateCostCenterRequest) =>
    apiClient.put<CostCenter>(`${MST_BASE}/cost-centers/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<CostCenter>(`${MST_BASE}/cost-centers/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<CostCenter>(`${MST_BASE}/cost-centers/${id}/deactivate`).then((r) => r.data),
}
