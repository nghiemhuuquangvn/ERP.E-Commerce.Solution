import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  schedulerSettingsApi,
  type SystemSchedulerSettings,
  type TenantSchedulerSettings,
} from '@/api/settings/scheduler.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  backgroundJobsEnabled: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('backgroundJobsEnabled')} label={t('settings.scheduler.backgroundJobsEnabled')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.scheduler.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.scheduler.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.scheduler.outboxPollingIntervalSeconds')}>
        <Input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('outboxPollingIntervalSeconds', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.scheduler.maxRetryAttempts')}>
        <Input
          type="number"
          min={0}
          disabled={!canManage}
          {...register('maxRetryAttempts', { valueAsNumber: true })}
 />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.scheduler.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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

      <TabList id="schedulersettings" label={t('settings.scheduler.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
