import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, useToast } from '@/components/ui'
import {
  notificationSettingsApi,
  type SystemNotificationSettings,
  type TenantNotificationSettings,
} from '@/api/settings/notification.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  emailChannelAvailable: boolean
  smsChannelAvailable: boolean
  pushChannelAvailable: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'notification', 'system'],
    queryFn: () => notificationSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { emailChannelAvailable: true, smsChannelAvailable: true, pushChannelAvailable: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => notificationSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemNotificationSettings) => {
      queryClient.setQueryData(['settings', 'notification', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('emailChannelAvailable')} label={t('settings.notification.emailChannelAvailable')} />
      <Checkbox disabled={!canManage} {...register('smsChannelAvailable')} label={t('settings.notification.smsChannelAvailable')} />
      <Checkbox disabled={!canManage} {...register('pushChannelAvailable')} label={t('settings.notification.pushChannelAvailable')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.notification.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  emailEnabledByDefault: boolean
  smsEnabledByDefault: boolean
  pushEnabledByDefault: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'notification', 'tenant', tenantId],
    queryFn: () => notificationSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { emailEnabledByDefault: true, smsEnabledByDefault: false, pushEnabledByDefault: false },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      notificationSettingsApi.updateTenantSettings(tenantId, values),
    onSuccess: (result: TenantNotificationSettings) => {
      queryClient.setQueryData(['settings', 'notification', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.notification.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('emailEnabledByDefault')} label={t('settings.notification.emailEnabledByDefault')} />
      <Checkbox disabled={!canManage} {...register('smsEnabledByDefault')} label={t('settings.notification.smsEnabledByDefault')} />
      <Checkbox disabled={!canManage} {...register('pushEnabledByDefault')} label={t('settings.notification.pushEnabledByDefault')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.notification.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function NotificationSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:notification:system:view')
  const canManageSystem = hasPermission('settings:notification:system:manage')
  const canManageTenant = hasPermission('settings:notification:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.notification.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.notification.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.notification.title')}</h1>

      <TabList id="notificationsettings" label={t('settings.notification.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
