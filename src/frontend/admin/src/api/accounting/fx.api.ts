import { apiClient, ACC_BASE } from './accClient'

/** One unit of `from` in `to` on a day (ROADMAP P1-4); `inverted` when the master data holds the opposite pair. */
export interface ExchangeRateQuote {
  from: string
  to: string
  rate: number
  effectiveDate: string
  inverted: boolean
}

export interface RevaluationLine {
  documentType: 'Invoice' | 'Bill'
  documentId: string
  number: string
  currency: string
  foreignOutstanding: number
  carryingAmount: number
  closingRate: number
  revaluedAmount: number
  /** Positive = unrealized gain, negative = unrealized loss. */
  difference: number
}

export interface Revaluation {
  fiscalPeriodId: string
  periodName: string
  asOfDate: string
  baseCurrency: string
  rates: Record<string, number>
  missingRates: string[]
  lines: RevaluationLine[]
  receivablesDifference: number
  payablesDifference: number
  netDifference: number
  posted: boolean
  entryNumber: string | null
  reversalEntryNumber: string | null
  notes: string[]
}

export const fxApi = {
  /** The master data's rate of a currency in the base currency; null when none is entered. */
  rate: (currency: string, date?: string) =>
    apiClient
      .get<ExchangeRateQuote>(`${ACC_BASE}/accounting/fx/rate`, { params: { currency, date }, validateStatus: (s) => s === 200 || s === 404 })
      .then((r) => (r.status === 200 ? r.data : null)),

  previewRevaluation: (fiscalPeriodId: string, rates: Record<string, number>) =>
    apiClient.post<Revaluation>(`${ACC_BASE}/accounting/fx/revaluation/preview`, { fiscalPeriodId, rates }).then((r) => r.data),

  revalue: (fiscalPeriodId: string, rates: Record<string, number>) =>
    apiClient.post<Revaluation>(`${ACC_BASE}/accounting/fx/revaluation`, { fiscalPeriodId, rates }).then((r) => r.data),
}
