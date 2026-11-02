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
import { Alert, Badge, Button, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface ProfileFormValues {
  fullName: string
  userName: string
}

function EditProfileModal({ user, onClose }: { user: UserDetail; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit } = useForm<ProfileFormValues>({
    defaultValues: { fullName: user.fullName ?? '', userName: user.userName ?? '' },
  })

  const mutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      usersApi.updateProfile(user.id, { fullName: values.fullName, userName: values.userName }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['identity', 'users'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('identity.users.editProfileTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="profile-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="profile-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <p className="text-sm text-ink-secondary">{user.email}</p>
        <FormField label={t('identity.users.fullName')}>
          <Input {...register('fullName')} />
        </FormField>
        <FormField label={t('identity.users.userName')}>
          <Input {...register('userName')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      setRoleCode('')
    },
  })

  const removeMutation = useMutation({
    mutationFn: (roleId: string) => usersApi.removeRole(user.id, roleId),
    onSuccess: () => {
      invalidate()
      toast.success(t('common.saved'))
    },
  })

  const assignableRoles = useMemo(
    () => (availableRoles ?? []).filter((r) => !user.roleAssignments.some((ra) => ra.roleId === r.id)),
    [availableRoles, user.roleAssignments],
  )

  const error = assignMutation.isError ? assignMutation.error : removeMutation.isError ? removeMutation.error : null

  return (
    <Modal
      open
      size="sm"
      title={t('identity.users.manageRolesTitle', { email: user.email })}
      onClose={onClose}
      footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}
    >
      <div className="space-y-4">
        {user.roleAssignments.length === 0 ? (
          <p className="text-sm text-ink-secondary">{t('identity.users.noRoles')}</p>
        ) : (
          <ul className="space-y-2">
            {user.roleAssignments.map((ra) => (
              <li key={ra.roleId} className="flex items-center justify-between rounded border border-line px-3 py-2">
                <span className="text-sm text-ink">{ra.roleName}</span>
                <Button
                  variant="link-danger"
                  className="text-xs"
                  aria-label={t('identity.users.removeRole', { role: ra.roleName })}
                  disabled={removeMutation.isPending}
                  onClick={() => removeMutation.mutate(ra.roleId)}
                >
                  {t('identity.users.remove')}
                </Button>
              </li>
            ))}
          </ul>
        )}

        <div className="flex items-end gap-2">
          <FormField label={t('identity.users.roleToAssign')} className="flex-1">
            <Select value={roleCode} onChange={(e) => setRoleCode(e.target.value)}>
              <option value="">{t('common.select')}</option>
              {assignableRoles.map((r) => (
                <option key={r.id} value={r.roleCode}>
                  {r.roleName}
                </option>
              ))}
            </Select>
          </FormField>
          <Button disabled={!roleCode} loading={assignMutation.isPending} onClick={() => assignMutation.mutate(roleCode)}>
            {t('identity.users.assignRole')}
          </Button>
        </div>

        {error && <Alert variant="danger">{serverMessage(error, t('common.failedToSave'))}</Alert>}
      </div>
    </Modal>
  )
}

interface InviteFormValues {
  email: string
  roleCode: string
}

function InviteUserModal({ tenantId, onClose }: { tenantId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit, reset, formState: { errors } } = useForm<InviteFormValues>({
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
      reset()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('identity.users.inviteTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.close')}</Button>
          <Button type="submit" form="invite-form" loading={mutation.isPending}>{t('identity.users.invite')}</Button>
        </>
      }
    >
      <form id="invite-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <p className="text-sm text-ink-secondary">{t('identity.users.inviteHint')}</p>
        <FormField label={t('identity.users.email')} required error={errors.email ? t('common.required') : undefined}>
          <Input type="email" {...register('email', { required: true })} />
        </FormField>
        <FormField label={t('identity.users.roleOptional')}>
          <Select {...register('roleCode')}>
            <option value="">{t('common.none')}</option>
            {(availableRoles ?? []).map((r) => (
              <option key={r.id} value={r.roleCode}>
                {r.roleName}
              </option>
            ))}
          </Select>
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('identity.users.inviteFailed'))}</Alert>}
        {mutation.isSuccess && (
          <Alert variant={mutation.data.alreadyMember ? 'info' : 'success'}>
            {mutation.data.alreadyMember ? t('identity.users.inviteAlreadyMember') : t('identity.users.inviteSuccess')}
          </Alert>
        )}
      </form>
    </Modal>
  )
}

