import { apiClient, ACC_BASE } from './accClient'

// LoanDto — a borrowing with its repayment schedule. The schedule gives B01 the real due dates (borrowings due after 12 months are long-term).
export interface LoanInstallment {
  id: string
  dueDate: string
  principalAmount: number
  interestAmount: number
  paidOn: string | null
  /** Interest of the installment already accrued to the books and not yet paid. */
  accruedInterest: number
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
  /** True once the drawdown is in the journal, so repayments are posted too; false for a loan booked by hand. */
  postedToLedger: boolean
  /** The asset account the money is received on and repaid from; null = the Bank key of the posting profile. */
  receivingAccountId: string | null
  receivingAccountNumber: string | null
  receivingAccountName: string | null
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
  /** Post the drawdown (Dr Bank / Cr Loan payable). False for a loan already booked by hand. */
  postDrawdown?: boolean
  receivingAccountId?: string
}

export interface LoanInterestAccrual {
  asOfDate: string
  totalAccrued: number
  lines: { loanId: string; loanNumber: string; installmentDueDate: string; amount: number; posted: boolean }[]
}

export interface LoanReconciliation {
  asOfDate: string
  accountNumber: string
  accountName: string
  interestAccountNumber: string
  interestLedger: number
  interestBook: number
  interestDifference: number
  interestReconciled: boolean
  ledgerTotal: number
  bookBalance: number
  difference: number
  isReconciled: boolean
  loans: { loanId: string; loanNumber: string; lender: string; postedToLedger: boolean; outstanding: number }[]
  notes: string[]
}

export const loansApi = {
  list: () => apiClient.get<Loan[]>(`${ACC_BASE}/accounting/loans`).then((r) => r.data),

  create: (body: CreateLoanRequest) => apiClient.post<Loan>(`${ACC_BASE}/accounting/loans`, body).then((r) => r.data),

  repay: (loanId: string, installmentId: string, paidOn?: string, interestPaid?: number) =>
    apiClient.post<Loan>(`${ACC_BASE}/accounting/loans/${loanId}/installments/${installmentId}/repay`, { paidOn, interestPaid }).then((r) => r.data),

  accrueInterest: (asOfDate: string) =>
    apiClient.post<LoanInterestAccrual>(`${ACC_BASE}/accounting/loans/accrue-interest`, null, { params: { asOfDate } }).then((r) => r.data),

  postDrawdown: (loanId: string, receivingAccountId?: string) =>
    apiClient.post<Loan>(`${ACC_BASE}/accounting/loans/${loanId}/post-drawdown`, { receivingAccountId }).then((r) => r.data),

  reconciliation: (asOfDate: string) =>
    apiClient.get<LoanReconciliation>(`${ACC_BASE}/accounting/reports/loans-reconciliation`, { params: { asOfDate } }).then((r) => r.data),

  cancel: (loanId: string, reason: string) =>
    apiClient.post<Loan>(`${ACC_BASE}/accounting/loans/${loanId}/cancel`, { reason }).then((r) => r.data),
}
