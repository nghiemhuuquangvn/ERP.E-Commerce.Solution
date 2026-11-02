import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  auditSettingsApi,
  type SystemAuditSettings,
  type TenantAuditSettings,
} from '@/api/settings/audit.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  auditLoggingEnabled: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'audit', 'system'],
    queryFn: () => auditSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { auditLoggingEnabled: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => auditSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemAuditSettings) => {
      queryClient.setQueryData(['settings', 'audit', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('auditLoggingEnabled')} />
        {t('settings.audit.auditLoggingEnabled')}
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.audit.failedToSave')}</p>}

      {canManage && (
        <div className="pt-2">
          <button type="submit" disabled={mutation.isPending} className="btn-primary">
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  retentionDays: number
  logSuccessfulLogins: boolean
  logFailedLogins: boolean
  logPermissionChanges: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'audit', 'tenant', tenantId],
    queryFn: () => auditSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { retentionDays: 90, logSuccessfulLogins: true, logFailedLogins: true, logPermissionChanges: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) => auditSettingsApi.updateTenantSettings(tenantId, values),
    onSuccess: (result: TenantAuditSettings) => {
      queryClient.setQueryData(['settings', 'audit', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.audit.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-ink-secondary">
        {t('settings.audit.retentionDays')}
        <input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('retentionDays', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('logSuccessfulLogins')} />
        {t('settings.audit.logSuccessfulLogins')}
      </label>
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('logFailedLogins')} />
        {t('settings.audit.logFailedLogins')}
      </label>
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('logPermissionChanges')} />
        {t('settings.audit.logPermissionChanges')}
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.audit.failedToSave')}</p>}

      {canManage && (
        <div className="pt-2">
          <button type="submit" disabled={mutation.isPending} className="btn-primary">
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
    </form>
  )
}

export function AuditSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:audit:system:view')
  const canManageSystem = hasPermission('settings:audit:system:manage')
  const canManageTenant = hasPermission('settings:audit:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.audit.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.audit.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.audit.title')}</h1>

      <div className="flex gap-2 border-b border-line">
        {TABS.map((x) => (
          <button
            key={x.key}
            onClick={() => setTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key
                ? 'border-b-2 border-brand-action text-brand-action'
                : 'text-ink-muted hover:text-ink'
            }`}
          >
            {x.label}
          </button>
        ))}
      </div>

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
