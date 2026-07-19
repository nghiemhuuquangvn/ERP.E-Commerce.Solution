import { apiClient, CRM_BASE, CrmPagedResult } from './crmClient'

// Matches ERP.CRM.Domain.Enums.ContactSource (int accepted by CreateContactCommand,
// string in ContactDto responses).
export const CONTACT_SOURCE = {
  Manual: 0,
  UserRegistration: 1,
  Import: 2,
} as const

export interface Contact {
  id: string
  name: string
  email?: string | null
  phone?: string | null
  company?: string | null
  userId?: string | null
  contactSource: string
  createdAt: string
  updatedAt: string
}

export interface ListContactsParams {
  search?: string
  page?: number
  pageSize?: number
}

// CreateContactCommand(Name, Email, Phone, Company, UserId, ContactSource = 0)
export interface CreateContactRequest {
  name: string
  email?: string | null
  phone?: string | null
  company?: string | null
  userId?: string | null
  contactSource?: number
}

// UpdateContactCommand(ContactId, Name, Email, Phone, Company) — no Company-only or
// source update; the route id must match ContactId in the body.
export interface UpdateContactRequest {
  contactId: string
  name: string
  email?: string | null
  phone?: string | null
  company?: string | null
}

export interface ContactTags {
  contactId: string
  tags: string[]
}

export const contactsApi = {
  list: (params: ListContactsParams) =>
    apiClient.get<CrmPagedResult<Contact>>(`${CRM_BASE}/contacts`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<Contact>(`${CRM_BASE}/contacts/${id}`).then((r) => r.data),

  create: (body: CreateContactRequest) =>
    apiClient.post<Contact>(`${CRM_BASE}/contacts`, body).then((r) => r.data),

  update: (id: string, body: UpdateContactRequest) =>
    apiClient.put<Contact>(`${CRM_BASE}/contacts/${id}`, body).then((r) => r.data),

  getTags: (id: string) =>
    apiClient.get<ContactTags>(`${CRM_BASE}/contacts/${id}/tags`).then((r) => r.data),

  addTag: (id: string, tag: string) =>
    apiClient.post<ContactTags>(`${CRM_BASE}/contacts/${id}/tags`, { tag }).then((r) => r.data),

  removeTag: (id: string, tag: string) =>
    apiClient
      .delete<ContactTags>(`${CRM_BASE}/contacts/${id}/tags/${encodeURIComponent(tag)}`)
      .then((r) => r.data),
}
