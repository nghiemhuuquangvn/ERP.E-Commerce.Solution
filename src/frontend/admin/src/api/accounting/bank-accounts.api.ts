import { apiClient, ACC_BASE } from './accClient'

// BankAccountDto(Id, AccountName, BankName, AccountNumber, CurrencyCode, CurrentBalance, IsActive, GlAccountId, CreatedAt)
export interface BankAccount {
  id: string
  accountName: string
  bankName: string
  accountNumber: string
  currencyCode: string
  currentBalance: number
  isActive: boolean
  glAccountId?: string | null
  createdAt: string
}

// BankStatementDto(Id, BankAccountId, StatementDate, OpeningBalance, ClosingBalance, Status, ReconciledAt, Notes, CreatedAt)
// Status is BankStatementStatus.ToString(): "Open" | "Reconciled"
export interface BankStatement {
  id: string
  bankAccountId: string
  statementDate: string
  openingBalance: number
  closingBalance: number
  status: string
  reconciledAt?: string | null
  notes?: string | null
  createdAt: string
}

export interface ListBankAccountsParams {
  activeOnly?: boolean
}

// CreateBankAccountCommand(AccountName, BankName, AccountNumber, OpeningBalance, CurrencyCode = "VND", GlAccountId)
export interface CreateBankAccountRequest {
  accountName: string
  bankName: string
  accountNumber: string
  openingBalance: number
  currencyCode: string
  glAccountId?: string | null
}

// BankAccountsController.CreateStatementRequest(StatementDate, OpeningBalance, ClosingBalance, Notes)
export interface CreateStatementRequest {
  statementDate: string
  openingBalance: number
  closingBalance: number
  notes?: string | null
}

// BankAccountsController.ReconcileRequest(Notes)
export interface ReconcileStatementRequest {
  notes?: string | null
}

export interface BankStatementLine {
  id: string
  bankStatementId: string
  lineDate: string
  description: string | null
  reference: string | null
  /** Signed: a deposit is positive, a withdrawal negative. */
  amount: number
  balance: number | null
  matchStatus: 'Unmatched' | 'Matched' | 'Posted'
  matchedJournalLineId: string | null
  journalEntryId: string | null
  matchedEntryNumber: string | null
}

export interface BankLedgerItem {
  lineId: string
  entryId: string
  entryNumber: string
  entryDate: string
  description: string
  referenceType: string | null
  amount: number
}

/** The statement against the books (ROADMAP P1-4). */
export interface BankReconciliation {
  bankStatementId: string
  bankAccountId: string
  statementDate: string
  status: string
  openingBalance: number
  closingBalance: number
  glAccountId: string | null
  glAccountNumber: string | null
  ledgerBalance: number
  outstandingLedgerNet: number
  unmatchedStatementNet: number
  expectedClosing: number
  difference: number
  unmatchedStatementLines: number
  canReconcile: boolean
  lines: BankStatementLine[]
  unmatchedLedger: BankLedgerItem[]
  notes: string[]
}

export const bankAccountsApi = {
  list: (params: ListBankAccountsParams) =>
    apiClient.get<BankAccount[]>(`${ACC_BASE}/bank-accounts`, { params }).then((r) => r.data),

  get: (id: string) =>
    apiClient.get<BankAccount>(`${ACC_BASE}/bank-accounts/${id}`).then((r) => r.data),

  create: (body: CreateBankAccountRequest) =>
    apiClient.post<BankAccount>(`${ACC_BASE}/bank-accounts`, body).then((r) => r.data),

  listStatements: (bankAccountId: string) =>
    apiClient
      .get<BankStatement[]>(`${ACC_BASE}/bank-accounts/${bankAccountId}/statements`)
      .then((r) => r.data),

  createStatement: (bankAccountId: string, body: CreateStatementRequest) =>
    apiClient
      .post<BankStatement>(`${ACC_BASE}/bank-accounts/${bankAccountId}/statements`, body)
      .then((r) => r.data),

  reconciliation: (bankAccountId: string, statementId: string) =>
    apiClient.get<BankReconciliation>(`${ACC_BASE}/bank-accounts/${bankAccountId}/statements/${statementId}/reconciliation`).then((r) => r.data),

  importLines: (bankAccountId: string, statementId: string, csv: string) =>
    apiClient.post<{ imported: number }>(`${ACC_BASE}/bank-accounts/${bankAccountId}/statements/${statementId}/lines/import`, { csv }).then((r) => r.data),

  autoMatch: (bankAccountId: string, statementId: string, toleranceDays?: number) =>
    apiClient.post<BankReconciliation>(`${ACC_BASE}/bank-accounts/${bankAccountId}/statements/${statementId}/auto-match`, null, { params: { toleranceDays } }).then((r) => r.data),

  matchLine: (bankAccountId: string, statementId: string, lineId: string, journalLineId: string) =>
    apiClient.post<BankReconciliation>(`${ACC_BASE}/bank-accounts/${bankAccountId}/statements/${statementId}/lines/${lineId}/match`, { journalLineId }).then((r) => r.data),

  unmatchLine: (bankAccountId: string, statementId: string, lineId: string) =>
    apiClient.post<BankReconciliation>(`${ACC_BASE}/bank-accounts/${bankAccountId}/statements/${statementId}/lines/${lineId}/unmatch`).then((r) => r.data),

  postLine: (bankAccountId: string, statementId: string, lineId: string, body?: { postingKey?: string; accountId?: string; description?: string }) =>
    apiClient.post<BankReconciliation>(`${ACC_BASE}/bank-accounts/${bankAccountId}/statements/${statementId}/lines/${lineId}/post`, body ?? {}).then((r) => r.data),

  removeLine: (bankAccountId: string, statementId: string, lineId: string) =>
    apiClient.delete(`${ACC_BASE}/bank-accounts/${bankAccountId}/statements/${statementId}/lines/${lineId}`).then(() => undefined),

  reconcileStatement: (bankAccountId: string, statementId: string, body?: ReconcileStatementRequest) =>
    apiClient
      .post<BankStatement>(
        `${ACC_BASE}/bank-accounts/${bankAccountId}/statements/${statementId}/reconcile`,
        body ?? {},
      )
      .then((r) => r.data),
}
