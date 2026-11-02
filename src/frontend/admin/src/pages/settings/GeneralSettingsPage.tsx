import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  generalSettingsApi,
  type SystemGeneralSettings,
  type TenantGeneralSettings,
} from '@/api/settings/general.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  maintenanceModeEnabled: boolean
  defaultLanguageCode: string
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'general', 'system'],
    queryFn: () => generalSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { maintenanceModeEnabled: false, defaultLanguageCode: 'en' },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => generalSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemGeneralSettings) => {
      queryClient.setQueryData(['settings', 'general', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('maintenanceModeEnabled')} />
        {t('settings.general.maintenanceModeEnabled')}
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('settings.general.defaultLanguageCode')}
        <input
          type="text"
          disabled={!canManage}
          {...register('defaultLanguageCode')}
          className="input mt-1 w-full"
        />
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.general.failedToSave')}</p>}

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
  siteName: string
  defaultLanguageCode: string
  timeZone: string
  dateFormat: string
  maintenanceModeEnabled: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'general', 'tenant', tenantId],
    queryFn: () => generalSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: {
      siteName: '',
      defaultLanguageCode: 'en',
      timeZone: 'UTC',
      dateFormat: 'yyyy-MM-dd',
      maintenanceModeEnabled: false,
    },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) => generalSettingsApi.updateTenantSettings(tenantId, values),
    onSuccess: (result: TenantGeneralSettings) => {
      queryClient.setQueryData(['settings', 'general', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.general.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-ink-secondary">
        {t('settings.general.siteName')}
        <input type="text" disabled={!canManage} {...register('siteName')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('settings.general.defaultLanguageCode')}
        <input
          type="text"
          disabled={!canManage}
          {...register('defaultLanguageCode')}
          className="input mt-1 w-full"
        />
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('settings.general.timeZone')}
        <input type="text" disabled={!canManage} {...register('timeZone')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('settings.general.dateFormat')}
        <input type="text" disabled={!canManage} {...register('dateFormat')} className="input mt-1 w-full" />
      </label>
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('maintenanceModeEnabled')} />
        {t('settings.general.maintenanceModeEnabled')}
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.general.failedToSave')}</p>}

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

export function GeneralSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:general:system:view')
  const canManageSystem = hasPermission('settings:general:system:manage')
  const canManageTenant = hasPermission('settings:general:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.general.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.general.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.general.title')}</h1>

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
