import { gatewayClient } from '@/api/client'
import type { Page } from '@/modules/usePagedList'
import { PAGE_SIZE } from '@/modules/usePagedList'

// HRMService is mounted at the Gateway's /hrm prefix (admin/src/api/hrm/*). Its paged answers are { items, totalCount, page, pageSize }.
const BASE = '/hrm/api/v1/hrm'

interface HrmPage<T> {
  items: T[]
  totalCount: number
}

const toPage = <T>(d: HrmPage<T>): Page<T> => ({ items: d.items, total: d.totalCount })

/** Server filter values (ERP.HRM.Domain.Enums.LeaveStatus / EmploymentStatus). */
export const LEAVE_STATUS = { Pending: 0, Approved: 1, Rejected: 2, Cancelled: 3 } as const
export const EMPLOYMENT_STATUS = { Active: 0, OnLeave: 1, Terminated: 2 } as const

export interface Employee {
  id: string
  employeeNumber: string
  fullName: string
  email?: string | null
  employmentStatus: string
  departmentId: string
}

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
}

export interface Attendance {
  id: string
  employeeId: string
  date: string
  checkInTime?: string | null
  checkOutTime?: string | null
  totalHours: number
  status: string
}

export const hrmApi = {
  employees: (search: string, page: number, pageSize = PAGE_SIZE) =>
    gatewayClient.get<HrmPage<Employee>>(`${BASE}/employees`, { params: { search: search || undefined, page, pageSize } }).then((r) => toPage(r.data)),
  employee: (id: string) => gatewayClient.get<Employee>(`${BASE}/employees/${id}`).then((r) => r.data),

  leaves: (status: number | undefined, page: number, pageSize = PAGE_SIZE) =>
    gatewayClient.get<HrmPage<LeaveRequest>>(`${BASE}/leave-requests`, { params: { status, page, pageSize } }).then((r) => toPage(r.data)),
  leave: (id: string) => gatewayClient.get<LeaveRequest>(`${BASE}/leave-requests/${id}`).then((r) => r.data),
  pendingLeaveCount: () => hrmApi.leaves(LEAVE_STATUS.Pending, 1, 1).then((p) => p.total),
  approveLeave: (id: string) => gatewayClient.post(`${BASE}/leave-requests/${id}/approve`).then((r) => r.data),
  rejectLeave: (id: string, reason: string) => gatewayClient.post(`${BASE}/leave-requests/${id}/reject`, { reason }).then((r) => r.data),
  cancelLeave: (id: string) => gatewayClient.post(`${BASE}/leave-requests/${id}/cancel`).then((r) => r.data),

  attendance: (page: number, pageSize = PAGE_SIZE) =>
    gatewayClient.get<HrmPage<Attendance>>(`${BASE}/attendance`, { params: { page, pageSize } }).then((r) => toPage(r.data)),
}

export type LeaveActionKey = 'approve' | 'reject' | 'cancel'

/** A pending request can be approved or rejected; an approved one cancelled. All need hrm:leave:manage (the server checks again). */
export function leaveActions(status: string, has: (permission: string) => boolean): LeaveActionKey[] {
  if (!has('hrm:leave:manage')) return []
  if (status === 'Pending') return ['approve', 'reject']
  if (status === 'Approved') return ['cancel']
  return []
}
