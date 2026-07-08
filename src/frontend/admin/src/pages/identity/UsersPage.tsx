import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { usersApi } from '@/api/users.api'
import type { UserDetail } from '@/api/users.api'
import { rolesApi } from '@/api/roles.api'
import { tenantsApi } from '@/api/tenants.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

interface ProfileFormValues {
  fullName: string
  userName: string
}

function EditProfileModal({ user, onClose }: { user: UserDetail; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit } = useForm<ProfileFormValues>({
    defaultValues: { fullName: user.fullName ?? '', userName: user.userName ?? '' },
  })

  const mutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      usersApi.updateProfile(user.id, { fullName: values.fullName, userName: values.userName }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['identity', 'users'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('identity.users.editProfileTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('identity.users.fullName')}</label>
            <input className="input" {...register('fullName')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('identity.users.userName')}</label>
            <input className="input" {...register('userName')} />
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

function ManageRolesModal({
  user,
  tenantId,
  onClose,
}: {
  user: UserDetail
  tenantId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [roleCode, setRoleCode] = useState('')

  const { data: availableRoles } = useQuery({
    queryKey: ['identity', 'roles', tenantId],
    queryFn: () => rolesApi.list({ tenantId }),
    staleTime: 30_000,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['identity', 'users'] })

  const assignMutation = useMutation({
    mutationFn: (code: string) => usersApi.assignRole(user.id, code),
    onSuccess: () => {
      invalidate()
      setRoleCode('')
    },
  })

  const removeMutation = useMutation({
    mutationFn: (roleId: string) => usersApi.removeRole(user.id, roleId),
    onSuccess: invalidate,
  })

  const assignableRoles = useMemo(
    () => (availableRoles ?? []).filter((r) => !user.roleAssignments.some((ra) => ra.roleId === r.id)),
    [availableRoles, user.roleAssignments],
  )

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('identity.users.manageRolesTitle', { email: user.email })}
        </h3>

        <ul className="mb-4 space-y-2">
          {user.roleAssignments.length === 0 && (
            <li className="text-sm text-gray-400">{t('identity.users.noRoles')}</li>
          )}
          {user.roleAssignments.map((ra) => (
            <li key={ra.roleId} className="flex items-center justify-between rounded border border-gray-200 px-3 py-2">
              <span className="text-sm text-gray-800">{ra.roleName}</span>
              <button
                className="text-xs text-red-600 hover:underline disabled:opacity-40"
                disabled={removeMutation.isPending}
                onClick={() => removeMutation.mutate(ra.roleId)}
              >
                {t('common.delete')}
              </button>
            </li>
          ))}
        </ul>

        <div className="flex gap-2">
          <select className="input flex-1" value={roleCode} onChange={(e) => setRoleCode(e.target.value)}>
            <option value="">{t('common.select')}</option>
            {assignableRoles.map((r) => (
              <option key={r.id} value={r.roleCode}>
                {r.roleName}
              </option>
            ))}
          </select>
          <button
            className="btn-primary"
            disabled={!roleCode || assignMutation.isPending}
            onClick={() => assignMutation.mutate(roleCode)}
          >
            {t('identity.users.assignRole')}
          </button>
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

interface InviteFormValues {
  email: string
  roleCode: string
}

function InviteUserModal({ tenantId, onClose }: { tenantId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<InviteFormValues>({
    defaultValues: { email: '', roleCode: '' },
  })

  const { data: availableRoles } = useQuery({
    queryKey: ['identity', 'roles', tenantId],
    queryFn: () => rolesApi.list({ tenantId }),
    staleTime: 30_000,
  })

  const mutation = useMutation({
    mutationFn: (values: InviteFormValues) =>
      usersApi.invite({ email: values.email, roleCode: values.roleCode || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['identity', 'users'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('identity.users.inviteTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('identity.users.email')}</label>
            <input type="email" className="input" {...register('email', { required: true })} />
            {errors.email && <p className="mt-1 text-xs text-red-600">{t('identity.users.email')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('identity.users.roleOptional')}
            </label>
            <select className="input" {...register('roleCode')}>
              <option value="">{t('common.none')}</option>
              {(availableRoles ?? []).map((r) => (
                <option key={r.id} value={r.roleCode}>
                  {r.roleName}
                </option>
              ))}
            </select>
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('identity.users.inviteFailed')}</p>}
          {mutation.isSuccess && (
            <p className="text-xs text-green-600">
              {mutation.data.alreadyMember
                ? t('identity.users.inviteAlreadyMember')
                : t('identity.users.inviteSuccess')}
            </p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.close')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('identity.users.invite')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function UsersPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const authUser = useAuthStore((s) => s.user)
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()

  const canViewTenants = hasPermission('identity:tenants:view')
  const canManage = hasPermission('identity:users:manage')
  const canEdit = hasPermission('identity:users:edit')
  const canDelete = hasPermission('identity:users:delete')

  const [selectedTenantId, setSelectedTenantId] = useState<string>('')
  const [editTarget, setEditTarget] = useState<UserDetail | null>(null)
  const [rolesTarget, setRolesTarget] = useState<UserDetail | null>(null)
  const [inviteOpen, setInviteOpen] = useState(false)
  const [lockTarget, setLockTarget] = useState<UserDetail | null>(null)
  const [deactivateTarget, setDeactivateTarget] = useState<UserDetail | null>(null)

  const { data: tenants } = useQuery({
    queryKey: ['identity', 'tenants'],
    queryFn: () => tenantsApi.list(),
    enabled: canViewTenants,
    staleTime: 60_000,
  })

  const effectiveTenantId = canViewTenants && selectedTenantId ? selectedTenantId : authUser?.tenantId ?? ''

  const { data, isLoading } = useQuery({
    queryKey: ['identity', 'users', { tenantId: effectiveTenantId, page, pageSize }],
    queryFn: () => usersApi.list({ tenantId: effectiveTenantId || undefined, page, pageSize }),
    staleTime: 15_000,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['identity', 'users'] })

  const lockMutation = useMutation({
    mutationFn: (u: UserDetail) => (u.isLocked ? usersApi.unlock(u.id) : usersApi.lock(u.id)),
    onSuccess: () => {
      invalidate()
      setLockTarget(null)
    },
  })

  const deactivateMutation = useMutation({
    mutationFn: (u: UserDetail) => usersApi.deactivate(u.id),
    onSuccess: () => {
      invalidate()
      setDeactivateTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'email', label: t('identity.users.email') },
      { key: 'fullName', label: t('identity.users.fullName'), render: (u: UserDetail) => u.fullName ?? '—' },
      {
        key: 'roles',
        label: t('identity.users.roles'),
        render: (u: UserDetail) => (u.roles.length ? u.roles.join(', ') : '—'),
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (u: UserDetail) => (
          <div className="flex gap-1">
            <StatusBadge status={u.isActive ? 'Active' : 'Cancelled'} label={u.isActive ? t('common.activate') : t('common.deactivate')} />
            {u.isLocked && <StatusBadge status="Failed" label={t('identity.users.locked')} />}
          </div>
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (u: UserDetail) => (
          <div className="flex flex-wrap gap-3">
            {canManage && (
              <button className="text-xs text-primary-600 hover:underline" onClick={() => setRolesTarget(u)}>
                {t('identity.users.manageRoles')}
              </button>
            )}
            {canEdit && (
              <button className="text-xs text-primary-600 hover:underline" onClick={() => setEditTarget(u)}>
                {t('common.edit')}
              </button>
            )}
            {canManage && (
              <button className="text-xs text-yellow-700 hover:underline" onClick={() => setLockTarget(u)}>
                {u.isLocked ? t('identity.users.unlock') : t('identity.users.lock')}
              </button>
            )}
            {canDelete && u.isActive && (
              <button className="text-xs text-red-600 hover:underline" onClick={() => setDeactivateTarget(u)}>
                {t('common.deactivate')}
              </button>
            )}
          </div>
        ),
      },
    ],
    [canManage, canEdit, canDelete, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('identity.users.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setInviteOpen(true)}>
            {t('identity.users.invite')}
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

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {editTarget && <EditProfileModal user={editTarget} onClose={() => setEditTarget(null)} />}
      {rolesTarget && (
        <ManageRolesModal
          user={data?.items.find((u) => u.id === rolesTarget.id) ?? rolesTarget}
          tenantId={effectiveTenantId}
          onClose={() => setRolesTarget(null)}
        />
      )}
      {inviteOpen && <InviteUserModal tenantId={effectiveTenantId} onClose={() => setInviteOpen(false)} />}

      <ConfirmDialog
        open={!!lockTarget}
        title={lockTarget?.isLocked ? t('identity.users.unlockTitle') : t('identity.users.lockTitle')}
        message={t('identity.users.lockConfirm', { email: lockTarget?.email })}
        confirmLabel={lockTarget?.isLocked ? t('identity.users.unlock') : t('identity.users.lock')}
        danger={!lockTarget?.isLocked}
        onConfirm={() => lockTarget && lockMutation.mutate(lockTarget)}
        onCancel={() => setLockTarget(null)}
      />

      <ConfirmDialog
        open={!!deactivateTarget}
        title={t('identity.users.deactivateTitle')}
        message={t('identity.users.deactivateConfirm', { email: deactivateTarget?.email })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => deactivateTarget && deactivateMutation.mutate(deactivateTarget)}
        onCancel={() => setDeactivateTarget(null)}
      />
    </div>
  )
}
