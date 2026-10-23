import { gatewayClient as apiClient } from './client'

// IdentityService's UsersController is mounted under the Gateway's `/id/api/...`
// route, same as settings.api.ts — see that file's comment for why the prefix
// differs from apiClient's default `/api` baseURL.
const USERS_BASE = `/id/api/v1/users`

export interface EnableTwoFactorRequest {
  method: 'Sms' | 'Email'
  phoneNumber?: string | null
  /** Re-confirms the caller. Omit only for accounts that sign in through an external provider only. */
  currentPassword?: string | null
}

export interface DisableTwoFactorRequest {
  currentPassword?: string | null
}

export interface MessageResponse {
  message: string
}

export interface RoleAssignment {
  roleId: string
  roleCode: string
  roleName: string
}

// UserDetailDto(Id, Email, UserName, FullName, IsActive, IsLocked, LastLoginAt,
// Roles, Permissions, RoleAssignments) — see UsersController.cs/UserService.cs.
export interface UserDetail {
  id: string
  email: string
  userName: string | null
  fullName: string | null
  isActive: boolean
  isLocked: boolean
  lastLoginAt: string | null
  roles: string[]
  permissions: string[]
  roleAssignments: RoleAssignment[]
}

export interface PagedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ListUsersParams {
  tenantId?: string
  page?: number
  pageSize?: number
}

export interface UpdateProfileRequest {
  fullName?: string | null
  userName?: string | null
}

export interface AssignRoleRequest {
  userId: string
  roleCode: string
}

export interface InviteUserToTenantRequest {
  email: string
  roleCode?: string | null
}

export interface InviteUserToTenantResponse {
  userId: string
  tenantId: string
  alreadyMember: boolean
}

export const usersApi = {
  list: (params: ListUsersParams) =>
    apiClient.get<PagedResponse<UserDetail>>(USERS_BASE, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<UserDetail>(`${USERS_BASE}/${id}`).then((r) => r.data),

  updateProfile: (id: string, body: UpdateProfileRequest) =>
    apiClient.put<MessageResponse>(`${USERS_BASE}/${id}/profile`, body).then((r) => r.data),

  lock: (id: string) =>
    apiClient.post<MessageResponse>(`${USERS_BASE}/${id}/lock`).then((r) => r.data),

  unlock: (id: string) =>
    apiClient.post<MessageResponse>(`${USERS_BASE}/${id}/unlock`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.delete<MessageResponse>(`${USERS_BASE}/${id}`).then((r) => r.data),

  assignRole: (id: string, roleCode: string) =>
    apiClient
      .post<MessageResponse>(`${USERS_BASE}/${id}/roles`, { userId: id, roleCode } as AssignRoleRequest)
      .then((r) => r.data),

  removeRole: (id: string, roleId: string) =>
    apiClient.delete<MessageResponse>(`${USERS_BASE}/${id}/roles/${roleId}`).then((r) => r.data),

  invite: (body: InviteUserToTenantRequest) =>
    apiClient.post<InviteUserToTenantResponse>(`${USERS_BASE}/invite`, body).then((r) => r.data),

  enableTwoFactor: (userId: string, body: EnableTwoFactorRequest) =>
    apiClient.post<MessageResponse>(`${USERS_BASE}/${userId}/2fa/enable`, body).then((r) => r.data),

  disableTwoFactor: (userId: string, body: DisableTwoFactorRequest = {}) =>
    apiClient.post<MessageResponse>(`${USERS_BASE}/${userId}/2fa/disable`, body).then((r) => r.data),
}
