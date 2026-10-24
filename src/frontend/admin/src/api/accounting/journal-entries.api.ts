import { apiClient, ACC_BASE } from './accClient'

// JournalLineDto(AccountId, Debit, Credit, Description)
export interface JournalLine {
  accountId: string
  debit: number
  credit: number
  description: string
}

// JournalEntryDto(Id, EntryNumber, FiscalPeriodId, Description, TotalDebit, TotalCredit, CreatedAt, EntryDate, Status, ReferenceType, ...)
// status: Draft | Posted | Voided | Reversed. A hand-written entry of an open period can be voided; any posted entry can be reversed.
export interface JournalEntry {
  id: string
  entryNumber: string
  fiscalPeriodId: string
  description: string
  totalDebit: number
  totalCredit: number
  createdAt: string
  entryDate?: string
  status?: string
  referenceType?: string | null
  referenceId?: string | null
  voidReason?: string | null
  lines: JournalLine[]
}

export interface ListJournalEntriesParams {
  fiscalPeriodId?: string
  pageNumber?: number
  pageSize?: number
}

// PostJournalEntryCommand(EntryNumber, FiscalPeriodId, Description, Lines[])
// NOTE: this single endpoint both creates AND posts the entry — there is no
// separate draft/post step, and there is no update/delete endpoint at all.
export interface PostJournalEntryRequest {
  entryNumber: string
  fiscalPeriodId: string
  description: string
  lines: {
    accountId: string
    debit: number
    credit: number
    description: string
  }[]
}

export const journalEntriesApi = {
  // NOTE: despite the pageNumber/pageSize query params, JournalEntriesController.List
  // returns a plain `List<JournalEntryDto>` (confirmed via ListJournalEntriesQueryHandler
  // in AccountingQueryHandlers.cs) — no wrapper object, no totalCount. Callers must
  // build Previous/Next paging from the returned array's length alone (see
  // accClient.ts's note and JournalEntriesPage.tsx).
  list: (params: ListJournalEntriesParams) =>
    apiClient.get<JournalEntry[]>(`${ACC_BASE}/journal-entries`, { params }).then((r) => r.data),

  get: (id: string) =>
    apiClient.get<JournalEntry>(`${ACC_BASE}/journal-entries/${id}`).then((r) => r.data),

  create: (body: PostJournalEntryRequest) =>
    apiClient.post<JournalEntry>(`${ACC_BASE}/journal-entries`, body).then((r) => r.data),

  // A mirror entry at reversalDate (default today); the original becomes Reversed.
  reverse: (id: string, reason: string, reversalDate?: string) =>
    apiClient
      .post<JournalEntry>(`${ACC_BASE}/journal-entries/${id}/reverse`, { reason, reversalDate: reversalDate || null })
      .then((r) => r.data),

  // Only a hand-written entry of an open period (status Voided, ignored by every report).
  void: (id: string, reason: string) =>
    apiClient.post<JournalEntry>(`${ACC_BASE}/journal-entries/${id}/void`, { reason }).then((r) => r.data),
}
