import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  integrationSettingsApi,
  type SystemIntegrationSettings,
  type TenantIntegrationSettings,
} from '@/api/settings/integration.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  externalIntegrationsAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'integration', 'system'],
    queryFn: () => integrationSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { externalIntegrationsAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => integrationSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemIntegrationSettings) => {
      queryClient.setQueryData(['settings', 'integration', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('externalIntegrationsAllowed')} />
        {t('settings.integration.externalIntegrationsAllowed')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.integration.failedToSave')}</p>}

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
  externalApiBaseUrl: string
  externalApiKey: string
  syncEnabled: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'integration', 'tenant', tenantId],
    queryFn: () => integrationSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { externalApiBaseUrl: '', externalApiKey: '', syncEnabled: false },
  })

  useEffect(() => {
    if (data) {
      reset({ externalApiBaseUrl: data.externalApiBaseUrl ?? '', externalApiKey: '', syncEnabled: data.syncEnabled })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      integrationSettingsApi.updateTenantSettings(tenantId, {
        externalApiBaseUrl: values.externalApiBaseUrl || null,
        // Empty means "leave the currently stored key unchanged" — never send a blank overwrite.
        externalApiKey: values.externalApiKey || undefined,
        syncEnabled: values.syncEnabled,
      }),
    onSuccess: (result: TenantIntegrationSettings) => {
      queryClient.setQueryData(['settings', 'integration', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.integration.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.integration.externalApiBaseUrl')}
        <input type="text" disabled={!canManage} {...register('externalApiBaseUrl')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.integration.externalApiKey')}
        <input
          type="password"
          disabled={!canManage}
          placeholder={data?.hasExternalApiKey ? t('settings.integration.apiKeyPlaceholderSet') : t('settings.integration.apiKeyPlaceholderUnset')}
          autoComplete="new-password"
          {...register('externalApiKey')}
          className="input mt-1 w-full"
        />
        <span className="text-xs text-gray-400">{t('settings.integration.apiKeyHint')}</span>
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('syncEnabled')} />
        {t('settings.integration.syncEnabled')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.integration.failedToSave')}</p>}

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

export function IntegrationSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:integration:system:view')
  const canManageSystem = hasPermission('settings:integration:system:manage')
  const canManageTenant = hasPermission('settings:integration:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.integration.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.integration.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.integration.title')}</h1>

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
