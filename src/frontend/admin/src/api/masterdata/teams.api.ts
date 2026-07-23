import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface Team {
  id: string
  departmentId: string
  name: string
  code: string
  leaderEmployeeId?: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ListTeamsParams {
  departmentId?: string
  page?: number
  pageSize?: number
}

// CreateTeamCommand(DepartmentId, Name, Code, LeaderEmployeeId)
export interface CreateTeamRequest {
  departmentId: string
  name: string
  code: string
  leaderEmployeeId?: string | null
}

// UpdateTeamRequest(Name, Code, LeaderEmployeeId) — no DepartmentId on update
export interface UpdateTeamRequest {
  name: string
  code: string
  leaderEmployeeId?: string | null
}

export const teamsApi = {
  list: (params: ListTeamsParams) =>
    apiClient
      .get<MstPagedResult<Team>>(`${MST_BASE}/teams`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Team>(`${MST_BASE}/teams/${id}`).then((r) => r.data),

  create: (body: CreateTeamRequest) =>
    apiClient.post<Team>(`${MST_BASE}/teams`, body).then((r) => r.data),

  update: (id: string, body: UpdateTeamRequest) =>
    apiClient.put<Team>(`${MST_BASE}/teams/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<Team>(`${MST_BASE}/teams/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<Team>(`${MST_BASE}/teams/${id}/deactivate`).then((r) => r.data),
}
