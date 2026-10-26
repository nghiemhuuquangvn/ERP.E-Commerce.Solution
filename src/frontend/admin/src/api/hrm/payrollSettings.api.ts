import { apiClient, HRM_BASE, HrmPagedResult } from './hrmClient'

// PayrollSettingsContracts.cs (ROADMAP P1-7)

export interface Compensation {
  id: string
  employeeId: string
  effectiveFrom: string
  baseSalary: number
  taxableAllowance: number
  exemptAllowance: number
  insuranceSalary: number | null
  insuranceEnabled: boolean
  taxCode: string | null
  insuranceNumber: string | null
}

export interface SalaryProfile {
  employeeId: string
  employeeNumber: string
  fullName: string
  dependentCount: number
  current: Compensation | null
}

export interface SaveCompensationRequest {
  effectiveFrom: string
  baseSalary: number
  taxableAllowance: number
  exemptAllowance: number
  insuranceSalary: number | null
  insuranceEnabled: boolean
  taxCode: string | null
  insuranceNumber: string | null
}

export interface Dependent {
  id: string
  employeeId: string
  fullName: string
  relationship: string
  taxCode: string | null
  fromMonth: string
  toMonth: string | null
}

export interface AddDependentRequest {
  fullName: string
  relationship: string
  taxCode: string | null
  fromMonth: string
  toMonth: string | null
}

export interface PitBracket {
  upTo: number | null
  rate: number
}

export interface PayrollRateSet {
  id: string
  effectiveFrom: string
  name: string
  socialEmployeeRate: number
  healthEmployeeRate: number
  unemploymentEmployeeRate: number
  socialEmployerRate: number
  healthEmployerRate: number
  unemploymentEmployerRate: number
  personalDeduction: number
  dependentDeduction: number
  insuranceSalaryCap: number
  unemploymentSalaryCap: number
  annualLeaveDays: number
  isConfirmed: boolean
  brackets: PitBracket[]
}

export type SaveRateSetRequest = Omit<PayrollRateSet, 'id'> & { id: string | null }

export interface LeaveBalance {
  employeeId: string
  year: number
  leaveType: string
  totalDays: number
  usedDays: number
  remainingDays: number
}

export const payrollSettingsApi = {
  salaryProfiles: (params: { search?: string; page?: number; pageSize?: number }) =>
    apiClient.get<HrmPagedResult<SalaryProfile>>(`${HRM_BASE}/salary-profiles`, { params }).then((r) => r.data),

  compensations: (employeeId: string) =>
    apiClient.get<Compensation[]>(`${HRM_BASE}/employees/${employeeId}/compensations`).then((r) => r.data),

  saveCompensation: (employeeId: string, body: SaveCompensationRequest) =>
    apiClient.put<Compensation>(`${HRM_BASE}/employees/${employeeId}/compensations`, body).then((r) => r.data),

  dependents: (employeeId: string) =>
    apiClient.get<Dependent[]>(`${HRM_BASE}/employees/${employeeId}/dependents`).then((r) => r.data),

  addDependent: (employeeId: string, body: AddDependentRequest) =>
    apiClient.post<Dependent>(`${HRM_BASE}/employees/${employeeId}/dependents`, body).then((r) => r.data),

  removeDependent: (id: string) => apiClient.delete(`${HRM_BASE}/dependents/${id}`).then(() => undefined),

  rateSets: () => apiClient.get<PayrollRateSet[]>(`${HRM_BASE}/payroll-settings/rate-sets`).then((r) => r.data),

  saveRateSet: (body: SaveRateSetRequest) =>
    apiClient.put<PayrollRateSet>(`${HRM_BASE}/payroll-settings/rate-sets`, body).then((r) => r.data),

  leaveBalances: (employeeId: string, year: number) =>
    apiClient
      .get<LeaveBalance[]>(`${HRM_BASE}/leave-requests/balances`, { params: { employeeId, year } })
      .then((r) => r.data),
}
