import { apiClient, HRM_BASE, HrmPagedResult } from './hrmClient'

// Matches ERP.HRM.Domain.Enums.AttendanceStatus (int on the wire for filters,
// string in AttendanceDto responses).
export const ATTENDANCE_STATUS = {
  Present: 0,
  Absent: 1,
  Late: 2,
  HalfDay: 3,
  OnLeave: 4,
  Holiday: 5,
} as const

export interface Attendance {
  id: string
  employeeId: string
  date: string
  checkInTime?: string | null
  checkOutTime?: string | null
  totalHours: number
  overtimeHours: number
  status: string
  workShiftId?: string | null
  notes?: string | null
  createdAt: string
}

export interface AttendanceSummary {
  employeeId: string
  year: number
  month: number
  presentDays: number
  absentDays: number
  lateDays: number
  leaveDays: number
  totalHours: number
  totalOvertimeHours: number
}

export interface ListAttendanceParams {
  employeeId?: string
  fromDate?: string
  toDate?: string
  status?: number
  page?: number
  pageSize?: number
}

// CheckInCommand(EmployeeId, CheckInTime, WorkShiftId, Notes)
export interface CheckInRequest {
  employeeId: string
  checkInTime: string
  workShiftId?: string | null
  notes?: string | null
}

// AttendanceController.CheckOutRequest(CheckOutTime, Notes)
export interface CheckOutRequestBody {
  checkOutTime: string
  notes?: string | null
}

// MarkAbsentCommand(EmployeeId, Date, Notes)
export interface MarkAbsentRequest {
  employeeId: string
  date: string
  notes?: string | null
}

export const attendanceApi = {
  list: (params: ListAttendanceParams) =>
    apiClient
      .get<HrmPagedResult<Attendance>>(`${HRM_BASE}/attendance`, { params })
      .then((r) => r.data),

  get: (id: string) => apiClient.get<Attendance>(`${HRM_BASE}/attendance/${id}`).then((r) => r.data),

  getByDate: (employeeId: string, date: string) =>
    apiClient
      .get<Attendance>(`${HRM_BASE}/attendance/employee/${employeeId}/date/${date}`)
      .then((r) => r.data),

  getSummary: (employeeId: string, year: number, month: number) =>
    apiClient
      .get<AttendanceSummary>(`${HRM_BASE}/attendance/employee/${employeeId}/summary`, {
        params: { year, month },
      })
      .then((r) => r.data),

  checkIn: (body: CheckInRequest) =>
    apiClient.post<Attendance>(`${HRM_BASE}/attendance/check-in`, body).then((r) => r.data),

  checkOut: (id: string, body: CheckOutRequestBody) =>
    apiClient.post<Attendance>(`${HRM_BASE}/attendance/${id}/check-out`, body).then((r) => r.data),

  markAbsent: (body: MarkAbsentRequest) =>
    apiClient.post<Attendance>(`${HRM_BASE}/attendance/mark-absent`, body).then((r) => r.data),
}
