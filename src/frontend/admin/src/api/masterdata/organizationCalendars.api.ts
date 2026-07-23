import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface OrganizationCalendar {
  id: string
  companyId: string
  year: number
  name: string
  isDefault: boolean
  createdAt: string
  updatedAt: string
}

export interface Holiday {
  id: string
  calendarId: string
  date: string
  name: string
  isRecurringYearly: boolean
  createdAt: string
  updatedAt: string
}

export interface ListOrganizationCalendarsParams {
  companyId?: string
  page?: number
  pageSize?: number
}

export interface ListHolidaysParams {
  page?: number
  pageSize?: number
}

// CreateOrganizationCalendarCommand(CompanyId, Year, Name, IsDefault)
export interface CreateOrganizationCalendarRequest {
  companyId: string
  year: number
  name: string
  isDefault?: boolean
}

// UpdateOrganizationCalendarRequest(Name, IsDefault) — no CompanyId/Year on update
export interface UpdateOrganizationCalendarRequest {
  name: string
  isDefault: boolean
}

// CreateHolidayRequest(Date, Name, IsRecurringYearly) — calendarId is in the route
export interface CreateHolidayRequest {
  date: string
  name: string
  isRecurringYearly?: boolean
}

// UpdateHolidayRequest(Date, Name, IsRecurringYearly)
export interface UpdateHolidayRequest {
  date: string
  name: string
  isRecurringYearly: boolean
}

export const organizationCalendarsApi = {
  list: (params: ListOrganizationCalendarsParams) =>
    apiClient
      .get<MstPagedResult<OrganizationCalendar>>(`${MST_BASE}/organization-calendars`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient
      .get<OrganizationCalendar>(`${MST_BASE}/organization-calendars/${id}`)
      .then((r) => r.data),

  create: (body: CreateOrganizationCalendarRequest) =>
    apiClient
      .post<OrganizationCalendar>(`${MST_BASE}/organization-calendars`, body)
      .then((r) => r.data),

  update: (id: string, body: UpdateOrganizationCalendarRequest) =>
    apiClient
      .put<OrganizationCalendar>(`${MST_BASE}/organization-calendars/${id}`, body)
      .then((r) => r.data),

  listHolidays: (calendarId: string, params: ListHolidaysParams) =>
    apiClient
      .get<MstPagedResult<Holiday>>(`${MST_BASE}/organization-calendars/${calendarId}/holidays`, {
        params,
      })
      .then((r) => r.data),

  getHoliday: (calendarId: string, id: string) =>
    apiClient
      .get<Holiday>(`${MST_BASE}/organization-calendars/${calendarId}/holidays/${id}`)
      .then((r) => r.data),

  createHoliday: (calendarId: string, body: CreateHolidayRequest) =>
    apiClient
      .post<Holiday>(`${MST_BASE}/organization-calendars/${calendarId}/holidays`, body)
      .then((r) => r.data),

  updateHoliday: (calendarId: string, id: string, body: UpdateHolidayRequest) =>
    apiClient
      .put<Holiday>(`${MST_BASE}/organization-calendars/${calendarId}/holidays/${id}`, body)
      .then((r) => r.data),
}
