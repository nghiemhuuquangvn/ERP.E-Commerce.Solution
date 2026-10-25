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
