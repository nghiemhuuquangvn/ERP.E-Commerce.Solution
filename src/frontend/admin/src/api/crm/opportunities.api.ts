import { apiClient, CRM_BASE, CrmPagedResult } from './crmClient'

// Matches ERP.CRM.Domain.Enums.OpportunityStage (int accepted by list filter,
// string in OpportunityDto responses).
export const OPPORTUNITY_STAGE = {
  Prospecting: 0,
  Qualification: 1,
  Proposal: 2,
  Negotiation: 3,
  Won: 4,
  Lost: 5,
} as const

export interface Opportunity {
  id: string
  opportunityName: string
  contactId: string
  stage: string
  probabilityPercent: number
  estimatedValue?: number | null
  expectedCloseDate?: string | null
  assignedToUserId?: string | null
  lostReason?: string | null
  createdAt: string
  updatedAt: string
}

export interface ListOpportunitiesParams {
  contactId?: string
  stage?: number
  page?: number
  pageSize?: number
}

export interface PipelineSummary {
  prospecting: number
  qualification: number
  proposal: number
  negotiation: number
  won: number
  lost: number
  totalValue: number
  wonValue: number
}

// CreateOpportunityCommand(Title, ContactId, Value, ExpectedCloseDate,
// AssignedToUserId) — note the command's field names (Title/Value) differ from
// OpportunityDto's response field names (OpportunityName/EstimatedValue); the
// backend doesn't accept the response shape back as a create request.
export interface CreateOpportunityRequest {
  title: string
  contactId: string
  value: number
  expectedCloseDate: string
  assignedToUserId?: string | null
}

// LoseOpportunityCommand(OpportunityId, Reason)
export interface LoseOpportunityRequest {
  reason: string
}

export const opportunitiesApi = {
  list: (params: ListOpportunitiesParams) =>
    apiClient
      .get<CrmPagedResult<Opportunity>>(`${CRM_BASE}/opportunities`, { params })
      .then((r) => r.data),

  pipelineSummary: () =>
    apiClient.get<PipelineSummary>(`${CRM_BASE}/opportunities/pipeline/summary`).then((r) => r.data),

  get: (id: string) => apiClient.get<Opportunity>(`${CRM_BASE}/opportunities/${id}`).then((r) => r.data),

  create: (body: CreateOpportunityRequest) =>
    apiClient.post<Opportunity>(`${CRM_BASE}/opportunities`, body).then((r) => r.data),

  // Advance to the next pipeline stage — no request body.
  advance: (id: string) =>
    apiClient.post<Opportunity>(`${CRM_BASE}/opportunities/${id}/advance`).then((r) => r.data),

  // Mark as won — no request body.
  win: (id: string) => apiClient.post<Opportunity>(`${CRM_BASE}/opportunities/${id}/win`).then((r) => r.data),

  lose: (id: string, body: LoseOpportunityRequest) =>
    apiClient.post<Opportunity>(`${CRM_BASE}/opportunities/${id}/lose`, body).then((r) => r.data),
}
