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

// B01-DN / B02-DN by indicator code (Vietnamese chart, draft layout)
export interface StatementRow {
  code: string
  name: string
  indent: number
  isTotal: boolean
  amount: number
  priorAmount: number | null
}

export interface UnmappedAccount {
  accountNumber: string
  accountName: string
  balance: number
}

export interface FinancialStatement {
  layoutId: string
  title: string
  isDraft: boolean
  currency: string
  fromDate: string | null
  toDate: string | null
  compareFromDate: string | null
  compareToDate: string | null
  rows: StatementRow[]
  unmapped: UnmappedAccount[]
  notes: string[]
  isBalanced: boolean | null
  /** "VND", "thousand VND" or "million VND": the unit of every amount in rows (unmapped accounts stay in VND). */
  unit: string
  unitDivisor: number
  header: ReportHeader | null
}

export interface ReportHeader {
  companyName: string
  taxCode: string
  address: string
  preparedBy: string
  chiefAccountant: string
  director: string
}

export type AmountUnit = 'Vnd' | 'Thousand' | 'Million'

// ReportProfileDto — the company header, signatories and default unit of the statements.
export interface ReportProfile extends ReportHeader {
  amountUnit: AmountUnit
}

export interface BalanceSheetStatementParams {
  asOfDate?: string
  compareAsOfDate?: string
  unit?: AmountUnit
}

export interface IncomeStatementParams {
  fromDate: string
  toDate: string
  compareFromDate?: string
  compareToDate?: string
  unit?: AmountUnit
  /** B03 only: 'direct' (default) or 'indirect' (starts from profit before tax). */
  method?: 'direct' | 'indirect'
}

/** Downloads a report as a file: CSV (UTF-8 with a BOM, Excel opens it correctly), Excel (.xlsx with numeric cells) or PDF (B01 / B02 / B03 only). */
export async function downloadReport(
  path: string,
  params: Record<string, unknown>,
  fallbackName: string,
  format: 'csv' | 'xlsx' | 'pdf' = 'csv',
): Promise<void> {
  const res = await apiClient.get<Blob>(`${ACC_BASE}/accounting/reports/${path}`, {
    params: { ...params, format },
    responseType: 'blob',
  })
  const header = String(res.headers['content-disposition'] ?? '')
  const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header)?.[1] ?? fallbackName
  const url = URL.createObjectURL(res.data)
  const link = document.createElement('a')
  link.href = url
  link.download = decodeURIComponent(name)
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export const downloadReportCsv = (path: string, params: Record<string, unknown>, fallbackName: string) =>
  downloadReport(path, params, fallbackName, 'csv')

// NotesDto — the notes B09-DN: the accountant's texts and tables computed from the books.
export interface NoteTable {
  columns: string[]
  rows: { label: string; cells: number[]; isTotal: boolean }[]
}

export interface NoteSection {
  number: string
  title: string
  key: string | null
  text: string | null
  hint: string | null
  isGenerated: boolean
  table: NoteTable | null
}

export type NoteLanguage = 'vi' | 'en' | 'ja' | 'zh' | 'fr'

/** One change of a note text: who saved it, when, and the text it got (empty = cleared). */
export interface NoteTextRevision {
  changedAt: string
  changedBy: string | null
  text: string
}

export interface Notes {
  language: NoteLanguage
  fiscalYear: number
  compareFromDate: string | null
  compareToDate: string | null
  title: string
  isDraft: boolean
  fromDate: string
  toDate: string
  unit: string
  header: ReportHeader | null
  sections: NoteSection[]
  notes: string[]
}

export interface NoteTextEntry {
  key: string
  number: string
  title: string
  hint: string
  text: string
  /** The fiscal year asked for (0 = the texts for every year) and the year the shown text was written for. */
  fiscalYear: number
  sourceYear: number
  inherited: boolean
}

export const reportsApi = {
  notes: (params: { fromDate: string; toDate: string; unit?: AmountUnit; lang?: NoteLanguage; compareFromDate?: string; compareToDate?: string }) =>
    apiClient.get<Notes>(`${ACC_BASE}/accounting/reports/statements/notes`, { params }).then((r) => r.data),

  noteTexts: (lang: NoteLanguage = 'vi', fiscalYear = 0) =>
    apiClient.get<NoteTextEntry[]>(`${ACC_BASE}/accounting/reports/notes/text`, { params: { lang, fiscalYear } }).then((r) => r.data),

  noteTextHistory: (key: string, lang: NoteLanguage = 'vi', fiscalYear = 0) =>
    apiClient.get<NoteTextRevision[]>(`${ACC_BASE}/accounting/reports/notes/text/history`, { params: { key, lang, fiscalYear } }).then((r) => r.data),

  updateNoteTexts: (texts: Record<string, string>, language: NoteLanguage = 'vi', fiscalYear = 0) =>
    apiClient.put<NoteTextEntry[]>(`${ACC_BASE}/accounting/reports/notes/text`, { texts, language, fiscalYear }).then((r) => r.data),

  reportProfile: () => apiClient.get<ReportProfile>(`${ACC_BASE}/accounting/reports/profile`).then((r) => r.data),

  updateReportProfile: (body: ReportProfile) =>
    apiClient.put<ReportProfile>(`${ACC_BASE}/accounting/reports/profile`, body).then((r) => r.data),

  // B03-DN, direct method, by indicator code.
  cashFlowStatement: (params: IncomeStatementParams) =>
    apiClient
      .get<FinancialStatement>(`${ACC_BASE}/accounting/reports/statements/cash-flow`, { params })
      .then((r) => r.data),

  balanceSheetStatement: (params: BalanceSheetStatementParams) =>
    apiClient
      .get<FinancialStatement>(`${ACC_BASE}/accounting/reports/statements/balance-sheet`, { params })
      .then((r) => r.data),

  incomeStatement: (params: IncomeStatementParams) =>
    apiClient
      .get<FinancialStatement>(`${ACC_BASE}/accounting/reports/statements/income-statement`, { params })
      .then((r) => r.data),

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
