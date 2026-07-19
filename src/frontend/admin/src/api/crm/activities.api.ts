import { apiClient, CRM_BASE, CrmPagedResult } from './crmClient'

// Matches ERP.CRM.Domain.Enums.ActivityType (int accepted by CreateActivityCommand
// and list filter, string in ActivityDto responses).
export const ACTIVITY_TYPE = {
  Call: 0,
  Email: 1,
  Meeting: 2,
  Note: 3,
  Task: 4,
} as const

export interface Activity {
  id: string
  contactId: string
  opportunityId?: string | null
  caseId?: string | null
  activityType: string
  subject: string
  notes?: string | null
  status: string
  scheduledAt?: string | null
  completedAt?: string | null
  assignedToUserId?: string | null
  source?: string | null
  createdAt: string
}

export interface ListActivitiesParams {
  contactId?: string
  opportunityId?: string
  activityType?: number
  page?: number
  pageSize?: number
}

// CreateActivityCommand(ContactId, ActivityType, Subject, Notes, ScheduledAt,
// OpportunityId, CaseId, AssignedToUserId, Source)
export interface CreateActivityRequest {
  contactId: string
  activityType: number
  subject: string
  notes?: string | null
  scheduledAt?: string | null
  opportunityId?: string | null
  caseId?: string | null
  assignedToUserId?: string | null
  source?: string | null
}

export interface CompleteActivityRequest {
  notes?: string | null
}

export interface UpdateActivityNotesRequest {
  notes: string
}

export const activitiesApi = {
  list: (params: ListActivitiesParams) =>
    apiClient.get<CrmPagedResult<Activity>>(`${CRM_BASE}/activities`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<Activity>(`${CRM_BASE}/activities/${id}`).then((r) => r.data),

  create: (body: CreateActivityRequest) =>
    apiClient.post<Activity>(`${CRM_BASE}/activities`, body).then((r) => r.data),

  complete: (id: string, body: CompleteActivityRequest) =>
    apiClient.post<Activity>(`${CRM_BASE}/activities/${id}/complete`, body).then((r) => r.data),

  // Cancel a scheduled activity — no request body.
  cancel: (id: string) => apiClient.post<Activity>(`${CRM_BASE}/activities/${id}/cancel`).then((r) => r.data),

  updateNotes: (id: string, body: UpdateActivityNotesRequest) =>
    apiClient.patch<Activity>(`${CRM_BASE}/activities/${id}/notes`, body).then((r) => r.data),
}
