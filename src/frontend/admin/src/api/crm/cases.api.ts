import { apiClient, CRM_BASE, CrmPagedResult } from './crmClient'

// Matches ERP.CRM.Domain.Enums.CasePriority (int accepted by OpenCaseCommand,
// string in CaseDto responses).
export const CASE_PRIORITY = {
  Low: 0,
  Medium: 1,
  High: 2,
  Critical: 3,
} as const

// Matches ERP.CRM.Domain.Enums.CaseStatus (int accepted by list filter,
// string in CaseDto responses).
export const CASE_STATUS = {
  Open: 0,
  InProgress: 1,
  Resolved: 2,
  Closed: 3,
} as const

// Named CrmCase, not Case, since Case is a reserved word.
export interface CrmCase {
  id: string
  caseNumber: string
  contactId: string
  caseSubject: string
  caseDescription?: string | null
  priority: string
  status: string
  resolutionNote?: string | null
  assignedToUserId?: string | null
  createdAt: string
  updatedAt: string
}

export interface ListCasesParams {
  contactId?: string
  status?: number
  page?: number
  pageSize?: number
}

// OpenCaseCommand(Title, ContactId, Description, Priority) — note the command's
// field names (Title/Description) differ from CaseDto's response field names
// (CaseSubject/CaseDescription).
export interface OpenCaseRequest {
  title: string
  contactId: string
  description: string
  priority: number
}

export interface AssignCaseRequest {
  assignedToUserId: string
}

export interface ResolveCaseRequest {
  resolution: string
}

export const casesApi = {
  list: (params: ListCasesParams) =>
    apiClient.get<CrmPagedResult<CrmCase>>(`${CRM_BASE}/cases`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<CrmCase>(`${CRM_BASE}/cases/${id}`).then((r) => r.data),

  open: (body: OpenCaseRequest) => apiClient.post<CrmCase>(`${CRM_BASE}/cases`, body).then((r) => r.data),

  assign: (id: string, body: AssignCaseRequest) =>
    apiClient.post<CrmCase>(`${CRM_BASE}/cases/${id}/assign`, body).then((r) => r.data),

  resolve: (id: string, body: ResolveCaseRequest) =>
    apiClient.post<CrmCase>(`${CRM_BASE}/cases/${id}/resolve`, body).then((r) => r.data),

  // Close a resolved case — no request body.
  close: (id: string) => apiClient.post<CrmCase>(`${CRM_BASE}/cases/${id}/close`).then((r) => r.data),
}
