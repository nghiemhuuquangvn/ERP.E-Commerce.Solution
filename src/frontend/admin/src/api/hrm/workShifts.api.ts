import { apiClient, HRM_BASE } from './hrmClient'

export interface WorkShift {
  id: string
  name: string
  startTime: string
  endTime: string
  shiftHours: number
  isActive: boolean
  createdAt: string
}

export interface ListWorkShiftsParams {
  activeOnly?: boolean
}

// CreateWorkShiftCommand(Name, StartTime, EndTime)
export interface CreateWorkShiftRequest {
  name: string
  startTime: string
  endTime: string
}

// UpdateShiftRequest(Name, StartTime, EndTime) — matches WorkShiftsController.UpdateShiftRequest
export interface UpdateWorkShiftRequest {
  name: string
  startTime: string
  endTime: string
}

export const workShiftsApi = {
  // Not paged — WorkShiftsController.List returns IEnumerable<WorkShiftDto>.
  list: (params: ListWorkShiftsParams = {}, signal?: AbortSignal) =>
    apiClient.get<WorkShift[]>(`${HRM_BASE}/work-shifts`, { params, signal }).then((r) => r.data),

  get: (id: string) => apiClient.get<WorkShift>(`${HRM_BASE}/work-shifts/${id}`).then((r) => r.data),

  create: (body: CreateWorkShiftRequest) =>
    apiClient.post<WorkShift>(`${HRM_BASE}/work-shifts`, body).then((r) => r.data),

  update: (id: string, body: UpdateWorkShiftRequest) =>
    apiClient.put<WorkShift>(`${HRM_BASE}/work-shifts/${id}`, body).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<WorkShift>(`${HRM_BASE}/work-shifts/${id}/deactivate`).then((r) => r.data),
}
