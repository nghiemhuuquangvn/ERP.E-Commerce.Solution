import { apiClient, HRM_BASE, HrmPagedResult } from './hrmClient'

// Matches ERP.HRM.Domain.Enums.PaymentStatus (string in PaySlipDto responses).
export const PAYMENT_STATUS = {
  Pending: 0,
  Paid: 1,
  Cancelled: 2,
} as const

export interface PaySlip {
  id: string
  paySlipNumber: string
  employeeId: string
  year: number
  month: number
  payPeriod: string
  grossSalary: number
  deductions: number
  overtimeHours: number
  overtimePay: number
  netSalary: number
  paymentDate: string
  paymentStatus: string
  createdAt: string
}

export interface ListPaySlipsParams {
  employeeId?: string
  year?: number
  month?: number
  page?: number
  pageSize?: number
}

// PayrollController.ProcessPayrollRequest(Year, Month)
export interface ProcessPayrollRequest {
  year: number
  month: number
}

// GeneratePaySlipCommand(EmployeeId, Year, Month, GrossPay, Deductions, OvertimeHours, OvertimePay)
export interface GeneratePaySlipRequest {
  employeeId: string
  year: number
  month: number
  grossPay: number
  deductions: number
  overtimeHours?: number
  overtimePay?: number
}

export const payrollApi = {
  listPaySlips: (params: ListPaySlipsParams) =>
    apiClient
      .get<HrmPagedResult<PaySlip>>(`${HRM_BASE}/payslips`, { params })
      .then((r) => r.data),

  getPaySlip: (id: string) => apiClient.get<PaySlip>(`${HRM_BASE}/payslips/${id}`).then((r) => r.data),

  generatePaySlip: (body: GeneratePaySlipRequest) =>
    apiClient.post<PaySlip>(`${HRM_BASE}/payroll/generate`, body).then((r) => r.data),

  processPayroll: (body: ProcessPayrollRequest) =>
    apiClient.post<PaySlip[]>(`${HRM_BASE}/payroll/process`, body).then((r) => r.data),
}
