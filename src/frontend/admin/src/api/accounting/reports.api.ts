import { apiClient, ACC_BASE } from './accClient'

// NOTE: ReportsController is routed at "api/v1/accounting/reports" (not
// "api/v1/reports"), so paths below are rooted at `${ACC_BASE}/accounting/reports`.

export interface TrialBalanceLineDto {
  accountId: string
  accountNumber: string
  accountName: string
  accountType: string
  normalBalance: string
  totalDebit: number
  totalCredit: number
  balance: number
  // Vietnamese trial balance (bảng cân đối số phát sinh)
  level: number
  parentAccountId: string | null
  isParent: boolean
  openingDebit: number
  openingCredit: number
  periodDebit: number
  periodCredit: number
  closingDebit: number
  closingCredit: number
}

export interface TrialBalanceTotals {
  openingDebit: number
  openingCredit: number
  periodDebit: number
  periodCredit: number
  closingDebit: number
  closingCredit: number
  isBalanced: boolean
}

export interface TrialBalanceFull {
  lines: TrialBalanceLineDto[]
  totals: TrialBalanceTotals
}

export interface LedgerLine {
  entryId: string
  entryNumber: string
  entryDate: string
  description: string
  accountNumber: string
  counterAccounts: string
  debit: number
  credit: number
  balance: number
  status: string
  isClosingEntry: boolean
}

export interface AccountLedger {
  accountId: string
  accountNumber: string
  accountName: string
  normalBalance: string
  includesSubAccounts: boolean
  openingBalance: number
  totalDebit: number
  totalCredit: number
  closingBalance: number
  totalRows: number
  pageNumber: number
  pageSize: number
  lines: LedgerLine[]
}

export interface CashFlowLine {
  accountId: string
  accountNumber: string
  accountName: string
  inflow: number
  outflow: number
  net: number
}

export interface CashFlowSection {
  category: string
  lines: CashFlowLine[]
  totalInflow: number
  totalOutflow: number
  net: number
}

export interface CashFlowStatement {
  fromDate: string
  toDate: string
  cashAccountCount: number
  openingCash: number
  operating: CashFlowSection
  investing: CashFlowSection
  financing: CashFlowSection
  netChange: number
  closingCash: number
  difference: number
  isReconciled: boolean
}

export interface FinancialAccountLineDto {
  accountId: string
  accountNumber: string
  accountName: string
  amount: number
}

export interface ProfitAndLossDto {
  fromDate: string
  toDate: string
  totalRevenue: number
  totalExpenses: number
  netIncome: number
  revenueLines: FinancialAccountLineDto[]
  expenseLines: FinancialAccountLineDto[]
}

export interface BalanceSheetDto {
  asOfDate: string
  totalAssets: number
  totalLiabilities: number
  totalEquity: number
  assetLines: FinancialAccountLineDto[]
  liabilityLines: FinancialAccountLineDto[]
  equityLines: FinancialAccountLineDto[]
}

export interface TrialBalanceParams {
  fiscalPeriodId?: string
  fromDate?: string
  toDate?: string
  hideZeroBalance?: boolean
  rollUp?: boolean
}

export interface AccountLedgerParams {
  accountId: string
  fromDate?: string
  toDate?: string
  includeSubAccounts?: boolean
  pageNumber?: number
  pageSize?: number
}

export interface ProfitAndLossParams {
  fromDate: string
  toDate: string
}

export interface BalanceSheetParams {
  asOfDate?: string
}

export const reportsApi = {
  trialBalance: (params: TrialBalanceParams) =>
    apiClient
      .get<TrialBalanceLineDto[]>(`${ACC_BASE}/accounting/reports/trial-balance`, { params })
      .then((r) => r.data),

  // Opening / period / closing columns with the grand totals and the balanced check.
  trialBalanceFull: (params: TrialBalanceParams) =>
    apiClient
      .get<TrialBalanceFull>(`${ACC_BASE}/accounting/reports/trial-balance/full`, { params })
      .then((r) => r.data),

  accountLedger: (params: AccountLedgerParams) =>
    apiClient
      .get<AccountLedger>(`${ACC_BASE}/accounting/reports/account-ledger`, { params })
      .then((r) => r.data),

  // Direct method, classified by the counter-account of each cash movement.
  cashFlow: (params: { fromDate: string; toDate: string }) =>
    apiClient
      .get<CashFlowStatement>(`${ACC_BASE}/accounting/reports/cash-flow`, { params })
      .then((r) => r.data),

  profitAndLoss: (params: ProfitAndLossParams) =>
    apiClient
      .get<ProfitAndLossDto>(`${ACC_BASE}/accounting/reports/profit-and-loss`, { params })
      .then((r) => r.data),

  balanceSheet: (params: BalanceSheetParams) =>
    apiClient
      .get<BalanceSheetDto>(`${ACC_BASE}/accounting/reports/balance-sheet`, { params })
      .then((r) => r.data),
}
