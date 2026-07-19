import { apiClient, HRM_BASE, HrmPagedResult } from './hrmClient'

// Matches ERP.HRM.Domain.Enums.LeaveType (int accepted by SubmitLeaveRequestCommand).
export const LEAVE_TYPE = {
  Annual: 0,
  Sick: 1,
  Unpaid: 2,
  Maternity: 3,
  Paternity: 4,
} as const

// Matches ERP.HRM.Domain.Enums.LeaveStatus (int on the wire for filters, string
// in LeaveRequestDto responses).
export const LEAVE_STATUS = {
  Pending: 0,
  Approved: 1,
  Rejected: 2,
  Cancelled: 3,
} as const

export interface LeaveRequest {
  id: string
  leaveRequestNumber: string
  employeeId: string
  leaveType: string
  startDate: string
  endDate: string
  daysRequested: number
  leaveStatus: string
  reason?: string | null
  createdAt: string
  updatedAt: string
}

export interface ListLeaveRequestsParams {
  employeeId?: string
  status?: number
  page?: number
  pageSize?: number
}

// SubmitLeaveRequestCommand(EmployeeId, LeaveType, StartDate, EndDate, Reason)
export interface SubmitLeaveRequestRequest {
  employeeId: string
  leaveType: number
  startDate: string
  endDate: string
  reason?: string | null
}

// LeaveRequestsController.RejectLeaveRequest(Reason)
export interface RejectLeaveRequestBody {
  reason: string
}

export const leaveRequestsApi = {
  list: (params: ListLeaveRequestsParams) =>
    apiClient
      .get<HrmPagedResult<LeaveRequest>>(`${HRM_BASE}/leave-requests`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<LeaveRequest>(`${HRM_BASE}/leave-requests/${id}`).then((r) => r.data),

  submit: (body: SubmitLeaveRequestRequest) =>
    apiClient.post<LeaveRequest>(`${HRM_BASE}/leave-requests`, body).then((r) => r.data),

  approve: (id: string) =>
    apiClient.post<LeaveRequest>(`${HRM_BASE}/leave-requests/${id}/approve`).then((r) => r.data),

  reject: (id: string, body: RejectLeaveRequestBody) =>
    apiClient.post<LeaveRequest>(`${HRM_BASE}/leave-requests/${id}/reject`, body).then((r) => r.data),

  cancel: (id: string) =>
    apiClient.post<LeaveRequest>(`${HRM_BASE}/leave-requests/${id}/cancel`).then((r) => r.data),
}
