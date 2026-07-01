import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface Department {
  id: string
  departmentName: string
  code: string
  managerId?: string | null
  parentDepartmentId?: string | null
  languageCode?: string | null
  createdAt: string
  updatedAt: string
}

export interface ListDepartmentsParams {
  page?: number
  pageSize?: number
  languageCode?: string
}

// CreateDepartmentCommand(Name, Code, ManagerId, ParentDepartmentId)
export interface CreateDepartmentRequest {
  name: string
  code: string
  managerId?: string | null
  parentDepartmentId?: string | null
}

// UpdateDepartmentRequest(Name, Code, ManagerId) — no ParentDepartmentId on update
export interface UpdateDepartmentRequest {
  name: string
  code: string
  managerId?: string | null
}

export const departmentsApi = {
  list: (params: ListDepartmentsParams) =>
    apiClient
      .get<MstPagedResult<Department>>(`${MST_BASE}/departments`, { params })
      .then((r) => r.data),

  get: (id: string, languageCode?: string) =>
    apiClient
      .get<Department>(`${MST_BASE}/departments/${id}`, { params: { languageCode } })
      .then((r) => r.data),

  create: (body: CreateDepartmentRequest) =>
    apiClient.post<Department>(`${MST_BASE}/departments`, body).then((r) => r.data),

  update: (id: string, body: UpdateDepartmentRequest) =>
    apiClient.put<Department>(`${MST_BASE}/departments/${id}`, body).then((r) => r.data),
}
