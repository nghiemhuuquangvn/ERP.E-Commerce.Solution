import axios from 'axios'
import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface Currency {
  id: string
  code: string
  name: string
  symbol: string
  decimalPlaces: number
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ExchangeRate {
  id: string
  fromCurrencyId: string
  toCurrencyId: string
  rate: number
  effectiveDate: string
  createdAt: string
  updatedAt: string
}

export interface ListCurrenciesParams {
  page?: number
  pageSize?: number
}

export interface ListExchangeRatesParams {
  toCurrencyId?: string
  page?: number
  pageSize?: number
}

// CreateCurrencyCommand(Code, Name, Symbol, DecimalPlaces = 2)
export interface CreateCurrencyRequest {
  code: string
  name: string
  symbol: string
  decimalPlaces?: number
}

// UpdateCurrencyRequest(Name, Symbol, DecimalPlaces) — no Code on update (immutable)
export interface UpdateCurrencyRequest {
  name: string
  symbol: string
  decimalPlaces: number
}

// CreateExchangeRateRequest(ToCurrencyId, Rate, EffectiveDate) — currencyId (FromCurrencyId) is in the route
export interface CreateExchangeRateRequest {
  toCurrencyId: string
  rate: number
  effectiveDate: string
}

// UpdateExchangeRateRequest(Rate, EffectiveDate)
export interface UpdateExchangeRateRequest {
  rate: number
  effectiveDate: string
}

export const currenciesApi = {
  list: (params: ListCurrenciesParams) =>
    apiClient.get<MstPagedResult<Currency>>(`${MST_BASE}/currencies`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<Currency>(`${MST_BASE}/currencies/${id}`).then((r) => r.data),

  create: (body: CreateCurrencyRequest) =>
    apiClient.post<Currency>(`${MST_BASE}/currencies`, body).then((r) => r.data),

  update: (id: string, body: UpdateCurrencyRequest) =>
    apiClient.put<Currency>(`${MST_BASE}/currencies/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<Currency>(`${MST_BASE}/currencies/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<Currency>(`${MST_BASE}/currencies/${id}/deactivate`).then((r) => r.data),

  listExchangeRates: (currencyId: string, params: ListExchangeRatesParams) =>
    apiClient
      .get<MstPagedResult<ExchangeRate>>(`${MST_BASE}/currencies/${currencyId}/exchange-rates`, {
        params,
      })
      .then((r) => r.data),

  getExchangeRate: (currencyId: string, id: string) =>
    apiClient
      .get<ExchangeRate>(`${MST_BASE}/currencies/${currencyId}/exchange-rates/${id}`)
      .then((r) => r.data),

  createExchangeRate: (currencyId: string, body: CreateExchangeRateRequest) =>
    apiClient
      .post<ExchangeRate>(`${MST_BASE}/currencies/${currencyId}/exchange-rates`, body)
      .then((r) => r.data),

  updateExchangeRate: (currencyId: string, id: string, body: UpdateExchangeRateRequest) =>
    apiClient
      .put<ExchangeRate>(`${MST_BASE}/currencies/${currencyId}/exchange-rates/${id}`, body)
      .then((r) => r.data),

  // Returns null when no effective rate exists for the pair as of the given date (backend responds 404).
  getEffectiveRate: (fromCurrencyId: string, toCurrencyId: string, asOfDate: string) =>
    apiClient
      .get<ExchangeRate>(`${MST_BASE}/currencies/exchange-rates/effective`, {
        params: { fromCurrencyId, toCurrencyId, asOfDate },
      })
      .then((r) => r.data)
      .catch((err: unknown) => {
        if (axios.isAxiosError(err) && err.response?.status === 404) return null
        throw err
      }),
}
