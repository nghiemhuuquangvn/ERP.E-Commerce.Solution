import { apiClient, ACC_BASE } from './accClient'

// LoanDto — a borrowing with its repayment schedule. The schedule gives B01 the real due dates (borrowings due after 12 months are long-term).
export interface LoanInstallment {
  id: string
  dueDate: string
  principalAmount: number
  interestAmount: number
  paidOn: string | null
}

export interface Loan {
  id: string
  loanNumber: string
  lender: string
  principal: number
  interestRatePercent: number
  startDate: string
  status: 'Active' | 'Repaid' | 'Cancelled'
  notes: string | null
  cancelReason: string | null
  outstanding: number
  dueWithinTwelveMonths: number
  dueLater: number
  installments: LoanInstallment[]
}

export interface CreateLoanRequest {
  loanNumber: string
  lender: string
  principal: number
  interestRatePercent: number
  startDate: string
  maturityDate?: string
  schedule: { dueDate: string; principalAmount: number; interestAmount: number }[]
  notes?: string
}

export const loansApi = {
  list: () => apiClient.get<Loan[]>(`${ACC_BASE}/accounting/loans`).then((r) => r.data),

  create: (body: CreateLoanRequest) => apiClient.post<Loan>(`${ACC_BASE}/accounting/loans`, body).then((r) => r.data),

  repay: (loanId: string, installmentId: string, paidOn?: string) =>
    apiClient.post<Loan>(`${ACC_BASE}/accounting/loans/${loanId}/installments/${installmentId}/repay`, { paidOn }).then((r) => r.data),

  cancel: (loanId: string, reason: string) =>
    apiClient.post<Loan>(`${ACC_BASE}/accounting/loans/${loanId}/cancel`, { reason }).then((r) => r.data),
}
