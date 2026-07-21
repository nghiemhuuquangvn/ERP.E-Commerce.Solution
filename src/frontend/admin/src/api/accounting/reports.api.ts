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

  profitAndLoss: (params: ProfitAndLossParams) =>
    apiClient
      .get<ProfitAndLossDto>(`${ACC_BASE}/accounting/reports/profit-and-loss`, { params })
      .then((r) => r.data),

  balanceSheet: (params: BalanceSheetParams) =>
    apiClient
      .get<BalanceSheetDto>(`${ACC_BASE}/accounting/reports/balance-sheet`, { params })
      .then((r) => r.data),
}
