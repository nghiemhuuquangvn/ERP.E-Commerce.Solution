import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

// Matches ERP.MST.Domain.Enums.UnitType.
export const UNIT_TYPE = {
  Weight: 0,
  Volume: 1,
  Length: 2,
  Count: 3,
  Area: 4,
} as const

export interface UnitOfMeasure {
  id: string
  code: string
  name: string
  symbol: string
  unitType: string
  baseUnitId?: string | null
  conversionFactor: number
  languageCode?: string | null
  createdAt: string
  updatedAt: string
}

export interface ListUnitsOfMeasureParams {
  languageCode?: string
  page?: number
  pageSize?: number
}

// CreateUnitOfMeasureCommand(Code, Name, Symbol, UnitType, BaseUnitId, ConversionFactor)
export interface CreateUnitOfMeasureRequest {
  code: string
  name: string
  symbol: string
  unitType: number
  baseUnitId?: string | null
  conversionFactor?: number
}

// UpdateUnitOfMeasureRequest(Name, Symbol, ConversionFactor) — Code/UnitType not editable
export interface UpdateUnitOfMeasureRequest {
  name: string
  symbol: string
  conversionFactor: number
}

export const unitsOfMeasureApi = {
  list: (params: ListUnitsOfMeasureParams) =>
    apiClient
      .get<MstPagedResult<UnitOfMeasure>>(`${MST_BASE}/units-of-measure`, { params })
      .then((r) => r.data),

  get: (id: string, languageCode?: string) =>
    apiClient
      .get<UnitOfMeasure>(`${MST_BASE}/units-of-measure/${id}`, { params: { languageCode } })
      .then((r) => r.data),

  create: (body: CreateUnitOfMeasureRequest) =>
    apiClient.post<UnitOfMeasure>(`${MST_BASE}/units-of-measure`, body).then((r) => r.data),

  update: (id: string, body: UpdateUnitOfMeasureRequest) =>
    apiClient.put<UnitOfMeasure>(`${MST_BASE}/units-of-measure/${id}`, body).then((r) => r.data),
}
