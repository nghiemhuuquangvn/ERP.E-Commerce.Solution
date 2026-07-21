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

// AccountDto — no parentAccountId; chart of accounts is flat in this codebase.
export interface Account {
  id: string
  accountNumber: string
  name: string
  accountType: AccountType
  normalBalance: NormalBalance
  createdAt: string
}

// CreateAccountCommand(AccountNumber, Name, AccountType:int, NormalBalance:int)
export interface CreateAccountRequest {
  accountNumber: string
  name: string
  accountType: number
  normalBalance: number
}

export const accountsApi = {
  // ListAccountsQuery takes no filters; controller returns a plain List<AccountDto> (no pagination wrapper).
  list: () => apiClient.get<Account[]>(`${ACC_BASE}/accounts`).then((r) => r.data),

  get: (id: string) => apiClient.get<Account>(`${ACC_BASE}/accounts/${id}`).then((r) => r.data),

  create: (body: CreateAccountRequest) =>
    apiClient.post<Account>(`${ACC_BASE}/accounts`, body).then((r) => r.data),
}
