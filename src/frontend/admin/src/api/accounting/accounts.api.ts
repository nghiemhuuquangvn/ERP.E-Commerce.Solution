import { apiClient, ACC_BASE } from './accClient'

// Mirrors ERP.ACC.Domain.Enums.AccountType (src/services/AccountingService/src/ERP.ACC.Domain/Enums/AccountingEnums.cs)
export type AccountType = 'Asset' | 'Liability' | 'Equity' | 'Revenue' | 'Expense'

// Mirrors ERP.ACC.Domain.Enums.NormalBalance
export type NormalBalance = 'Debit' | 'Credit'

/**
 * CreateAccountCommand sends AccountType/NormalBalance as raw ints (enum numeric
 * value); AccountDto returns them back as strings (enum member name). These option
 * lists supply the numeric values for the create form's <select>s.
 */
export const ACCOUNT_TYPE_OPTIONS: { value: number; label: AccountType }[] = [
  { value: 1, label: 'Asset' },
  { value: 2, label: 'Liability' },
  { value: 3, label: 'Equity' },
  { value: 4, label: 'Revenue' },
  { value: 5, label: 'Expense' },
]

export const NORMAL_BALANCE_OPTIONS: { value: number; label: NormalBalance }[] = [
  { value: 1, label: 'Debit' },
  { value: 2, label: 'Credit' },
]

// AccountDto. A chart is a tree: parentAccountId / level; a parent groups sub-accounts and takes no postings (isPostable = false).
export interface Account {
  id: string
  accountNumber: string
  name: string
  accountType: AccountType
  normalBalance: NormalBalance
  createdAt: string
  isActive: boolean
  parentAccountId: string | null
  level: number
  isControl: boolean
  /** First digit of the number: the class of the Vietnamese chart (0 = number does not start with a digit). */
  accountClass: number
  isPostable: boolean
  /** Cash or bank account: tracked by the cash-flow statement. */
  isCash: boolean
  /** How a cash movement against this account is classified (the default applies when none was set). */
  cashFlowCategory: 'Operating' | 'Investing' | 'Financing'
}

export const CASH_FLOW_CATEGORY_OPTIONS: { value: number; label: 'Operating' | 'Investing' | 'Financing' }[] = [
  { value: 1, label: 'Operating' },
  { value: 2, label: 'Investing' },
  { value: 3, label: 'Financing' },
]

// CreateAccountCommand(AccountNumber, Name, AccountType:int, NormalBalance:int, ParentAccountId?)
export interface CreateAccountRequest {
  accountNumber: string
  name: string
  accountType: number
  normalBalance: number
  parentAccountId?: string | null
  isCash?: boolean
  cashFlowCategory?: number | null
}

// UpdateAccountCommand: renames and/or moves under another parent (null = top level)
export interface UpdateAccountRequest {
  name: string
  parentAccountId: string | null
  /** omitted = unchanged */
  isCash?: boolean
  /** omitted = unchanged; 0 = back to the default; 1 operating, 2 investing, 3 financing */
  cashFlowCategory?: number
}

// GET accounting/chart/templates
export interface ChartTemplate {
  id: string
  name: string
  description: string
  accountCount: number
}

export interface ApplyChartTemplateResult {
  templateId: string
  accountsCreated: number
  postingKeysMapped: number
}

export const accountsApi = {
  // ListAccountsQuery takes no filters; controller returns a plain List<AccountDto> (no pagination wrapper), ordered by number.
  list: () => apiClient.get<Account[]>(`${ACC_BASE}/accounts`).then((r) => r.data),

  get: (id: string) => apiClient.get<Account>(`${ACC_BASE}/accounts/${id}`).then((r) => r.data),

  create: (body: CreateAccountRequest) =>
    apiClient.post<Account>(`${ACC_BASE}/accounts`, body).then((r) => r.data),

  update: (id: string, body: UpdateAccountRequest) =>
    apiClient.put<Account>(`${ACC_BASE}/accounts/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<Account>(`${ACC_BASE}/accounts/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<Account>(`${ACC_BASE}/accounts/${id}/deactivate`).then((r) => r.data),

  templates: () =>
    apiClient.get<ChartTemplate[]>(`${ACC_BASE}/accounting/chart/templates`).then((r) => r.data),

  // Only for a tenant with no journal entries; the API answers 400 otherwise.
  applyTemplate: (templateId: string) =>
    apiClient
      .post<ApplyChartTemplateResult>(`${ACC_BASE}/accounting/chart/apply-template`, { templateId })
      .then((r) => r.data),
}
