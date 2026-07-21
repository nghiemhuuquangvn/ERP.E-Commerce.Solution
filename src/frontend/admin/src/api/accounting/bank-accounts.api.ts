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

  reconcileStatement: (bankAccountId: string, statementId: string, body?: ReconcileStatementRequest) =>
    apiClient
      .post<BankStatement>(
        `${ACC_BASE}/bank-accounts/${bankAccountId}/statements/${statementId}/reconcile`,
        body ?? {},
      )
      .then((r) => r.data),
}
