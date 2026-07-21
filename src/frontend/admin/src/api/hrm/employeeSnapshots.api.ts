import { apiClient, HRM_BASE, HrmPagedResult } from './hrmClient'

// Matches ERP.HRM.Domain.Enums.EmploymentStatus (int on the wire for filters,
// string in EmployeeSnapshotDto responses).
export const EMPLOYMENT_STATUS = {
  Active: 0,
  OnLeave: 1,
  Terminated: 2,
} as const

// Read-only view of HRM's local EmployeeSnapshot projection — Employee lifecycle
// (hire/terminate/transfer) is owned by SharedMasterDataService's employees.api.ts.
// This is what HRM's own pages (attendance/leave/payroll) use for their employee
// pickers, matching EmployeesController's documented purpose.
export interface EmployeeSnapshot {
  id: string
  employeeNumber: string
  fullName: string
  email?: string | null
  employmentStatus: string
  departmentId: string
}

export interface ListEmployeeSnapshotsParams {
  search?: string
  departmentId?: string
  employmentStatus?: number
  page?: number
  pageSize?: number
}

export const employeeSnapshotsApi = {
  list: (params: ListEmployeeSnapshotsParams) =>
    apiClient
      .get<HrmPagedResult<EmployeeSnapshot>>(`${HRM_BASE}/employees`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<EmployeeSnapshot>(`${HRM_BASE}/employees/${id}`).then((r) => r.data),
}
