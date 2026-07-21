import { apiClient, ACC_BASE } from './accClient'

// TaxRateDto
export interface TaxRate {
  id: string
  name: string
  code: string
  rate: number
  isActive: boolean
  description?: string | null
  createdAt: string
}

export interface ListTaxRatesParams {
  activeOnly?: boolean
}

// CreateTaxRateCommand(Name, Code, Rate, Description?)
export interface CreateTaxRateRequest {
  name: string
  code: string
  rate: number
  description?: string | null
}

// TaxRatesController.UpdateTaxRateRequest(Name, Code, Rate, Description?) — PUT body;
// the controller maps this into UpdateTaxRateCommand server-side.
export interface UpdateTaxRateRequest {
  name: string
  code: string
  rate: number
  description?: string | null
}

export const taxRatesApi = {
  // ListTaxRatesQuery { ActiveOnly?: bool } — controller returns a plain List<TaxRateDto> (no pagination wrapper).
  list: (params?: ListTaxRatesParams) =>
    apiClient.get<TaxRate[]>(`${ACC_BASE}/tax-rates`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<TaxRate>(`${ACC_BASE}/tax-rates/${id}`).then((r) => r.data),

  create: (body: CreateTaxRateRequest) =>
    apiClient.post<TaxRate>(`${ACC_BASE}/tax-rates`, body).then((r) => r.data),

  update: (id: string, body: UpdateTaxRateRequest) =>
    apiClient.put<TaxRate>(`${ACC_BASE}/tax-rates/${id}`, body).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<TaxRate>(`${ACC_BASE}/tax-rates/${id}/deactivate`).then((r) => r.data),
}
