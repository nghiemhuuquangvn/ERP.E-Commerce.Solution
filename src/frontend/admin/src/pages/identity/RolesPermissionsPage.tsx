import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { rolesApi } from '@/api/roles.api'
import type { Role } from '@/api/roles.api'
import { permissionsApi } from '@/api/permissions.api'
import type { Permission } from '@/api/permissions.api'
import { tenantsApi } from '@/api/tenants.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

interface RoleFormValues {
  roleCode: string
  roleName: string
}

function RoleModal({
  role,
  tenantId,
  onClose,
}: {
  role: Role | null
  tenantId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!role
  const { register, handleSubmit, formState: { errors } } = useForm<RoleFormValues>({
    defaultValues: { roleCode: role?.roleCode ?? '', roleName: role?.roleName ?? '' },
  })

  const mutation = useMutation({
    mutationFn: async (values: RoleFormValues) => {
      if (isEdit) {
        await rolesApi.update(role!.id, { roleName: values.roleName })
        return
      }
      await rolesApi.create({ roleCode: values.roleCode, roleName: values.roleName })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['identity', 'roles', tenantId] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('identity.roles.editTitle') : t('identity.roles.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
              <input className="input" {...register('roleCode', { required: true })} />
              {errors.roleCode && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('roleName', { required: true })} />
            {errors.roleName && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('common.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function ManagePermissionsModal({ role, onClose }: { role: Role; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data: catalog } = useQuery({
    queryKey: ['identity', 'permissions', 'catalog'],
    queryFn: () => permissionsApi.list(),
    staleTime: 60_000,
  })

  const { data: assignedCodes } = useQuery({
    queryKey: ['identity', 'roles', role.id, 'permissions'],
    queryFn: () => rolesApi.getPermissions(role.id),
    staleTime: 15_000,
  })

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['identity', 'roles', role.id, 'permissions'] })

  const assignMutation = useMutation({
    mutationFn: (code: string) => rolesApi.assignPermission(role.id, code),
    onSuccess: invalidate,
  })

  const removeMutation = useMutation({
    mutationFn: (permissionId: string) => rolesApi.removePermission(role.id, permissionId),
    onSuccess: invalidate,
  })

  const assignedSet = useMemo(() => new Set(assignedCodes ?? []), [assignedCodes])

  const grouped = useMemo(() => {
    const groups = new Map<string, Permission[]>()
    for (const p of catalog ?? []) {
      const module = p.module ?? 'Other'
      const list = groups.get(module) ?? []
      list.push(p)
      groups.set(module, list)
    }
    return groups
  }, [catalog])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-lg max-h-[80vh] overflow-y-auto p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('identity.roles.managePermissionsTitle', { name: role.roleName })}
        </h3>

        <div className="space-y-4">
          {Array.from(grouped.entries()).map(([module, perms]) => (
            <div key={module}>
              <h4 className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-400">{module}</h4>
              <ul className="space-y-1">
                {perms.map((p) => {
                  const checked = assignedSet.has(p.permissionCode)
                  return (
                    <li key={p.id} className="flex items-center justify-between">
                      <label className="flex items-center gap-2 text-sm text-gray-700">
                        <input
                          type="checkbox"
                          checked={checked}
                          disabled={assignMutation.isPending || removeMutation.isPending}
                          onChange={() =>
                            checked ? removeMutation.mutate(p.id) : assignMutation.mutate(p.permissionCode)
                          }
                        />
                        {p.permissionName ?? p.permissionCode}
                      </label>
                      <span className="font-mono text-xs text-gray-400">{p.permissionCode}</span>
                    </li>
                  )
                })}
              </ul>
            </div>
          ))}
        </div>

        <div className="flex justify-end pt-6">
          <button type="button" onClick={onClose} className="btn-outline">
            {t('common.close')}
          </button>
        </div>
      </div>
    </div>
  )
}

export function RolesPermissionsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const authUser = useAuthStore((s) => s.user)
  const queryClient = useQueryClient()

  const canViewTenants = hasPermission('identity:tenants:view')
  const canCreate = hasPermission('identity:roles:create')
  const canEdit = hasPermission('identity:roles:edit')
  const canDelete = hasPermission('identity:roles:delete')
  const canManage = hasPermission('identity:roles:manage')

  const [selectedTenantId, setSelectedTenantId] = useState('')
  const [modalState, setModalState] = useState<{ open: boolean; role: Role | null }>({ open: false, role: null })
  const [permsTarget, setPermsTarget] = useState<Role | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<Role | null>(null)

  const { data: tenants } = useQuery({
    queryKey: ['identity', 'tenants'],
    queryFn: () => tenantsApi.list(),
    enabled: canViewTenants,
    staleTime: 60_000,
  })

  const effectiveTenantId = canViewTenants && selectedTenantId ? selectedTenantId : authUser?.tenantId ?? ''

  const { data: roles, isLoading } = useQuery({
    queryKey: ['identity', 'roles', effectiveTenantId],
    queryFn: () => rolesApi.list({ tenantId: effectiveTenantId || undefined }),
    staleTime: 15_000,
  })

  const { data: catalog, isLoading: catalogLoading } = useQuery({
    queryKey: ['identity', 'permissions', 'catalog'],
    queryFn: () => permissionsApi.list(),
    staleTime: 60_000,
  })

  const deleteMutation = useMutation({
    mutationFn: (r: Role) => rolesApi.delete(r.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['identity', 'roles', effectiveTenantId] })
      setDeleteTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'roleCode', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'roleName', label: t('common.name') },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (r: Role) => (
          <StatusBadge status={r.isActive ? 'Active' : 'Cancelled'} label={r.isActive ? t('common.activate') : t('common.deactivate')} />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (r: Role) => (
          <div className="flex gap-3">
            {canManage && (
              <button className="text-xs text-primary-600 hover:underline" onClick={() => setPermsTarget(r)}>
                {t('identity.roles.managePermissions')}
              </button>
            )}
            {canEdit && (
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, role: r })}
              >
                {t('common.edit')}
              </button>
            )}
            {canDelete && (
              <button className="text-xs text-red-600 hover:underline" onClick={() => setDeleteTarget(r)}>
                {t('common.delete')}
              </button>
            )}
          </div>
        ),
      },
    ],
    [canManage, canEdit, canDelete, t],
  )

  const catalogColumns = useMemo(
    () => [
      { key: 'permissionCode', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'permissionName', label: t('common.name'), render: (p: Permission) => p.permissionName ?? '—' },
      { key: 'module', label: t('identity.roles.module'), render: (p: Permission) => p.module ?? '—' },
    ],
    [t],
  )

  return (
    <div className="space-y-8">
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-semibold">{t('identity.roles.title')}</h1>
          {canCreate && (
            <button className="btn-primary" onClick={() => setModalState({ open: true, role: null })}>
              {t('identity.roles.add')}
            </button>
          )}
        </div>

        {canViewTenants && (
          <div className="max-w-xs">
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('identity.users.tenant')}</label>
            <select
              className="input"
              value={selectedTenantId}
              onChange={(e) => setSelectedTenantId(e.target.value)}
            >
              <option value="">{t('identity.users.myTenant')}</option>
              {(tenants ?? []).map((tn) => (
                <option key={tn.id} value={tn.id}>
                  {tn.tenantName}
                </option>
              ))}
            </select>
          </div>
        )}

        <DataTable columns={columns} data={roles ?? []} isLoading={isLoading} />
      </div>

      <div className="space-y-2">
        <h2 className="text-lg font-semibold">{t('identity.roles.permissionCatalog')}</h2>
        <p className="text-sm text-gray-500">{t('identity.roles.permissionCatalogHint')}</p>
        <DataTable columns={catalogColumns} data={catalog ?? []} isLoading={catalogLoading} />
      </div>

      {modalState.open && (
        <RoleModal
          role={modalState.role}
          tenantId={effectiveTenantId}
          onClose={() => setModalState({ open: false, role: null })}
        />
      )}
      {permsTarget && <ManagePermissionsModal role={permsTarget} onClose={() => setPermsTarget(null)} />}

      <ConfirmDialog
        open={!!deleteTarget}
        title={t('identity.roles.deleteTitle')}
        message={t('identity.roles.deleteConfirm', { name: deleteTarget?.roleName })}
        confirmLabel={t('common.delete')}
        danger
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  )
}
