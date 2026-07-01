import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface SupportedLanguage {
  id: string
  code: string
  name: string
  nativeName: string
  isDefault: boolean
  isActive: boolean
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export interface ListLanguagesParams {
  page?: number
  pageSize?: number
}

// CreateLanguageCommand(Code, Name, NativeName, IsDefault, SortOrder)
export interface CreateLanguageRequest {
  code: string
  name: string
  nativeName: string
  isDefault: boolean
  sortOrder: number
}

export const languagesApi = {
  list: (params: ListLanguagesParams) =>
    apiClient
      .get<MstPagedResult<SupportedLanguage>>(`${MST_BASE}/languages`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<SupportedLanguage>(`${MST_BASE}/languages/${id}`).then((r) => r.data),

  create: (body: CreateLanguageRequest) =>
    apiClient.post<SupportedLanguage>(`${MST_BASE}/languages`, body).then((r) => r.data),
}
