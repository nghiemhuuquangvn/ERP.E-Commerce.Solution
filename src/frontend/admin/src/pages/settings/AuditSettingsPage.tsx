import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  auditSettingsApi,
  type SystemAuditSettings,
  type TenantAuditSettings,
} from '@/api/settings/audit.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  auditLoggingEnabled: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('auditLoggingEnabled')} label={t('settings.audit.auditLoggingEnabled')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.audit.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.audit.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.audit.retentionDays')}>
        <Input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('retentionDays', { valueAsNumber: true })}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('logSuccessfulLogins')} label={t('settings.audit.logSuccessfulLogins')} />
      <Checkbox disabled={!canManage} {...register('logFailedLogins')} label={t('settings.audit.logFailedLogins')} />
      <Checkbox disabled={!canManage} {...register('logPermissionChanges')} label={t('settings.audit.logPermissionChanges')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.audit.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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

      <TabList id="auditsettings" label={t('settings.audit.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
