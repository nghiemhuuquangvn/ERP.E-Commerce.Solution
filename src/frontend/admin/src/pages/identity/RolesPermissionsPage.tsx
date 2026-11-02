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
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('identity.roles.editTitle') : t('identity.roles.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="role-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="role-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField
            label={t('common.code')}
            required
            hint={t('identity.roles.codeHint')}
            error={errors.roleCode ? t('common.required') : undefined}
          >
            <Input {...register('roleCode', { required: true })} />
          </FormField>
        )}
        <FormField label={t('common.name')} required error={errors.roleName ? t('common.required') : undefined}>
          <Input {...register('roleName', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

function ManagePermissionsModal({ role, onClose }: { role: Role; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [filter, setFilter] = useState('')

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

  // Each tick is saved at once: say so when it worked, and say why when the server refused (the box then goes back).
  const after = {
    onSuccess: () => {
      invalidate()
      toast.success(t('common.saved'))
    },
    onError: (e: unknown) => {
      invalidate()
      toast.error(serverMessage(e, t('common.failedToSave')))
    },
  }

  const assignMutation = useMutation({
    mutationFn: (code: string) => rolesApi.assignPermission(role.id, code),
    ...after,
  })

  const removeMutation = useMutation({
    mutationFn: (permissionId: string) => rolesApi.removePermission(role.id, permissionId),
    ...after,
  })

  const assignedSet = useMemo(() => new Set(assignedCodes ?? []), [assignedCodes])
  const busy = assignMutation.isPending || removeMutation.isPending

  const grouped = useMemo(() => {
    const needle = filter.trim().toLowerCase()
    const groups = new Map<string, Permission[]>()
    for (const p of catalog ?? []) {
      if (needle && !`${p.permissionCode} ${p.permissionName ?? ''}`.toLowerCase().includes(needle)) continue
      const module = p.module ?? 'Other'
      const list = groups.get(module) ?? []
      list.push(p)
      groups.set(module, list)
    }
    return groups
  }, [catalog, filter])

  return (
    <Modal
      open
      size="md"
      title={t('identity.roles.managePermissionsTitle', { name: role.roleName })}
      onClose={onClose}
      footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}
    >
      <div className="space-y-4">
        <Input
          type="search"
          aria-label={t('identity.roles.filterPermissions')}
          placeholder={t('identity.roles.filterPermissions')}
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
        {grouped.size === 0 && (
          <EmptyState
            variant="no-results"
            title={t('common.noResults')}
            action={filter ? <Button variant="outline" size="sm" onClick={() => setFilter('')}>{t('common.clear')}</Button> : undefined}
          />
        )}
        {Array.from(grouped.entries()).map(([module, perms]) => (
          <fieldset key={module} className="space-y-1">
            <legend className="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-secondary">
              {module} · {t('identity.roles.granted', { count: perms.filter((p) => assignedSet.has(p.permissionCode)).length, total: perms.length })}
            </legend>
            {perms.map((p) => {
              const checked = assignedSet.has(p.permissionCode)
              return (
                <div key={p.id} className="flex items-center justify-between gap-3">
                  <Checkbox
                    label={p.permissionName ?? p.permissionCode}
                    checked={checked}
                    disabled={busy}
                    onChange={() => (checked ? removeMutation.mutate(p.id) : assignMutation.mutate(p.permissionCode))}
                  />
                  <span className="font-mono text-xs text-ink-secondary">{p.permissionCode}</span>
                </div>
              )
            })}
          </fieldset>
        ))}
      </div>
    </Modal>
  )
}

export function RolesPermissionsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const authUser = useAuthStore((s) => s.user)
  const queryClient = useQueryClient()
  const toast = useToast()

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
  // R90: role actions take the caller's own tenant; another tenant's roles can be read here, not changed.
  const viewingOther = canViewTenants && !!selectedTenantId && selectedTenantId !== authUser?.tenantId

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
      toast.success(t('common.saved'))
      setDeleteTarget(null)
    },
    onError: (e) => {
      setDeleteTarget(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
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
          <StatusBadge status={r.isActive ? 'Active' : 'Inactive'} label={r.isActive ? t('common.active') : t('common.inactive')} />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (r: Role) => viewingOther ? null : (
          <div className="flex gap-3">
            {canManage && (
              <Button
                variant="link"
                className="text-xs"
                aria-label={`${t('identity.roles.managePermissions')} ${r.roleName}`}
                onClick={() => setPermsTarget(r)}
              >
                {t('identity.roles.managePermissions')}
              </Button>
            )}
            {canEdit && (
              <Button
                variant="link"
                className="text-xs"
                aria-label={`${t('common.edit')} ${r.roleName}`}
                onClick={() => setModalState({ open: true, role: r })}
              >
                {t('common.edit')}
              </Button>
            )}
            {canDelete && (
              <Button
                variant="link-danger"
                className="text-xs"
                aria-label={`${t('common.delete')} ${r.roleName}`}
                onClick={() => setDeleteTarget(r)}
              >
                {t('common.delete')}
              </Button>
            )}
          </div>
        ),
      },
    ],
    [canManage, canEdit, canDelete, viewingOther, t],
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
          {canCreate && !viewingOther && <Button onClick={() => setModalState({ open: true, role: null })}>{t('identity.roles.add')}</Button>}
        </div>

        {canViewTenants && (
          <FormField label={t('identity.users.tenant')} className="max-w-xs">
            <Select value={selectedTenantId} onChange={(e) => setSelectedTenantId(e.target.value)}>
              <option value="">{t('identity.users.myTenant')}</option>
              {(tenants ?? []).map((tn) => (
                <option key={tn.id} value={tn.id}>
                  {tn.tenantName}
                </option>
              ))}
            </Select>
          </FormField>
        )}

        {viewingOther && <Alert variant="info">{t('identity.roles.otherTenantNotice')}</Alert>}

        <DataTable
          columns={columns}
          data={roles ?? []}
          isLoading={isLoading}
          emptyState={<EmptyState title={t('identity.roles.empty')} />}
        />
      </div>

      <div className="space-y-2">
        <h2 className="text-lg font-semibold">{t('identity.roles.permissionCatalog')}</h2>
        <p className="text-sm text-ink-secondary">{t('identity.roles.permissionCatalogHint')}</p>
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
