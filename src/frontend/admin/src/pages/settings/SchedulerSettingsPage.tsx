import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  schedulerSettingsApi,
  type SystemSchedulerSettings,
  type TenantSchedulerSettings,
} from '@/api/settings/scheduler.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  backgroundJobsEnabled: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'scheduler', 'system'],
    queryFn: () => schedulerSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { backgroundJobsEnabled: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => schedulerSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemSchedulerSettings) => {
      queryClient.setQueryData(['settings', 'scheduler', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('backgroundJobsEnabled')} />
        {t('settings.scheduler.backgroundJobsEnabled')}
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.scheduler.failedToSave')}</p>}

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
  outboxPollingIntervalSeconds: number
  maxRetryAttempts: number
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'scheduler', 'tenant', tenantId],
    queryFn: () => schedulerSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { outboxPollingIntervalSeconds: 30, maxRetryAttempts: 5 },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) => schedulerSettingsApi.updateTenantSettings(tenantId, values),
    onSuccess: (result: TenantSchedulerSettings) => {
      queryClient.setQueryData(['settings', 'scheduler', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.scheduler.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-ink-secondary">
        {t('settings.scheduler.outboxPollingIntervalSeconds')}
        <input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('outboxPollingIntervalSeconds', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('settings.scheduler.maxRetryAttempts')}
        <input
          type="number"
          min={0}
          disabled={!canManage}
          {...register('maxRetryAttempts', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.scheduler.failedToSave')}</p>}

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

export function SchedulerSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:scheduler:system:view')
  const canManageSystem = hasPermission('settings:scheduler:system:manage')
  const canManageTenant = hasPermission('settings:scheduler:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.scheduler.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.scheduler.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.scheduler.title')}</h1>

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
