import { apiClient, ACC_BASE } from './accClient'

// A tenant's own placement of accounts on the rows of B01 / B02: accounts the standard layout cannot place are "unmapped" until a prefix is mapped here.
export type MappingSide = 'Net' | 'DebitOnly' | 'CreditOnly'

export interface StatementMappingRow {
  code: string
  name: string
  indent: number
  isMappable: boolean
  standardPrefixes: string[]
  mappings: { accountPrefix: string; side: MappingSide }[]
}

export interface StatementMapping {
  statementId: string
  title: string
  /** False for B03 and the notes: the "balances taken" choice has no effect there. */
  sideApplies: boolean
  /** True when the unmapped accounts are shown on the report itself (B03) or do not apply (B09). */
  unmappedOnReport: boolean
  rows: StatementMappingRow[]
  unmapped: { accountNumber: string; accountName: string }[]
}

export interface StatementMappingInput {
  rowCode: string
  accountPrefix: string
  side: MappingSide
}

export const statementMappingsApi = {
  get: (statementId: string) =>
    apiClient.get<StatementMapping>(`${ACC_BASE}/accounting/statement-mappings`, { params: { statementId } }).then((r) => r.data),

  update: (statementId: string, mappings: StatementMappingInput[]) =>
    apiClient.put<StatementMapping>(`${ACC_BASE}/accounting/statement-mappings`, { statementId, mappings }).then((r) => r.data),
}
