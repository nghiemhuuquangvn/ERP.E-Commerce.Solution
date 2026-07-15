import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  localizationSettingsApi,
  type SystemLocalizationSettings,
  type TenantLocalizationSettings,
} from '@/api/settings/localization.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  multiLanguageAllowed: boolean
  defaultLocale: string
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'localization', 'system'],
    queryFn: () => localizationSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { multiLanguageAllowed: true, defaultLocale: 'en-US' },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => localizationSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemLocalizationSettings) => {
      queryClient.setQueryData(['settings', 'localization', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('multiLanguageAllowed')} />
        {t('settings.localization.multiLanguageAllowed')}
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.localization.defaultLocale')}
        <input type="text" disabled={!canManage} {...register('defaultLocale')} className="input mt-1 w-full" />
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.localization.failedToSave')}</p>}

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
  defaultLocale: string
  supportedLocales: string
  numberFormatLocale: string
  rtlEnabled: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'localization', 'tenant', tenantId],
    queryFn: () => localizationSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { defaultLocale: 'en-US', supportedLocales: 'en-US', numberFormatLocale: 'en-US', rtlEnabled: false },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) => localizationSettingsApi.updateTenantSettings(tenantId, values),
    onSuccess: (result: TenantLocalizationSettings) => {
      queryClient.setQueryData(['settings', 'localization', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.localization.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.localization.defaultLocale')}
        <input type="text" disabled={!canManage} {...register('defaultLocale')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.localization.supportedLocales')}
        <input type="text" disabled={!canManage} {...register('supportedLocales')} className="input mt-1 w-full" />
        <span className="text-xs text-gray-400">{t('settings.localization.supportedLocalesHint')}</span>
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.localization.numberFormatLocale')}
        <input type="text" disabled={!canManage} {...register('numberFormatLocale')} className="input mt-1 w-full" />
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('rtlEnabled')} />
        {t('settings.localization.rtlEnabled')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.localization.failedToSave')}</p>}

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

export function LocalizationSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:localization:system:view')
  const canManageSystem = hasPermission('settings:localization:system:manage')
  const canManageTenant = hasPermission('settings:localization:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.localization.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.localization.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.localization.title')}</h1>

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
