import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface Country {
  id: string
  code: string
  name: string
  phoneCode?: string | null
  languageCode?: string | null
  createdAt: string
  updatedAt: string
}

export interface Province {
  id: string
  countryId: string
  code: string
  name: string
  languageCode?: string | null
  createdAt: string
  updatedAt: string
}

export interface District {
  id: string
  provinceId: string
  code: string
  name: string
  languageCode?: string | null
  createdAt: string
  updatedAt: string
}

export interface Ward {
  id: string
  districtId: string
  code: string
  name: string
  languageCode?: string | null
  createdAt: string
  updatedAt: string
}

export interface ListGeographyParams {
  languageCode?: string
  page?: number
  pageSize?: number
}

// CreateCountryCommand(Code, Name, PhoneCode)
export interface CreateCountryRequest {
  code: string
  name: string
  phoneCode?: string | null
}

// CreateProvinceRequest(Code, Name) — countryId is in the route
export interface CreateProvinceRequest {
  code: string
  name: string
}

// CreateDistrictRequest(Code, Name) — provinceId is in the route
export interface CreateDistrictRequest {
  code: string
  name: string
}

// CreateWardRequest(Code, Name) — districtId is in the route
export interface CreateWardRequest {
  code: string
  name: string
}

export const geographyApi = {
  listCountries: (params: ListGeographyParams) =>
    apiClient
      .get<MstPagedResult<Country>>(`${MST_BASE}/geography/countries`, { params })
      .then((r) => r.data),

  getCountry: (countryId: string, languageCode?: string) =>
    apiClient
      .get<Country>(`${MST_BASE}/geography/countries/${countryId}`, { params: { languageCode } })
      .then((r) => r.data),

  createCountry: (body: CreateCountryRequest) =>
    apiClient.post<Country>(`${MST_BASE}/geography/countries`, body).then((r) => r.data),

  listProvinces: (countryId: string, params: ListGeographyParams) =>
    apiClient
      .get<MstPagedResult<Province>>(`${MST_BASE}/geography/countries/${countryId}/provinces`, {
        params,
      })
      .then((r) => r.data),

  getProvince: (provinceId: string, languageCode?: string) =>
    apiClient
      .get<Province>(`${MST_BASE}/geography/provinces/${provinceId}`, { params: { languageCode } })
      .then((r) => r.data),

  createProvince: (countryId: string, body: CreateProvinceRequest) =>
    apiClient
      .post<Province>(`${MST_BASE}/geography/countries/${countryId}/provinces`, body)
      .then((r) => r.data),

  listDistricts: (provinceId: string, params: ListGeographyParams) =>
    apiClient
      .get<MstPagedResult<District>>(`${MST_BASE}/geography/provinces/${provinceId}/districts`, {
        params,
      })
      .then((r) => r.data),

  getDistrict: (districtId: string, languageCode?: string) =>
    apiClient
      .get<District>(`${MST_BASE}/geography/districts/${districtId}`, { params: { languageCode } })
      .then((r) => r.data),

  createDistrict: (provinceId: string, body: CreateDistrictRequest) =>
    apiClient
      .post<District>(`${MST_BASE}/geography/provinces/${provinceId}/districts`, body)
      .then((r) => r.data),

  listWards: (districtId: string, params: ListGeographyParams) =>
    apiClient
      .get<MstPagedResult<Ward>>(`${MST_BASE}/geography/districts/${districtId}/wards`, {
        params,
      })
      .then((r) => r.data),

  getWard: (wardId: string, languageCode?: string) =>
    apiClient
      .get<Ward>(`${MST_BASE}/geography/wards/${wardId}`, { params: { languageCode } })
      .then((r) => r.data),

  createWard: (districtId: string, body: CreateWardRequest) =>
    apiClient
      .post<Ward>(`${MST_BASE}/geography/districts/${districtId}/wards`, body)
      .then((r) => r.data),
}
