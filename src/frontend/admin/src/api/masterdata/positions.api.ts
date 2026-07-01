import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface Position {
  id: string
  title: string
  code: string
  description?: string | null
  departmentId?: string | null
  isActive: boolean
  languageCode?: string | null
  createdAt: string
  updatedAt: string
}

export interface ListPositionsParams {
  departmentId?: string
  languageCode?: string
  page?: number
  pageSize?: number
}

// CreatePositionCommand(Title, Code, Description, DepartmentId)
export interface CreatePositionRequest {
  title: string
  code: string
  description?: string | null
  departmentId?: string | null
}

// UpdatePositionRequest(Title, Description) — Code/DepartmentId not editable
export interface UpdatePositionRequest {
  title: string
  description?: string | null
}

export const positionsApi = {
  list: (params: ListPositionsParams) =>
    apiClient
      .get<MstPagedResult<Position>>(`${MST_BASE}/positions`, { params })
      .then((r) => r.data),

  get: (id: string, languageCode?: string) =>
    apiClient
      .get<Position>(`${MST_BASE}/positions/${id}`, { params: { languageCode } })
      .then((r) => r.data),

  create: (body: CreatePositionRequest) =>
    apiClient.post<Position>(`${MST_BASE}/positions`, body).then((r) => r.data),

  update: (id: string, body: UpdatePositionRequest) =>
    apiClient.put<Position>(`${MST_BASE}/positions/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<Position>(`${MST_BASE}/positions/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<Position>(`${MST_BASE}/positions/${id}/deactivate`).then((r) => r.data),
}
