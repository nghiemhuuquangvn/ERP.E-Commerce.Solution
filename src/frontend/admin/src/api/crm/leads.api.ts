import { apiClient, CRM_BASE, CrmPagedResult } from './crmClient'

// Matches ERP.CRM.Domain.Enums.LeadStatus (string in LeadDto responses).
export const LEAD_STATUS = {
  New: 0,
  Contacted: 1,
  Qualified: 2,
  Lost: 3,
} as const

// Matches ERP.CRM.Domain.Enums.LeadSource (int accepted by CreateLeadCommand,
// string in LeadDto responses).
export const LEAD_SOURCE = {
  Website: 0,
  Referral: 1,
  SocialMedia: 2,
  Email: 3,
  Phone: 4,
  Other: 5,
} as const

export interface Lead {
  id: string
  leadName: string
  leadStatus: string
  leadSource: string
  estimatedValue?: number | null
  contactId?: string | null
  createdAt: string
  updatedAt: string
}

export interface ListLeadsParams {
  status?: number
  page?: number
  pageSize?: number
}

// CreateLeadCommand(LeadName, LeadSource, EstimatedValue, ContactId)
export interface CreateLeadRequest {
  leadName: string
  leadSource: number
  estimatedValue?: number | null
  contactId?: string | null
}

// LoseLeadCommand(LeadId, Reason)
export interface LoseLeadRequest {
  reason: string
}

export const leadsApi = {
  list: (params: ListLeadsParams) =>
    apiClient.get<CrmPagedResult<Lead>>(`${CRM_BASE}/leads`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<Lead>(`${CRM_BASE}/leads/${id}`).then((r) => r.data),

  create: (body: CreateLeadRequest) =>
    apiClient.post<Lead>(`${CRM_BASE}/leads`, body).then((r) => r.data),

  // Mark a lead as contacted — no request body.
  contact: (id: string) => apiClient.post<Lead>(`${CRM_BASE}/leads/${id}/contact`).then((r) => r.data),

  // Qualify a lead — creates an opportunity automatically. No request body.
  qualify: (id: string) => apiClient.post<Lead>(`${CRM_BASE}/leads/${id}/qualify`).then((r) => r.data),

  lose: (id: string, body: LoseLeadRequest) =>
    apiClient.post<Lead>(`${CRM_BASE}/leads/${id}/lose`, body).then((r) => r.data),
}
