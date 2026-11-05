import { gatewayClient } from '@/api/client'
import type { Page } from '@/modules/usePagedList'
import { onePage, PAGE_SIZE } from '@/modules/usePagedList'
import { periodMonths } from '@/lib/periods'
import { parseISO } from 'date-fns'

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
/** ERP.HRM.Domain.Enums.AttendanceStatus. */
export const ATTENDANCE_STATUS = { Present: 0, Absent: 1, Late: 2, HalfDay: 3, OnLeave: 4, Holiday: 5 } as const

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
  /** `employmentStatus` is the EMPLOYMENT_STATUS number. */
  employees: (search: string, page: number, pageSize = PAGE_SIZE, employmentStatus?: number) =>
    gatewayClient.get<HrmPage<Employee>>(`${BASE}/employees`, { params: { search: search || undefined, employmentStatus, page, pageSize } }).then((r) => toPage(r.data)),
  employee: (id: string) => gatewayClient.get<Employee>(`${BASE}/employees/${id}`).then((r) => r.data),

  /** `status` is the LEAVE_STATUS number; a request counts for a day range when its leave period touches it. */
  leaves: (params: { status?: number; dateFrom?: string; dateTo?: string; search?: string }, page: number, pageSize = PAGE_SIZE) =>
    gatewayClient.get<HrmPage<LeaveRequest>>(`${BASE}/leave-requests`, { params: { ...params, page, pageSize } }).then((r) => toPage(r.data)),
  leave: (id: string) => gatewayClient.get<LeaveRequest>(`${BASE}/leave-requests/${id}`).then((r) => r.data),
  pendingLeaveCount: () => hrmApi.leaves({ status: LEAVE_STATUS.Pending }, 1, 1).then((p) => p.total),
  approveLeave: (id: string) => gatewayClient.post(`${BASE}/leave-requests/${id}/approve`).then((r) => r.data),
  rejectLeave: (id: string, reason: string) => gatewayClient.post(`${BASE}/leave-requests/${id}/reject`, { reason }).then((r) => r.data),
  cancelLeave: (id: string) => gatewayClient.post(`${BASE}/leave-requests/${id}/cancel`).then((r) => r.data),

  /** The attendance endpoint names its range fromDate / toDate and takes the status as a number. */
  attendance: (params: { dateFrom?: string; dateTo?: string; status?: number }, page: number, pageSize = PAGE_SIZE) =>
    gatewayClient
      .get<HrmPage<Attendance>>(`${BASE}/attendance`, { params: { fromDate: params.dateFrom, toDate: params.dateTo, status: params.status, page, pageSize } })
      .then((r) => toPage(r.data)),
}

export type LeaveActionKey = 'approve' | 'reject' | 'cancel'

/** A pending request can be approved or rejected; an approved one cancelled. All need hrm:leave:manage (the server checks again). */
export function leaveActions(status: string, has: (permission: string) => boolean): LeaveActionKey[] {
  if (!has('hrm:leave:manage')) return []
  if (status === 'Pending') return ['approve', 'reject']
  if (status === 'Approved') return ['cancel']
  return []
}

export interface PaySlip {
  id: string
  paySlipNumber: string
  employeeId: string
  year: number
  month: number
  payPeriod?: string
  grossSalary: number
  deductions: number
  overtimeHours?: number
  overtimePay?: number
  netSalary: number
  paymentDate?: string
  paymentStatus: string
  // Calculation breakdown (all 0 on a slip entered by hand).
  baseSalary?: number
  allowances?: number
  workedDays?: number
  standardDays?: number
  employeeSocial?: number
  employeeHealth?: number
  employeeUnemployment?: number
  employerSocial?: number
  employerHealth?: number
  employerUnemployment?: number
  taxableIncome?: number
  personalIncomeTax?: number
  dependentCount?: number
}

export interface WorkShift {
  id: string
  name: string
  startTime: string
  endTime: string
  shiftHours: number
  isActive: boolean
}

/** Most months one pay slip search spans (a custom range wider than this is cut to its last months). */
export const MAX_PAYSLIP_MONTHS = 24

export const hrmExtrasApi = {
  /**
   * Pay slips. Without a range the server pages them all; with one it is read month by month (the endpoint filters by one year and
   * month; at most MAX_PAYSLIP_MONTHS calls, in parallel) and put together newest first.
   */
  async paySlips(params: { dateFrom?: string; dateTo?: string }, page = 1, pageSize = PAGE_SIZE): Promise<Page<PaySlip>> {
    if (!params.dateFrom || !params.dateTo) {
      const { data } = await gatewayClient.get<HrmPage<PaySlip>>(BASE + '/payslips', { params: { page, pageSize } })
      return toPage(data)
    }
    const from = parseISO(params.dateFrom)
    const to = parseISO(params.dateTo)
    const wanted: { year: number; month: number }[] = []
    for (let d = new Date(from.getFullYear(), from.getMonth(), 1); d <= to; d = new Date(d.getFullYear(), d.getMonth() + 1, 1)) wanted.push({ year: d.getFullYear(), month: d.getMonth() + 1 })
    const pages = await Promise.all(
      wanted.slice(-MAX_PAYSLIP_MONTHS).map((m) => gatewayClient.get<HrmPage<PaySlip>>(BASE + '/payslips', { params: { year: m.year, month: m.month, page: 1, pageSize: 100 } }).then((r) => r.data.items)),
    )
    return onePage(pages.flat().sort((a, b) => b.year - a.year || b.month - a.month || a.paySlipNumber.localeCompare(b.paySlipNumber)))
  },
  paySlip: (id: string) => gatewayClient.get<PaySlip>(BASE + '/payslips/' + id).then((r) => r.data),
  payPaySlip: (id: string, paymentMethod?: string) => gatewayClient.post(BASE + '/payslips/' + id + '/pay', { paymentMethod: paymentMethod || undefined }).then((r) => r.data),
  cancelPaySlip: (id: string, reason: string) => gatewayClient.post(BASE + '/payslips/' + id + '/cancel', { reason }).then((r) => r.data),
  /** The list is short and answers a bare array. */
  shifts: () => gatewayClient.get<WorkShift[]>(BASE + '/work-shifts').then((r) => onePage(r.data)),
}

export type PaySlipActionKey = 'pay' | 'cancel'

/** A pending pay slip can be paid or cancelled; both need hrm:payroll:manage (the server checks again). */
export function paySlipActions(status: string, has: (permission: string) => boolean): PaySlipActionKey[] {
  return status === 'Pending' && has('hrm:payroll:manage') ? ['pay', 'cancel'] : []
}
