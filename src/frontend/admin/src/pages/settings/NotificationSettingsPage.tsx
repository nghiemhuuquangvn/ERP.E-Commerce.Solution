import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  notificationSettingsApi,
  type SystemNotificationSettings,
  type TenantNotificationSettings,
} from '@/api/settings/notification.settings.api'
import { usePermission } from '@/hooks/usePermission'
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
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('emailChannelAvailable')} />
        {t('settings.notification.emailChannelAvailable')}
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('smsChannelAvailable')} />
        {t('settings.notification.smsChannelAvailable')}
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('pushChannelAvailable')} />
        {t('settings.notification.pushChannelAvailable')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.notification.failedToSave')}</p>}

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
  emailEnabledByDefault: boolean
  smsEnabledByDefault: boolean
  pushEnabledByDefault: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

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
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.notification.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('emailEnabledByDefault')} />
        {t('settings.notification.emailEnabledByDefault')}
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('smsEnabledByDefault')} />
        {t('settings.notification.smsEnabledByDefault')}
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('pushEnabledByDefault')} />
        {t('settings.notification.pushEnabledByDefault')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.notification.failedToSave')}</p>}

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

      <div className="flex gap-2 border-b border-gray-200">
        {TABS.map((x) => (
          <button
            key={x.key}
            onClick={() => setTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key
                ? 'border-b-2 border-primary-600 text-primary-700'
                : 'text-gray-500 hover:text-gray-700'
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
