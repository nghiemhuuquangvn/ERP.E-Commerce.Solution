import { apiClient } from './client'

// IdentityService's PermissionsController. List-only wrapper — Create/Update/Delete
// exist on the backend but never call SaveChangesAsync (known no-op persistence
// bug), so the frontend deliberately ships a read-only catalog view instead of
// building CRUD against a broken endpoint.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const PERMISSIONS_BASE = `${GATEWAY_ROOT}/id/api/permissions`

// PermissionDto(Id, PermissionCode, PermissionName, Module)
export interface Permission {
  id: string
  permissionCode: string
  permissionName: string | null
  module: string | null
}

export const permissionsApi = {
  list: () => apiClient.get<Permission[]>(PERMISSIONS_BASE).then((r) => r.data),
}
