import { apiClient, ACC_BASE } from './accClient'

/** A document (invoice or approved bill) that has no journal entry; after a repost, whether the entry was posted now (ROADMAP P1-3b). */
export interface PostingGap {
  documentType: 'Invoice' | 'Bill'
  documentId: string
  number: string
  date: string
  amount: number
  posted: boolean | null
  entryNumber: string | null
}

export const postingGapsApi = {
  list: () => apiClient.get<PostingGap[]>(`${ACC_BASE}/accounting/posting-gaps`).then((r) => r.data),
  repost: () => apiClient.post<PostingGap[]>(`${ACC_BASE}/accounting/posting-gaps/repost`).then((r) => r.data),
}
