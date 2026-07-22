import { apiClient } from './client'

// IdentityService's RolesController — mounted under the Gateway's `/id/api/...`
// route, same reasoning as users.api.ts.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const ROLES_BASE = `${GATEWAY_ROOT}/id/api/v1/roles`

export interface MessageResponse {
  message: string
}

// RoleDto(Id, RoleCode, RoleName, IsActive)
export interface Role {
  id: string
  roleCode: string
  roleName: string
  isActive: boolean
}

export interface ListRolesParams {
  tenantId?: string
}

export interface CreateRoleRequest {
  roleCode: string
  roleName: string
}

export interface UpdateRoleRequest {
  roleName: string
}

export const rolesApi = {
  list: (params: ListRolesParams) =>
    apiClient.get<Role[]>(ROLES_BASE, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<Role>(`${ROLES_BASE}/${id}`).then((r) => r.data),

  create: (body: CreateRoleRequest) =>
    apiClient.post<Role>(ROLES_BASE, body).then((r) => r.data),

  update: (id: string, body: UpdateRoleRequest) =>
    apiClient.put<MessageResponse>(`${ROLES_BASE}/${id}`, body).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete<MessageResponse>(`${ROLES_BASE}/${id}`).then((r) => r.data),

  getPermissions: (id: string) =>
    apiClient.get<string[]>(`${ROLES_BASE}/${id}/permissions`).then((r) => r.data),

  assignPermission: (id: string, permissionCode: string) =>
    apiClient
      .post<MessageResponse>(`${ROLES_BASE}/${id}/permissions`, { permissionCode })
      .then((r) => r.data),

  removePermission: (id: string, permissionId: string) =>
    apiClient.delete<MessageResponse>(`${ROLES_BASE}/${id}/permissions/${permissionId}`).then((r) => r.data),
}
