import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

// Matches ERP.MST.Domain.Enums.EmploymentStatus (int on the wire for filters,
// string in EmployeeDto responses).
export const EMPLOYMENT_STATUS = {
  Active: 0,
  OnLeave: 1,
  Terminated: 2,
} as const

// Matches ERP.MST.Domain.Enums.EmploymentType (int accepted by HireEmployeeCommand).
export const EMPLOYMENT_TYPE = {
  FullTime: 0,
  PartTime: 1,
  Contract: 2,
} as const

export interface Employee {
  id: string
  employeeNumber: string
  firstName: string
  lastName: string
  fullName: string
  email?: string | null
  phone?: string | null
  dateOfHire: string
  departmentId: string
  positionId?: string | null
  employmentStatus: string
  employmentType: string
  createdAt: string
  updatedAt: string
}

export interface ListEmployeesParams {
  departmentId?: string
  status?: number
  page?: number
  pageSize?: number
}

// HireEmployeeCommand(FirstName, LastName, Email, Phone, DepartmentId, DateOfHire, EmploymentType, PositionId)
export interface HireEmployeeRequest {
  firstName: string
  lastName: string
  email?: string | null
  phone?: string | null
  departmentId: string
  dateOfHire: string
  employmentType: number
  positionId?: string | null
}

// TerminateRequest(Reason)
export interface TerminateEmployeeRequest {
  reason: string
}

// TransferRequest(NewDepartmentId)
export interface TransferEmployeeRequest {
  newDepartmentId: string
}

// UpdateProfileRequest(Email, Phone)
export interface UpdateEmployeeProfileRequest {
  email?: string | null
  phone?: string | null
}

export const employeesApi = {
  list: (params: ListEmployeesParams) =>
    apiClient
      .get<MstPagedResult<Employee>>(`${MST_BASE}/employees`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Employee>(`${MST_BASE}/employees/${id}`).then((r) => r.data),

  hire: (body: HireEmployeeRequest) =>
    apiClient.post<Employee>(`${MST_BASE}/employees`, body).then((r) => r.data),

  terminate: (id: string, body: TerminateEmployeeRequest) =>
    apiClient.post<Employee>(`${MST_BASE}/employees/${id}/terminate`, body).then((r) => r.data),

  transfer: (id: string, body: TransferEmployeeRequest) =>
    apiClient.post<Employee>(`${MST_BASE}/employees/${id}/transfer`, body).then((r) => r.data),

  updateProfile: (id: string, body: UpdateEmployeeProfileRequest) =>
    apiClient.put<Employee>(`${MST_BASE}/employees/${id}/profile`, body).then((r) => r.data),
}
