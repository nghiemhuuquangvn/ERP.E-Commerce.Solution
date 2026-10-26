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
  // Calculation breakdown (ROADMAP P1-7); all 0 on a slip entered by hand.
  baseSalary: number
  allowances: number
  workedDays: number
  standardDays: number
  employeeSocial: number
  employeeHealth: number
  employeeUnemployment: number
  employerSocial: number
  employerHealth: number
  employerUnemployment: number
  taxableIncome: number
  personalIncomeTax: number
  dependentCount: number
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

export interface SkippedEmployee {
  employeeId: string
  employeeNumber: string
  fullName: string
  reason: string
}

// ProcessPayrollResultDto(Created, Skipped)
export interface ProcessPayrollResult {
  created: PaySlip[]
  skipped: SkippedEmployee[]
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
    apiClient.post<ProcessPayrollResult>(`${HRM_BASE}/payroll/process`, body).then((r) => r.data),

  payPaySlip: (id: string, body: { paidOn?: string; paymentMethod?: string }) =>
    apiClient.post<PaySlip>(`${HRM_BASE}/payslips/${id}/pay`, body).then((r) => r.data),

  cancelPaySlip: (id: string, reason: string) =>
    apiClient.post<PaySlip>(`${HRM_BASE}/payslips/${id}/cancel`, { reason }).then((r) => r.data),
}

/** The server's refusal text (BusinessRuleException -> ProblemDetails.detail), or the fallback. */
export function serverMessage(e: unknown, fallback: string): string {
  const data = (e as { response?: { data?: { detail?: string; message?: string } } } | null)?.response?.data
  return data?.detail ?? data?.message ?? fallback
}