export function UsersPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const authUser = useAuthStore((s) => s.user)
  const { page, pageSize, setPage, reset } = usePagination()
  const queryClient = useQueryClient()
  const toast = useToast()

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
  const [activateTarget, setActivateTarget] = useState<UserDetail | null>(null)

  const { data: tenants } = useQuery({
    queryKey: ['identity', 'tenants'],
    queryFn: () => tenantsApi.list(),
    enabled: canViewTenants,
    staleTime: 60_000,
  })

  const effectiveTenantId = canViewTenants && selectedTenantId ? selectedTenantId : authUser?.tenantId ?? ''
  // R90: every action takes the caller's own tenant; another tenant's users can be read here, not changed.
  const viewingOther = canViewTenants && !!selectedTenantId && selectedTenantId !== authUser?.tenantId

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
      toast.success(t('common.saved'))
      setLockTarget(null)
    },
    onError: (e) => {
      setLockTarget(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
    },
  })

  const deactivateMutation = useMutation({
    mutationFn: (u: UserDetail) => usersApi.deactivate(u.id),
    onSuccess: () => {
      invalidate()
      toast.success(t('common.saved'))
      setDeactivateTarget(null)
    },
    onError: (e) => {
      setDeactivateTarget(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
    },
  })

  const activateMutation = useMutation({
    mutationFn: (u: UserDetail) => usersApi.activate(u.id),
    onSuccess: () => {
      invalidate()
      toast.success(t('common.saved'))
      setActivateTarget(null)
    },
    onError: (e) => {
      setActivateTarget(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
    },
  })

  const columns = useMemo(
    () => [
      { key: 'email', label: t('identity.users.email') },
      { key: 'fullName', label: t('identity.users.fullName'), render: (u: UserDetail) => u.fullName ?? '—' },
      {
        key: 'roles',
        label: t('identity.users.roles'),
        render: (u: UserDetail) =>
          u.roles.length ? (
            <div className="flex flex-wrap gap-1">
              {u.roles.map((r) => (
                <Badge key={r}>{r}</Badge>
              ))}
            </div>
          ) : (
            '—'
          ),
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (u: UserDetail) => (
          <div className="flex gap-1">
            <StatusBadge status={u.isActive ? 'Active' : 'Inactive'} label={u.isActive ? t('common.active') : t('common.inactive')} />
            {u.isLocked && <StatusBadge status="Failed" label={t('identity.users.locked')} />}
          </div>
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (u: UserDetail) => {
          if (viewingOther) return null
          // Locking or deactivating the account you are signed in with would end your own session: not offered on your own row.
          const isSelf = u.id === authUser?.id
          return (
            <div className="flex flex-wrap gap-3">
              {canManage && (
                <Button
                  variant="link"
                  className="text-xs"
                  aria-label={`${t('identity.users.manageRoles')} ${u.email}`}
                  onClick={() => setRolesTarget(u)}
                >
                  {t('identity.users.manageRoles')}
                </Button>
              )}
              {canEdit && (
                <Button
                  variant="link"
                  className="text-xs"
                  aria-label={`${t('common.edit')} ${u.email}`}
                  onClick={() => setEditTarget(u)}
                >
                  {t('common.edit')}
                </Button>
              )}
              {canManage && !isSelf && (
                <Button
                  variant="link"
                  className="text-xs"
                  aria-label={`${u.isLocked ? t('identity.users.unlock') : t('identity.users.lock')} ${u.email}`}
                  onClick={() => setLockTarget(u)}
                >
                  {u.isLocked ? t('identity.users.unlock') : t('identity.users.lock')}
                </Button>
              )}
              {canManage && !u.isActive && !isSelf && (
                <Button
                  variant="link"
                  className="text-xs"
                  aria-label={`${t('identity.users.activate')} ${u.email}`}
                  onClick={() => setActivateTarget(u)}
                >
                  {t('identity.users.activate')}
                </Button>
              )}
              {canDelete && u.isActive && !isSelf && (
                <Button
                  variant="link-danger"
                  className="text-xs"
                  aria-label={`${t('common.deactivate')} ${u.email}`}
                  onClick={() => setDeactivateTarget(u)}
                >
                  {t('common.deactivate')}
                </Button>
              )}
            </div>
          )
        },
      },
    ],
    [canManage, canEdit, canDelete, authUser?.id, viewingOther, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('identity.users.title')}</h1>
        {canManage && !viewingOther && <Button onClick={() => setInviteOpen(true)}>{t('identity.users.invite')}</Button>}
      </div>

      {canViewTenants && (
        <FormField label={t('identity.users.tenant')} className="max-w-xs">
          <Select value={selectedTenantId} onChange={(e) => { setSelectedTenantId(e.target.value); reset() }}>
            <option value="">{t('identity.users.myTenant')}</option>
            {(tenants ?? []).map((tn) => (
              <option key={tn.id} value={tn.id}>
                {tn.tenantName}
              </option>
            ))}
          </Select>
        </FormField>
      )}

      {viewingOther && <Alert variant="info">{t('identity.users.otherTenantNotice')}</Alert>}

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={<EmptyState title={t('identity.users.empty')} />}
      />
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
        message={
          lockTarget?.isLocked
            ? t('identity.users.unlockConfirm', { email: lockTarget?.email })
            : t('identity.users.lockConfirm', { email: lockTarget?.email })
        }
        confirmLabel={lockTarget?.isLocked ? t('identity.users.unlock') : t('identity.users.lock')}
        danger={!lockTarget?.isLocked}
        onConfirm={() => lockTarget && lockMutation.mutate(lockTarget)}
        onCancel={() => setLockTarget(null)}
      />

      <ConfirmDialog
        open={!!activateTarget}
        title={t('identity.users.activateTitle')}
        message={t('identity.users.activateConfirm', { email: activateTarget?.email })}
        confirmLabel={t('identity.users.activate')}
        onConfirm={() => activateTarget && !activateMutation.isPending && activateMutation.mutate(activateTarget)}
        onCancel={() => setActivateTarget(null)}
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
