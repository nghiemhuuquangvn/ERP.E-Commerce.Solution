import { gatewayClient as apiClient } from './client'

// IdentityService's PermissionsController. List-only wrapper — Create/Update/Delete
// exist on the backend but never call SaveChangesAsync (known no-op persistence
// bug), so the frontend deliberately ships a read-only catalog view instead of
// building CRUD against a broken endpoint.
const PERMISSIONS_BASE = `/id/api/v1/permissions`

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
