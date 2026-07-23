import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface EmployeeTeamAssignment {
  id: string
  employeeId: string
  teamId: string
  positionId?: string | null
  effectiveFrom: string
  effectiveTo?: string | null
  createdAt: string
  updatedAt: string
}

export interface ListEmployeeTeamAssignmentsParams {
  employeeId: string
  page?: number
  pageSize?: number
}

// CreateEmployeeTeamAssignmentCommand(EmployeeId, TeamId, EffectiveFrom, PositionId)
export interface CreateEmployeeTeamAssignmentRequest {
  employeeId: string
  teamId: string
  effectiveFrom: string
  positionId?: string | null
}

// EndEmployeeTeamAssignmentRequest(EffectiveTo)
export interface EndEmployeeTeamAssignmentRequest {
  effectiveTo: string
}

export const employeeTeamAssignmentsApi = {
  list: (params: ListEmployeeTeamAssignmentsParams) =>
    apiClient
      .get<MstPagedResult<EmployeeTeamAssignment>>(`${MST_BASE}/employee-team-assignments`, {
        params,
      })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient
      .get<EmployeeTeamAssignment>(`${MST_BASE}/employee-team-assignments/${id}`)
      .then((r) => r.data),

  create: (body: CreateEmployeeTeamAssignmentRequest) =>
    apiClient
      .post<EmployeeTeamAssignment>(`${MST_BASE}/employee-team-assignments`, body)
      .then((r) => r.data),

  end: (id: string, body: EndEmployeeTeamAssignmentRequest) =>
    apiClient
      .post<EmployeeTeamAssignment>(`${MST_BASE}/employee-team-assignments/${id}/end`, body)
      .then((r) => r.data),
}
